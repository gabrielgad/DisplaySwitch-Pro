# System Tray Implementation - DisplaySwitch-Pro

## Overview

This document outlines the implementation plan for system tray functionality in DisplaySwitch-Pro, allowing the application to run minimized in the system tray with quick access to preset switching and core functionality.

## Architecture Integration

### Current Foundation

The system tray implementation leverages DisplaySwitch-Pro's excellent functional programming architecture:

- **Event-driven UI system** (`UIEventSystem.fs`) for tray event handling
- **Unified state management** (`UIStateManager.fs`) for tray state tracking
- **Enhanced preset management** with metadata for tray menu population
- **Comprehensive configuration system** for tray settings
- **Avalonia 11.0.0** with built-in TrayIcon support

### Technology Stack

**Avalonia TrayIcon** ✅ **RECOMMENDED**
- ✅ Built into Avalonia 11.0.0 - no additional dependencies
- ✅ Cross-platform support (Windows, macOS, Linux)
- ✅ Native integration with platform system tray APIs
- ✅ Consistent with existing UI theme system

### Functional Programming Approach

**Pure Functional Implementation** 🎯 **CRITICAL**
- ✅ All state transformations through pure functions
- ✅ Immutable data structures for tray state
- ✅ Event-driven architecture with functional composition
- ✅ Railway-oriented programming for error handling
- ✅ No imperative patterns - maintain functional excellence established in Phases 1-5

## Functional Architecture Design

### Data Types Integration

```fsharp
// Extend UIApplicationState in ApplicationState.fs
type UIApplicationState = {
    // ... existing fields ...
    TrayState: TrayApplicationState option
}

and TrayApplicationState = {
    IsVisible: bool
    TrayIcon: obj option                    // Avalonia TrayIcon instance
    RecentPresets: string list             // Last 5 used presets for quick access
    QuickActions: TrayAction list          // Dynamic menu items
    NotificationSettings: TrayNotificationSettings
    LastTrayInteraction: DateTime
    MenuUpdateCount: int                   // For tracking menu refreshes
}

and TrayAction = {
    Label: string
    Action: TrayActionType
    IsEnabled: bool
    Shortcut: string option               // Display shortcut text (e.g., "Ctrl+1")
    Icon: string option                   // Optional icon name
    SortOrder: int                        // Menu ordering
}

and TrayActionType =
    | ShowMainWindow
    | ApplyPreset of presetName: string
    | RefreshDisplays
    | OpenSettings
    | ExitApplication
    | Separator                           // Menu separator

and TrayNotificationSettings = {
    ShowPresetNotifications: bool         // Show notifications when presets applied
    NotificationDuration: TimeSpan        // How long notifications stay visible
    Position: NotificationPosition        // Where notifications appear
    ShowSuccessOnly: bool                 // Only show successful operations
}

and NotificationPosition =
    | Default                             // System default
    | TopRight
    | TopLeft
    | BottomRight
    | BottomLeft
```

### Configuration Integration

```fsharp
// Extend ApplicationConfiguration.fs
type ApplicationConfiguration = {
    // ... existing fields ...
    TraySettings: TraySettings
}

and TraySettings = {
    EnableSystemTray: bool                // Master enable/disable
    StartMinimized: bool                  // Start application minimized to tray
    MinimizeToTray: bool                  // Minimize sends to tray instead of taskbar
    CloseToTray: bool                     // Close button sends to tray instead of exit
    ShowTrayNotifications: bool           // Enable tray notifications
    MaxRecentPresets: int                 // Number of recent presets in tray menu (default: 5)
    AutoHideMainWindow: bool              // Hide main window after preset selection
    TrayClickAction: TrayClickAction      // Action for single-click on tray icon
    DoubleClickAction: TrayClickAction    // Action for double-click on tray icon
}

and TrayClickAction =
    | ShowMainWindow                      // Restore main window
    | ShowTrayMenu                        // Show context menu
    | ApplyLastPreset                     // Apply most recently used preset
    | DoNothing                           // No action
```

### Event System Integration

```fsharp
// Extend UIEvent in UIEventSystem.fs
type UIEvent =
    | // ... existing events ...

    // Tray interaction events
    | TrayIconClicked
    | TrayIconDoubleClicked
    | TrayMenuItemSelected of action: TrayActionType
    | TrayMenuOpened
    | TrayMenuClosed

    // Window state events
    | WindowMinimizedToTray
    | WindowRestoredFromTray
    | WindowHiddenToTray
    | WindowShownFromTray

    // Notification events
    | TrayNotificationShown of message: string * duration: TimeSpan
    | TrayNotificationClicked
    | TrayNotificationTimedOut

    // Preset application from tray
    | TrayPresetApplied of presetName: string * success: bool
    | TrayPresetApplicationFailed of presetName: string * error: string
```

## Implementation Modules

### TrayManager Module

```fsharp
// UI/TrayManager.fs
module TrayManager =

    /// Pure functions for tray state management

    let createTrayState (recentPresets: string list) : TrayApplicationState = {
        IsVisible = true
        TrayIcon = None
        RecentPresets = recentPresets |> List.truncate 5
        QuickActions = []
        NotificationSettings = {
            ShowPresetNotifications = true
            NotificationDuration = TimeSpan.FromSeconds(3.0)
            Position = Default
            ShowSuccessOnly = false
        }
        LastTrayInteraction = DateTime.Now
        MenuUpdateCount = 0
    }

    let updateRecentPresets (presetName: string) (trayState: TrayApplicationState) : TrayApplicationState = {
        trayState with
            RecentPresets =
                presetName :: (trayState.RecentPresets |> List.filter ((<>) presetName))
                |> List.truncate 5
            LastTrayInteraction = DateTime.Now
    }

    let buildTrayMenu
        (presets: Map<string, EnhancedDisplayConfiguration>)
        (trayState: TrayApplicationState)
        (settings: TraySettings) : TrayAction list =

        let recentPresetActions =
            trayState.RecentPresets
            |> List.mapi (fun i presetName ->
                {
                    Label = sprintf "&%d. %s" (i + 1) presetName
                    Action = ApplyPreset presetName
                    IsEnabled = Map.containsKey presetName presets
                    Shortcut = Some (sprintf "Ctrl+%d" (i + 1))
                    Icon = Some "preset-icon"
                    SortOrder = i
                })

        let separatorAction = {
            Label = ""
            Action = Separator
            IsEnabled = true
            Shortcut = None
            Icon = None
            SortOrder = 100
        }

        let systemActions = [
            {
                Label = "&Show DisplaySwitch-Pro"
                Action = ShowMainWindow
                IsEnabled = true
                Shortcut = None
                Icon = Some "window-icon"
                SortOrder = 200
            }
            {
                Label = "&Refresh Displays"
                Action = RefreshDisplays
                IsEnabled = true
                Shortcut = Some "F5"
                Icon = Some "refresh-icon"
                SortOrder = 201
            }
            {
                Label = "&Settings..."
                Action = OpenSettings
                IsEnabled = true
                Shortcut = None
                Icon = Some "settings-icon"
                SortOrder = 202
            }
            {
                Label = "E&xit"
                Action = ExitApplication
                IsEnabled = true
                Shortcut = None
                Icon = Some "exit-icon"
                SortOrder = 300
            }
        ]

        recentPresetActions @ [separatorAction] @ systemActions
        |> List.sortBy (fun action -> action.SortOrder)

    /// Avalonia TrayIcon integration functions

    let createTrayIcon (onMenuItemClick: TrayActionType -> unit) (onTrayClick: unit -> unit) : Avalonia.Controls.TrayIcon =
        let trayIcon = new Avalonia.Controls.TrayIcon()
        trayIcon.Icon <- new Avalonia.Media.Imaging.Bitmap("Assets/app-icon.ico")
        trayIcon.ToolTipText <- "DisplaySwitch-Pro - Click for menu"

        // Handle tray icon clicks
        trayIcon.Clicked.Add(fun _ -> onTrayClick())

        trayIcon

    let updateTrayMenu (trayIcon: Avalonia.Controls.TrayIcon) (actions: TrayAction list) : unit =
        let menu = new Avalonia.Controls.NativeMenu()

        actions
        |> List.iter (fun action ->
            match action.Action with
            | Separator ->
                let separator = new Avalonia.Controls.NativeMenuSeparator()
                menu.Add(separator)
            | _ ->
                let menuItem = new Avalonia.Controls.NativeMenuItem()
                menuItem.Header <- action.Label
                menuItem.IsEnabled <- action.IsEnabled

                // Add click handler
                menuItem.Click.Add(fun _ ->
                    action.Action |> onMenuItemClick)

                menu.Add(menuItem)
        )

        trayIcon.Menu <- menu

    let showNotification
        (trayIcon: Avalonia.Controls.TrayIcon)
        (title: string)
        (message: string)
        (settings: TrayNotificationSettings) : unit =

        if settings.ShowPresetNotifications then
            // Use platform-specific notification system
            trayIcon.ShowNotification(title, message)

    let hideTrayIcon (trayIcon: Avalonia.Controls.TrayIcon) : unit =
        trayIcon.IsVisible <- false

    let showTrayIcon (trayIcon: Avalonia.Controls.TrayIcon) : unit =
        trayIcon.IsVisible <- true
```

### UIStateManager Integration

```fsharp
// Extend UIStateManager.fs processUIEventInternal function

| TrayIconClicked ->
    match model.UISettings.TraySettings.TrayClickAction with
    | ShowMainWindow ->
        { model with
            WindowState = { model.WindowState with IsVisible = true; IsMinimized = false }
            TrayState = model.TrayState |> Option.map (fun ts ->
                { ts with LastTrayInteraction = DateTime.Now }) }
    | ShowTrayMenu ->
        // Menu will be shown automatically by Avalonia
        model
    | ApplyLastPreset ->
        match model.TrayState |> Option.bind (fun ts -> ts.RecentPresets |> List.tryHead) with
        | Some presetName ->
            // Trigger preset application
            publishUIMessage (UIEvent (PresetApplied presetName))
            model
        | None ->
            model
    | DoNothing ->
        model

| TrayMenuItemSelected action ->
    match action with
    | ShowMainWindow ->
        { model with
            WindowState = { model.WindowState with IsVisible = true; IsMinimized = false } }

    | ApplyPreset presetName ->
        // Update recent presets and apply
        let updatedTrayState =
            model.TrayState
            |> Option.map (TrayManager.updateRecentPresets presetName)

        publishUIMessage (UIEvent (PresetApplied presetName))
        { model with TrayState = updatedTrayState }

    | RefreshDisplays ->
        publishUIMessage (UIEvent (DisplayDetectionRequested))
        model

    | OpenSettings ->
        publishUIMessage (UIEvent (SettingsRequested))
        model

    | ExitApplication ->
        publishUIMessage (UIEvent (ApplicationExitRequested))
        model

    | Separator ->
        model

| WindowMinimizedToTray ->
    { model with
        WindowState = { model.WindowState with IsVisible = false; IsMinimized = true }
        TrayState = model.TrayState |> Option.map (fun ts ->
            { ts with LastTrayInteraction = DateTime.Now }) }

| WindowRestoredFromTray ->
    { model with
        WindowState = { model.WindowState with IsVisible = true; IsMinimized = false } }

| TrayPresetApplied (presetName, success) ->
    let updatedTrayState =
        model.TrayState
        |> Option.map (TrayManager.updateRecentPresets presetName)

    // Show notification if enabled
    if model.UISettings.TraySettings.ShowTrayNotifications then
        let message =
            if success then sprintf "Applied preset: %s" presetName
            else sprintf "Failed to apply preset: %s" presetName
        publishUIMessage (UIEvent (TrayNotificationShown (message, TimeSpan.FromSeconds(3.0))))

    { model with TrayState = updatedTrayState }
```

## User Interface Integration

### Window Management

```fsharp
// Extend GUI.fs or ApplicationRunner.fs

module WindowTrayIntegration =

    let setupTrayIntegration (window: Window) (trayIcon: Avalonia.Controls.TrayIcon) (settings: TraySettings) =

        // Handle window state changes
        window.WindowState.Subscribe(fun state ->
            match state with
            | WindowState.Minimized when settings.MinimizeToTray ->
                UIEventSystem.UICoordinator.publishUIMessage (UIEvent WindowMinimizedToTray)
                window.Hide()
            | _ -> ()
        ) |> ignore

        // Handle window closing
        window.Closing.Add(fun e ->
            if settings.CloseToTray then
                e.Cancel <- true
                window.Hide()
                UIEventSystem.UICoordinator.publishUIMessage (UIEvent WindowHiddenToTray)
        )

        // Handle window show/hide
        window.IsVisibleChanged.Add(fun isVisible ->
            if isVisible then
                UIEventSystem.UICoordinator.publishUIMessage (UIEvent WindowShownFromTray)
        )

    let restoreWindowFromTray (window: Window) =
        window.Show()
        window.WindowState <- WindowState.Normal
        window.Activate()
        window.Focus()
```

## Implementation Approach

### Sub-Agent Implementation Guidelines

**Build-First Methodology** 🎯 **CRITICAL**
- ✅ Sub-agent implements complete functional system tray functionality
- ✅ Focus on achieving clean `dotnet build` with 0 warnings, 0 errors
- ✅ NEVER use `dotnet run` - only `dotnet build` for verification
- ✅ Manual testing will be performed by user after successful build
- ✅ Maintain 100% backward compatibility throughout implementation

**Functional Programming Standards** 🎯 **CRITICAL**
- ✅ Use pure functions for all state transformations
- ✅ Implement immutable data structures only
- ✅ Leverage existing event-driven architecture from Phase 2
- ✅ Maintain railway-oriented programming patterns from Phase 3
- ✅ No imperative code - continue functional excellence from Phases 1-5

### Implementation Timeline

### Phase 1: Core Tray Infrastructure (Day 1)
- ✅ Create TrayManager module with pure functions only
- ✅ Extend ApplicationConfiguration with TraySettings
- ✅ Add tray events to UIEventSystem
- ✅ Basic TrayIcon creation and menu building using functional patterns

### Phase 2: State Management Integration (Day 2)
- ✅ Extend ApplicationState with TrayApplicationState
- ✅ Update UIStateManager for tray event processing
- ✅ Implement recent presets tracking with pure functions
- ✅ Add window state management integration

### Phase 3: User Interface Integration (Day 3)
- ✅ Integrate TrayIcon with main window lifecycle
- ✅ Handle minimize/close to tray behavior functionally
- ✅ Implement tray menu updates based on presets
- ✅ Add settings UI for tray configuration

### Phase 4: Build Verification & Integration (Day 4)
- ✅ Ensure clean build with all new functionality
- ✅ Verify all integration points work correctly
- ✅ Validate functional programming patterns maintained
- ✅ Prepare for manual testing by user

## User Experience Flow

### Startup Behavior
1. **Normal Start**: Application opens main window
2. **Start Minimized**: Application starts hidden in tray (if configured)
3. **Tray Icon**: Always visible when application is running

### Tray Interaction
1. **Single Click**: Configurable action (show window, show menu, apply last preset)
2. **Double Click**: Configurable action (typically show main window)
3. **Right Click**: Always shows context menu
4. **Menu Selection**: Immediate action execution

### Window Management
1. **Minimize**: Goes to taskbar or tray (based on settings)
2. **Close**: Exits application or hides to tray (based on settings)
3. **Restore**: Double-click tray icon or menu selection

### Preset Management
1. **Quick Access**: Recent 5 presets in tray menu with keyboard shortcuts
2. **Application**: Click preset in tray menu applies immediately
3. **Feedback**: Optional notifications show application status
4. **History**: Recent presets list updates automatically

## Testing Strategy

### Functional Testing
- ✅ Tray icon visibility across platforms
- ✅ Menu item functionality and state
- ✅ Window minimize/restore behavior
- ✅ Preset application from tray
- ✅ Settings persistence and hot reload

### Integration Testing
- ✅ Event system integration
- ✅ State management consistency
- ✅ Configuration system integration
- ✅ Preset metadata integration

### Cross-Platform Testing
- ✅ Windows system tray behavior
- ✅ macOS menu bar integration
- ✅ Linux system tray compatibility
- ✅ Platform-specific notification systems

### Performance Testing
- ✅ Memory usage with tray icon
- ✅ Menu update performance
- ✅ Event processing efficiency
- ✅ Resource cleanup on exit

## Configuration Examples

### Default Tray Settings
```fsharp
let defaultTraySettings = {
    EnableSystemTray = true
    StartMinimized = false
    MinimizeToTray = true
    CloseToTray = true
    ShowTrayNotifications = true
    MaxRecentPresets = 5
    AutoHideMainWindow = false
    TrayClickAction = ShowMainWindow
    DoubleClickAction = ShowMainWindow
}
```

### User Customization Options
- Enable/disable system tray functionality
- Configure minimize and close behavior
- Customize tray click actions
- Adjust notification settings
- Set number of recent presets displayed
- Configure automatic window hiding

## Risk Assessment

### Low Risk ✅
- **Avalonia TrayIcon Stability**: Mature, well-tested component
- **Cross-Platform Support**: Built into Avalonia framework
- **Integration Complexity**: Fits well with existing architecture
- **User Experience**: Familiar system tray patterns

### Medium Risk ⚠️
- **Platform Differences**: Slight behavior variations across platforms
- **Resource Management**: Proper cleanup of tray resources on exit
- **State Synchronization**: Keeping tray menu in sync with application state

### Mitigation Strategies
- Comprehensive cross-platform testing
- Proper resource disposal patterns
- Event-driven state updates for consistency
- Graceful degradation if tray not available

## Success Criteria

### Functional Requirements ✅
- System tray icon visible when application running
- Context menu with recent presets and system actions
- Minimize/close to tray functionality working
- Preset application from tray menu
- Configuration options for tray behavior

### Performance Requirements ✅
- Tray icon responsive (<100ms click response)
- Menu updates efficient (<50ms)
- Minimal memory overhead (<5MB for tray functionality)
- Clean resource management (no leaks)

### User Experience Requirements ✅
- Intuitive tray interaction patterns
- Clear visual feedback for actions
- Consistent behavior across platforms
- Accessible keyboard shortcuts in menu

The system tray implementation will provide excellent user value while maintaining the high functional programming standards established in DisplaySwitch-Pro's architecture transformation.