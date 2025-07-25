# System Tray - Cross-Platform Functional Reactive Architecture

## Overview

The System Tray integration in DisplaySwitch-Pro implements a cross-platform functional reactive approach using the ECS architecture. The tray functionality is handled by platform adapters that manage all side effects, while the core tray logic remains pure and testable. This design ensures consistent behavior across Windows, macOS, and Linux environments.

## Architectural Principles

### Effect-Based System Tray
All tray operations are modeled as effects that are handled by platform adapters:

```fsharp
type TrayEffect =
    | CreateTrayIcon of iconPath: string * tooltip: string
    | UpdateTrayIcon of icon: TrayIcon * newState: TrayState
    | ShowTrayMenu of menuItems: TrayMenuItem list * position: Point
    | ShowBalloonNotification of title: string * message: string * icon: NotificationIcon
    | MinimizeToTray of windowHandle: WindowHandle
    | RestoreFromTray of windowHandle: WindowHandle
    | RemoveTrayIcon of icon: TrayIcon

// Effects are handled by platform-specific adapters
let handleTrayEffect (effect: TrayEffect) : Async<TrayResult> =
    async {
        match effect with
        | CreateTrayIcon (iconPath, tooltip) ->
            return! PlatformTrayAdapter.createIcon iconPath tooltip
        | ShowBalloonNotification (title, message, icon) ->
            return! PlatformTrayAdapter.showNotification title message icon
        | _ -> return! PlatformTrayAdapter.handleEffect effect
    }
```

### Immutable Tray State
Tray state is managed immutably and flows through the system:

```fsharp
type TrayState = {
    IsVisible: bool
    CurrentMode: DisplayMode option
    MenuItems: TrayMenuItem list
    PendingNotifications: Notification list
    LastInteraction: DateTime option
}

type TrayMenuItem = {
    Id: string
    Text: string
    IsEnabled: bool
    IsChecked: bool
    Shortcut: string option
    Action: TrayAction
}

type TrayAction =
    | SetDisplayMode of DisplayMode
    | ShowMainWindow
    | ExitApplication
    | OpenSettings
    | RefreshDisplays
```

## Platform Adapter Pattern

The platform adapter pattern isolates platform-specific tray implementations:

### Windows Platform Adapter
```fsharp
module WindowsTrayAdapter =
    open System.Windows.Forms
    
    type WindowsTrayIcon = {
        NotifyIcon: NotifyIcon
        ContextMenu: ContextMenuStrip
        BalloonTip: ToolTip
    }
    
    let createIcon (iconPath: string) (tooltip: string) : Async<TrayIcon> =
        async {
            let notifyIcon = new NotifyIcon()
            notifyIcon.Icon <- Icon.FromHandle(iconPath)
            notifyIcon.Text <- tooltip
            notifyIcon.Visible <- true
            
            return { 
                Handle = notifyIcon :> obj
                Platform = Windows
                IsVisible = true 
            }
        }
    
    let showNotification (title: string) (message: string) (icon: NotificationIcon) : Async<unit> =
        async {
            let notifyIcon = getCurrentNotifyIcon()
            let tipIcon = match icon with
                | Info -> ToolTipIcon.Info
                | Warning -> ToolTipIcon.Warning
                | Error -> ToolTipIcon.Error
            
            notifyIcon.ShowBalloonTip(3000, title, message, tipIcon)
        }
```

### macOS Platform Adapter
```fsharp
module MacOSTrayAdapter =
    open Foundation
    open AppKit
    
    type MacOSTrayIcon = {
        StatusItem: NSStatusItem
        Menu: NSMenu
        Icon: NSImage
    }
    
    let createIcon (iconPath: string) (tooltip: string) : Async<TrayIcon> =
        async {
            let statusBar = NSStatusBar.SystemStatusBar
            let statusItem = statusBar.CreateStatusItem(NSStatusItemLength.Variable)
            
            let icon = NSImage.FromFile(iconPath)
            icon.Template <- true // Adapts to dark/light mode
            
            statusItem.Image <- icon
            statusItem.ToolTip <- tooltip
            
            return {
                Handle = statusItem :> obj
                Platform = MacOS
                IsVisible = true
            }
        }
    
    let showNotification (title: string) (message: string) (icon: NotificationIcon) : Async<unit> =
        async {
            let notification = new NSUserNotification()
            notification.Title <- title
            notification.InformativeText <- message
            
            let center = NSUserNotificationCenter.DefaultUserNotificationCenter
            center.DeliverNotification(notification)
        }
```

### Linux Platform Adapter
```fsharp
module LinuxTrayAdapter =
    open Gtk
    
    type LinuxTrayIcon = {
        StatusIcon: StatusIcon
        Menu: Menu
        Pixbuf: Gdk.Pixbuf
    }
    
    let createIcon (iconPath: string) (tooltip: string) : Async<TrayIcon> =
        async {
            let pixbuf = new Gdk.Pixbuf(iconPath)
            let statusIcon = new StatusIcon(pixbuf)
            
            statusIcon.Tooltip <- tooltip
            statusIcon.Visible <- true
            
            return {
                Handle = statusIcon :> obj
                Platform = Linux
                IsVisible = true
            }
        }
    
    let showNotification (title: string) (message: string) (icon: NotificationIcon) : Async<unit> =
        async {
            // Use libnotify for Linux desktop notifications
            let iconName = match icon with
                | Info -> "dialog-information"
                | Warning -> "dialog-warning"
                | Error -> "dialog-error"
            
            do! LibNotify.showNotification title message iconName
        }
```

## Functional Menu System

Tray menus are defined as pure data and transformed by platform adapters:

```fsharp
module TrayMenu =
    let createMenuItems (state: TrayState) : TrayMenuItem list =
        [
            {
                Id = "pc-mode"
                Text = "PC Mode (All Displays)"
                IsEnabled = true
                IsChecked = (state.CurrentMode = Some PCMode)
                Shortcut = Some "Ctrl+1"
                Action = SetDisplayMode PCMode
            }
            
            {
                Id = "tv-mode" 
                Text = "TV Mode (Single Display)"
                IsEnabled = true
                IsChecked = (state.CurrentMode = Some TVMode)
                Shortcut = Some "Ctrl+2"
                Action = SetDisplayMode TVMode
            }
            
            {
                Id = "separator-1"
                Text = "---"
                IsEnabled = false
                IsChecked = false
                Shortcut = None
                Action = NoAction
            }
            
            {
                Id = "show-window"
                Text = "Show Window"
                IsEnabled = true
                IsChecked = false
                Shortcut = None
                Action = ShowMainWindow
            }
            
            {
                Id = "refresh"
                Text = "Refresh Displays"
                IsEnabled = true
                IsChecked = false
                Shortcut = Some "Ctrl+R"
                Action = RefreshDisplays
            }
            
            {
                Id = "separator-2"
                Text = "---"
                IsEnabled = false
                IsChecked = false
                Shortcut = None
                Action = NoAction
            }
            
            {
                Id = "exit"
                Text = "Exit"
                IsEnabled = true
                IsChecked = false
                Shortcut = None
                Action = ExitApplication
            }
        ]

    // Platform adapters transform menu data to native menus
    let transformToNativeMenu (items: TrayMenuItem list) (platform: Platform) : obj =
        match platform with
        | Windows -> WindowsMenuTransformer.transform items
        | MacOS -> MacOSMenuTransformer.transform items  
        | Linux -> LinuxMenuTransformer.transform items
```

## Event-Driven Updates

Tray updates are handled through an event-driven system:

```fsharp
type TrayEvent =
    | DisplayModeChanged of DisplayMode
    | DisplaysRefreshed of DisplayInfo list
    | WindowMinimized
    | WindowRestored
    | UserClickedTrayIcon
    | UserRightClickedTrayIcon
    | MenuItemSelected of menuItemId: string

let updateTrayState (event: TrayEvent) (state: TrayState) : TrayState * TrayEffect list =
    match event with
    | DisplayModeChanged newMode ->
        let newState = { state with CurrentMode = Some newMode }
        let updateEffect = UpdateTrayIcon (getCurrentTrayIcon(), newState)
        let notificationEffect = ShowBalloonNotification (
            "Display Manager",
            $"{DisplayMode.toString newMode} activated",
            Info
        )
        newState, [updateEffect; notificationEffect]
        
    | WindowMinimized ->
        let notification = ShowBalloonNotification (
            "Display Manager",
            "Minimized to system tray. Click icon to restore.",
            Info
        )
        state, [notification]
        
    | UserClickedTrayIcon ->
        let restoreEffect = RestoreFromTray (getCurrentWindowHandle())
        state, [restoreEffect]
        
    | UserRightClickedTrayIcon ->
        let menuItems = TrayMenu.createMenuItems state
        let showMenuEffect = ShowTrayMenu (menuItems, getCurrentCursorPosition())
        state, [showMenuEffect]
        
    | MenuItemSelected menuItemId ->
        let menuItem = state.MenuItems |> List.find (fun item -> item.Id = menuItemId)
        let effect = translateActionToEffect menuItem.Action
        state, [effect]
```

## Cross-Platform Icon Management

Icons are managed through a unified interface that adapts to platform conventions:

```fsharp
module TrayIconManager =
    type IconTheme = Light | Dark | Auto
    
    type TrayIconSet = {
        Default: string
        PCMode: string  
        TVMode: string
        Error: string
        Disabled: string
    }
    
    let getIconSet (platform: Platform) (theme: IconTheme) : TrayIconSet =
        match platform, theme with
        | Windows, _ -> {
            Default = "icons/windows/tray-default.ico"
            PCMode = "icons/windows/tray-pc.ico"
            TVMode = "icons/windows/tray-tv.ico"
            Error = "icons/windows/tray-error.ico"
            Disabled = "icons/windows/tray-disabled.ico"
        }
        | MacOS, Light -> {
            Default = "icons/macos/tray-default-light.png"
            PCMode = "icons/macos/tray-pc-light.png"
            TVMode = "icons/macos/tray-tv-light.png"
            Error = "icons/macos/tray-error-light.png"
            Disabled = "icons/macos/tray-disabled-light.png"
        }
        | MacOS, Dark -> {
            Default = "icons/macos/tray-default-dark.png"
            PCMode = "icons/macos/tray-pc-dark.png"
            TVMode = "icons/macos/tray-tv-dark.png"
            Error = "icons/macos/tray-error-dark.png"
            Disabled = "icons/macos/tray-disabled-dark.png"
        }
        | Linux, _ -> {
            Default = "icons/linux/tray-default.svg"
            PCMode = "icons/linux/tray-pc.svg"
            TVMode = "icons/linux/tray-tv.svg"
            Error = "icons/linux/tray-error.svg"
            Disabled = "icons/linux/tray-disabled.svg"
        }
    
    let selectIcon (iconSet: TrayIconSet) (mode: DisplayMode option) (hasError: bool) : string =
        match hasError, mode with
        | true, _ -> iconSet.Error
        | false, Some PCMode -> iconSet.PCMode
        | false, Some TVMode -> iconSet.TVMode
        | false, None -> iconSet.Default
```

## Notification System

Notifications are handled uniformly across platforms:

```fsharp
module TrayNotifications =
    type NotificationPriority = Low | Normal | High | Critical
    
    type Notification = {
        Id: string
        Title: string
        Message: string
        Icon: NotificationIcon
        Priority: NotificationPriority
        Duration: TimeSpan option
        Actions: NotificationAction list
    }
    
    type NotificationAction = {
        Id: string
        Text: string
        Action: TrayAction
    }
    
    let createModeChangeNotification (mode: DisplayMode) : Notification =
        {
            Id = Guid.NewGuid().ToString()
            Title = "Display Manager"
            Message = $"{DisplayMode.toString mode} activated"
            Icon = Info
            Priority = Normal
            Duration = Some (TimeSpan.FromSeconds 3.0)
            Actions = []
        }
    
    let createErrorNotification (error: string) : Notification =
        {
            Id = Guid.NewGuid().ToString()
            Title = "Display Manager - Error"
            Message = error
            Icon = Error
            Priority = High
            Duration = Some (TimeSpan.FromSeconds 5.0)
            Actions = [
                {
                    Id = "retry"
                    Text = "Retry"
                    Action = RefreshDisplays
                }
            ]
        }
```

## Testing Strategy

The functional approach enables comprehensive testing:

### Pure Function Tests
```fsharp
[<Test>]
let ``updateTrayState with DisplayModeChanged updates current mode`` () =
    // Arrange
    let initialState = { TrayState.empty with CurrentMode = None }
    let event = DisplayModeChanged PCMode
    
    // Act
    let newState, effects = updateTrayState event initialState
    
    // Assert
    newState.CurrentMode |> should equal (Some PCMode)
    effects |> should contain (UpdateTrayIcon (_, newState))
    effects |> should contain (ShowBalloonNotification ("Display Manager", "PC Mode activated", Info))

[<Test>]
let ``createMenuItems marks correct mode as checked`` () =
    // Arrange
    let state = { TrayState.empty with CurrentMode = Some TVMode }
    
    // Act
    let menuItems = TrayMenu.createMenuItems state
    
    // Assert
    let tvModeItem = menuItems |> List.find (fun item -> item.Id = "tv-mode")
    let pcModeItem = menuItems |> List.find (fun item -> item.Id = "pc-mode")
    
    tvModeItem.IsChecked |> should be True
    pcModeItem.IsChecked |> should be False
```

### Platform Adapter Tests
```fsharp
[<Test>]
let ``WindowsTrayAdapter creates visible tray icon`` () =
    async {
        // Arrange
        let iconPath = "test-icon.ico"
        let tooltip = "Test Tooltip"
        
        // Act
        let! trayIcon = WindowsTrayAdapter.createIcon iconPath tooltip
        
        // Assert
        trayIcon.IsVisible |> should be True
        trayIcon.Platform |> should equal Windows
    }

[<Test>]
let ``MacOSTrayAdapter shows notification`` () =
    async {
        // Arrange
        let title = "Test Title"
        let message = "Test Message"
        let icon = Info
        
        // Act & Assert (no exception thrown)
        do! MacOSTrayAdapter.showNotification title message icon
    }
```

### Integration Tests
```fsharp
[<Test>]
let ``Clicking tray icon restores window`` () =
    async {
        // Arrange
        let trayState = TrayState.empty
        let event = UserClickedTrayIcon
        
        // Act
        let newState, effects = updateTrayState event trayState
        
        // Assert
        effects |> should contain (RestoreFromTray _)
    }
```

## Error Handling and Resilience

Error handling is built into the effect system:

```fsharp
type TrayResult<'T> =
    | Success of 'T
    | PlatformNotSupported of string
    | PermissionDenied of string
    | ResourceUnavailable of string
    | UnknownError of exn

let handleTrayEffectSafely (effect: TrayEffect) : Async<TrayResult<unit>> =
    async {
        try
            do! handleTrayEffect effect
            return Success ()
        with
        | :? UnauthorizedAccessException as ex ->
            return PermissionDenied ex.Message
        | :? PlatformNotSupportedException as ex ->
            return PlatformNotSupported ex.Message
        | ex ->
            return UnknownError ex
    }

// Retry logic for transient failures
let executeWithRetry (effect: TrayEffect) (maxRetries: int) : Async<TrayResult<unit>> =
    let rec attempt retriesLeft =
        async {
            let! result = handleTrayEffectSafely effect
            match result, retriesLeft with
            | Success (), _ -> return result
            | (ResourceUnavailable _ | UnknownError _), n when n > 0 ->
                do! Async.Sleep 1000
                return! attempt (n - 1)
            | _ -> return result
        }
    attempt maxRetries
```

## Performance Optimizations

### Icon Caching
```fsharp
module IconCache =
    let private cache = ConcurrentDictionary<string, obj>()
    
    let getOrCreateIcon (iconPath: string) (factory: string -> Async<obj>) : Async<obj> =
        async {
            match cache.TryGetValue(iconPath) with
            | true, icon -> return icon
            | false, _ ->
                let! icon = factory iconPath
                cache.TryAdd(iconPath, icon) |> ignore
                return icon
        }
```

### Notification Throttling
```fsharp
module NotificationThrottling =
    let private lastNotificationTime = ref DateTime.MinValue
    let private minimumInterval = TimeSpan.FromSeconds 1.0
    
    let shouldShowNotification (notification: Notification) : bool =
        let now = DateTime.Now
        let timeSinceLastNotification = now - !lastNotificationTime
        
        match notification.Priority with
        | Critical -> true
        | High -> timeSinceLastNotification > (minimumInterval / 2.0)
        | Normal -> timeSinceLastNotification > minimumInterval
        | Low -> timeSinceLastNotification > (minimumInterval * 2.0)
```

## Integration with ECS

The tray system integrates with the ECS world:

```fsharp
// Tray system reads from ECS components
let createTrayStateFromWorld (world: World) : TrayState =
    let displayEntities = world.Query<DisplayComponent>()
    let activeDisplays = displayEntities |> Seq.filter (fun (entity, _) -> 
        world.HasComponent<ActiveComponent>(entity))
    
    let currentMode = 
        match Seq.length activeDisplays with
        | 1 -> Some TVMode
        | n when n > 1 -> Some PCMode
        | _ -> None
    
    {
        IsVisible = true
        CurrentMode = currentMode
        MenuItems = TrayMenu.createMenuItems { TrayState.empty with CurrentMode = currentMode }
        PendingNotifications = []
        LastInteraction = None
    }

// Tray events can trigger ECS updates
let handleTrayEventInWorld (event: TrayEvent) (world: World) : World =
    match event with
    | MenuItemSelected "pc-mode" ->
        world.SendMessage(SetDisplayMode PCMode)
    | MenuItemSelected "tv-mode" ->
        world.SendMessage(SetDisplayMode TVMode)
    | MenuItemSelected "refresh" ->
        world.SendMessage(RefreshDisplays)
    | _ -> world
```

## Benefits of Functional Approach

### Testability
- Pure functions are easy to test in isolation
- No need to mock platform-specific tray APIs
- Effect system allows testing without actual tray operations
- Platform adapters can be tested separately

### Maintainability  
- Platform-specific code is isolated in adapters
- Core tray logic is platform-agnostic
- Changes to one platform don't affect others
- Clear separation of concerns

### Cross-Platform Consistency
- Same functional interface across all platforms
- Unified notification and menu systems
- Consistent behavior regardless of platform
- Easy to add new platform support

### Reliability
- Immutable state prevents race conditions
- Effect system handles errors gracefully
- Retry logic for transient failures
- Graceful degradation when tray unavailable

The functional reactive approach to system tray integration provides a robust, testable, and maintainable foundation that works consistently across all supported platforms while maintaining the pure functional principles of the overall architecture.