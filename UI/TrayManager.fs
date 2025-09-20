namespace DisplaySwitchPro

open System
open Avalonia.Controls

/// Pure functional system tray management module
/// Provides system tray functionality with functional programming patterns
module TrayManager =

    // ===== Core Tray Types =====

    /// Tray action types for menu items
    type TrayActionType =
        | ShowMainWindow
        | ApplyPreset of presetName: string
        | RefreshDisplays
        | OpenSettings
        | ExitApplication
        | Separator                           // Menu separator

    /// Tray action definition
    type TrayAction = {
        Label: string
        Action: TrayActionType
        IsEnabled: bool
        Shortcut: string option               // Display shortcut text (e.g., "Ctrl+1")
        Icon: string option                   // Optional icon name
        SortOrder: int                        // Menu ordering
    }

    /// Tray notification settings
    type TrayNotificationSettings = {
        ShowPresetNotifications: bool         // Show notifications when presets applied
        NotificationDuration: TimeSpan        // How long notifications stay visible
        ShowSuccessOnly: bool                 // Only show successful operations
    }

    /// System tray application state
    type TrayApplicationState = {
        IsVisible: bool
        TrayIcon: TrayIcon option             // Avalonia TrayIcon instance
        RecentPresets: string list            // Last 5 used presets for quick access
        QuickActions: TrayAction list         // Dynamic menu items
        NotificationSettings: TrayNotificationSettings
        LastTrayInteraction: DateTime
        MenuUpdateCount: int                  // For tracking menu refreshes
    }

    /// Tray settings configuration
    type TraySettings = {
        EnableSystemTray: bool                // Master enable/disable
        StartMinimized: bool                  // Start application minimized to tray
        MinimizeToTray: bool                  // Minimize sends to tray instead of taskbar
        CloseToTray: bool                     // Close button sends to tray instead of exit
        ShowTrayNotifications: bool           // Enable tray notifications
        MaxRecentPresets: int                 // Number of recent presets in tray menu (default: 5)
        AutoHideMainWindow: bool              // Hide main window after preset selection
        TrayClickAction: TrayActionType       // Action for single-click on tray icon
        DoubleClickAction: TrayActionType     // Action for double-click on tray icon
    }

    // ===== Default Values =====

    /// Default tray notification settings
    let defaultNotificationSettings = {
        ShowPresetNotifications = true
        NotificationDuration = TimeSpan.FromSeconds(3.0)
        ShowSuccessOnly = false
    }

    /// Default tray settings
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

    /// Create initial tray state
    let createTrayState (recentPresets: string list) : TrayApplicationState = {
        IsVisible = true
        TrayIcon = None
        RecentPresets = recentPresets |> List.truncate 5
        QuickActions = []
        NotificationSettings = defaultNotificationSettings
        LastTrayInteraction = DateTime.Now
        MenuUpdateCount = 0
    }

    // ===== Pure Functional State Management =====

    /// Update recent presets with functional approach
    let updateRecentPresets (presetName: string) (trayState: TrayApplicationState) : TrayApplicationState =
        { trayState with
            RecentPresets =
                presetName :: (trayState.RecentPresets |> List.filter ((<>) presetName))
                |> List.truncate 5
            LastTrayInteraction = DateTime.Now }

    /// Update tray visibility
    let updateTrayVisibility (isVisible: bool) (trayState: TrayApplicationState) : TrayApplicationState =
        { trayState with
            IsVisible = isVisible
            LastTrayInteraction = DateTime.Now }

    /// Update tray icon reference
    let updateTrayIcon (trayIcon: TrayIcon option) (trayState: TrayApplicationState) : TrayApplicationState =
        { trayState with
            TrayIcon = trayIcon
            LastTrayInteraction = DateTime.Now }

    /// Update notification settings
    let updateNotificationSettings (settings: TrayNotificationSettings) (trayState: TrayApplicationState) : TrayApplicationState =
        { trayState with
            NotificationSettings = settings
            LastTrayInteraction = DateTime.Now }

    // ===== Menu Building Functions =====

    /// Build tray menu actions based on current state
    let buildTrayMenu
        (presets: Map<string, DisplayConfiguration>)
        (trayState: TrayApplicationState)
        (settings: TraySettings) : TrayAction list =

        let recentPresetActions =
            trayState.RecentPresets
            |> List.mapi (fun i presetName ->
                {
                    Label = sprintf "%d. %s" (i + 1) presetName
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
                Label = "Show DisplaySwitch-Pro"
                Action = ShowMainWindow
                IsEnabled = true
                Shortcut = None
                Icon = Some "window-icon"
                SortOrder = 200
            }
            {
                Label = "Refresh Displays"
                Action = RefreshDisplays
                IsEnabled = true
                Shortcut = Some "F5"
                Icon = Some "refresh-icon"
                SortOrder = 201
            }
            {
                Label = "Settings..."
                Action = OpenSettings
                IsEnabled = true
                Shortcut = None
                Icon = Some "settings-icon"
                SortOrder = 202
            }
            {
                Label = "Exit"
                Action = ExitApplication
                IsEnabled = true
                Shortcut = None
                Icon = Some "exit-icon"
                SortOrder = 300
            }
        ]

        recentPresetActions @ [separatorAction] @ systemActions
        |> List.sortBy (fun action -> action.SortOrder)

    /// Update menu update count
    let incrementMenuUpdateCount (trayState: TrayApplicationState) : TrayApplicationState =
        { trayState with
            MenuUpdateCount = trayState.MenuUpdateCount + 1
            LastTrayInteraction = DateTime.Now }

    // ===== Avalonia TrayIcon Integration Functions =====

    /// Create Avalonia TrayIcon with event handlers
    let createTrayIcon
        (onMenuItemClick: TrayActionType -> unit)
        (onTrayClick: unit -> unit)
        (onTrayDoubleClick: unit -> unit) : TrayIcon =
        let trayIcon = new TrayIcon()

        // Set icon properties
        trayIcon.ToolTipText <- "DisplaySwitch-Pro - Click for menu"

        // TODO: Set icon from embedded resource
        // trayIcon.Icon <- new Avalonia.Media.Imaging.Bitmap("Assets/app-icon.ico")

        // Handle tray icon clicks
        trayIcon.Clicked.Add(fun _ -> onTrayClick())

        // Note: Avalonia TrayIcon doesn't have a built-in double-click event
        // We would need to implement double-click detection ourselves if needed

        trayIcon

    /// Update tray menu with actions
    let updateTrayMenu (trayIcon: TrayIcon) (actions: TrayAction list) (onMenuItemClick: TrayActionType -> unit) : unit =
        try
            let menu = new NativeMenu()

            actions
            |> List.iter (fun action ->
                match action.Action with
                | Separator ->
                    let separator = new NativeMenuItemSeparator()
                    menu.Add(separator)
                | _ ->
                    let menuItem = new NativeMenuItem()
                    menuItem.Header <- action.Label
                    menuItem.IsEnabled <- action.IsEnabled

                    // Add click handler
                    menuItem.Click.Add(fun _ ->
                        try
                            onMenuItemClick action.Action
                        with ex ->
                            Logging.logError (sprintf "Tray menu item click failed: %s" ex.Message))

                    menu.Add(menuItem)
            )

            trayIcon.Menu <- menu
            Logging.logVerbose "Tray menu updated successfully"
        with ex ->
            Logging.logError (sprintf "Failed to update tray menu: %s" ex.Message)

    /// Show notification via tray icon
    let showNotification
        (trayIcon: TrayIcon)
        (title: string)
        (message: string)
        (settings: TrayNotificationSettings) : unit =
        try
            if settings.ShowPresetNotifications then
                // Fallback: Use tooltip for notification
                // TODO: Implement proper notification system when available
                trayIcon.ToolTipText <- sprintf "%s: %s" title message
                Logging.logVerbose (sprintf "Tray notification shown: %s - %s" title message)
        with ex ->
            Logging.logError (sprintf "Failed to show tray notification: %s" ex.Message)

    /// Hide tray icon
    let hideTrayIcon (trayIcon: TrayIcon) : unit =
        try
            trayIcon.IsVisible <- false
            Logging.logVerbose "Tray icon hidden"
        with ex ->
            Logging.logError (sprintf "Failed to hide tray icon: %s" ex.Message)

    /// Show tray icon
    let showTrayIcon (trayIcon: TrayIcon) : unit =
        try
            trayIcon.IsVisible <- true
            Logging.logVerbose "Tray icon shown"
        with ex ->
            Logging.logError (sprintf "Failed to show tray icon: %s" ex.Message)

    /// Dispose tray icon resources
    let disposeTrayIcon (trayIcon: TrayIcon) : unit =
        try
            trayIcon.Dispose()
            Logging.logVerbose "Tray icon disposed"
        with ex ->
            Logging.logError (sprintf "Failed to dispose tray icon: %s" ex.Message)

    // ===== Integration Helper Functions =====

    /// Handle tray action with logging
    let handleTrayAction (action: TrayActionType) : unit =
        try
            Logging.logVerbose (sprintf "Handling tray action: %A" action)

            match action with
            | ShowMainWindow ->
                UIEventSystem.UICoordinator.refreshMainWindow()

            | ApplyPreset presetName ->
                UIEventSystem.UICoordinator.notifyPresetApplied presetName

            | RefreshDisplays ->
                UIEventSystem.UICoordinator.publishUIMessage (UIEventSystem.UIEvent UIEventSystem.DisplayDetectionRequested)

            | OpenSettings ->
                // TODO: Implement settings dialog
                Logging.logNormal "Settings dialog not yet implemented"

            | ExitApplication ->
                // TODO: Implement graceful application exit
                Logging.logNormal "Application exit requested from tray"

            | Separator ->
                () // No action for separator

        with ex ->
            Logging.logError (sprintf "Tray action handling failed: %s" ex.Message)

    /// Create complete tray management system
    let createTrayManagement (settings: TraySettings) : TrayApplicationState * TrayIcon =
        try
            let initialState = createTrayState []

            let trayIcon =
                createTrayIcon
                    handleTrayAction
                    (fun () -> handleTrayAction settings.TrayClickAction)
                    (fun () -> handleTrayAction settings.DoubleClickAction)

            let updatedState = updateTrayIcon (Some trayIcon) initialState

            if settings.EnableSystemTray then
                showTrayIcon trayIcon
            else
                hideTrayIcon trayIcon

            Logging.logNormal "Tray management system created successfully"
            (updatedState, trayIcon)

        with ex ->
            Logging.logError (sprintf "Failed to create tray management system: %s" ex.Message)
            let fallbackState = createTrayState []
            // Return a dummy tray icon that won't cause issues
            let dummyTrayIcon = new TrayIcon()
            (fallbackState, dummyTrayIcon)

    // ===== Validation Functions =====

    /// Validate tray settings
    let validateTraySettings (settings: TraySettings) : Result<unit, string list> =
        let errors = [
            if settings.MaxRecentPresets < 1 || settings.MaxRecentPresets > 10 then
                yield "MaxRecentPresets must be between 1 and 10"
        ]

        if List.isEmpty errors then Ok ()
        else Error errors

    /// Validate tray state
    let validateTrayState (trayState: TrayApplicationState) : Result<unit, string list> =
        let errors = [
            if trayState.RecentPresets.Length > 10 then
                yield "Too many recent presets"
            if trayState.MenuUpdateCount < 0 then
                yield "Invalid menu update count"
        ]

        if List.isEmpty errors then Ok ()
        else Error errors