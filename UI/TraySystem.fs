namespace DisplaySwitchPro

open System
open System.Runtime.InteropServices
open Avalonia.Controls
open Avalonia.Platform
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading

/// Pure functional system tray module
/// Provides system tray functionality with functional programming patterns
module TraySystem =

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

        // Create preset actions for all available presets
        let presetActions =
            presets
            |> Map.toList
            |> List.sortBy fst  // Sort presets alphabetically
            |> List.mapi (fun i (presetName, _) ->
                {
                    Label = presetName
                    Action = ApplyPreset presetName
                    IsEnabled = true
                    Shortcut = None  // Remove placeholder shortcuts - not implemented
                    Icon = Some "preset-icon"
                    SortOrder = i + 10  // Start from 10 to leave space for priority items
                })

        // Add preset header if presets exist
        let presetHeader =
            if not (List.isEmpty presetActions) then
                [{
                    Label = "Display Presets"
                    Action = Separator
                    IsEnabled = false
                    Shortcut = None
                    Icon = None
                    SortOrder = 5
                }]
            else
                []

        // If no presets available, show a disabled placeholder
        let presetActionsWithFallback =
            if List.isEmpty presetActions then
                [{
                    Label = "No presets saved"
                    Action = Separator
                    IsEnabled = false
                    Shortcut = None
                    Icon = None
                    SortOrder = 10
                }]
            else
                presetHeader @ presetActions

        let mainSeparator = {
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
                Shortcut = None  // Remove placeholder - F5 not implemented
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
        ]

        let exitSeparator = {
            Label = ""
            Action = Separator
            IsEnabled = true
            Shortcut = None
            Icon = None
            SortOrder = 290
        }

        let exitAction = {
            Label = "Exit"
            Action = ExitApplication
            IsEnabled = true
            Shortcut = None  // Alt+F4 is system default, not app-specific
            Icon = Some "exit-icon"
            SortOrder = 300
        }

        presetActionsWithFallback @ [mainSeparator] @ systemActions @ [exitSeparator; exitAction]
        |> List.sortBy (fun action -> action.SortOrder)

    /// Update menu update count
    let incrementMenuUpdateCount (trayState: TrayApplicationState) : TrayApplicationState =
        { trayState with
            MenuUpdateCount = trayState.MenuUpdateCount + 1
            LastTrayInteraction = DateTime.Now }

    // ===== Platform Detection =====

    /// Check if we're running on Windows (same pattern as PlatformAdapter.fs)
    let isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT

    // ===== Windows System Tray Structures =====

    [<Struct>]
    type POINT = { X: int; Y: int }

    [<Struct>]
    type NOTIFYICONDATA = {
        mutable cbSize: uint32
        mutable hWnd: IntPtr
        mutable uID: uint32
        mutable uFlags: uint32
        mutable uCallbackMessage: uint32
        mutable hIcon: IntPtr
        mutable szTip: string
        mutable dwState: uint32
        mutable dwStateMask: uint32
        mutable szInfo: string
        mutable uVersion: uint32
        mutable szInfoTitle: string
        mutable dwInfoFlags: uint32
        mutable guidItem: System.Guid
        mutable hBalloonIcon: IntPtr
    }

    // ===== Windows System Tray P/Invoke =====

    [<DllImport("shell32.dll", SetLastError = true)>]
    extern bool Shell_NotifyIcon(uint32 dwMessage, NOTIFYICONDATA& lpData)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern IntPtr CreatePopupMenu()

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool AppendMenu(IntPtr hMenu, uint32 uFlags, UIntPtr uIDNewItem, string lpNewItem)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool DestroyMenu(IntPtr hMenu)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern int TrackPopupMenu(IntPtr hMenu, uint32 uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool GetCursorPos(POINT& lpPoint)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern IntPtr GetForegroundWindow()

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool SetForegroundWindow(IntPtr hWnd)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool PostMessage(IntPtr hWnd, uint32 msg, UIntPtr wParam, IntPtr lParam)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern uint32 RegisterWindowMessage(string lpString)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern IntPtr DefWindowProc(IntPtr hWnd, uint32 msg, UIntPtr wParam, IntPtr lParam)

    // Window subclassing for message handling
    type SUBCLASSPROC = delegate of IntPtr * uint32 * UIntPtr * IntPtr * UIntPtr * UIntPtr -> IntPtr

    [<DllImport("comctl32.dll", SetLastError = true)>]
    extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData)

    [<DllImport("comctl32.dll", SetLastError = true)>]
    extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass)

    [<DllImport("comctl32.dll", SetLastError = true)>]
    extern IntPtr DefSubclassProc(IntPtr hWnd, uint32 uMsg, UIntPtr wParam, IntPtr lParam)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern IntPtr GetModuleHandle(string lpModuleName)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern IntPtr LoadImage(IntPtr hInst, string name, uint32 ``type``, int cx, int cy, uint32 fuLoad)

    [<DllImport("gdi32.dll", SetLastError = true)>]
    extern bool DeleteObject(IntPtr hObject)

    [<DllImport("gdi32.dll", SetLastError = true)>]
    extern IntPtr CreateCompatibleDC(IntPtr hdc)

    [<DllImport("gdi32.dll", SetLastError = true)>]
    extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern IntPtr CreateIconFromResourceEx(IntPtr presbits, uint32 dwResSize, bool fIcon, uint32 dwVer, int cxDesired, int cyDesired, uint32 flags)

    // Icon loading constants
    [<Literal>]
    let IMAGE_ICON = 1u
    [<Literal>]
    let LR_LOADFROMFILE = 0x00000010u
    [<Literal>]
    let LR_DEFAULTSIZE = 0x00000040u

    // Windows system tray constants
    [<Literal>]
    let NIM_ADD = 0x00000000u
    [<Literal>]
    let NIM_MODIFY = 0x00000001u
    [<Literal>]
    let NIM_DELETE = 0x00000002u
    [<Literal>]
    let NIM_SETFOCUS = 0x00000003u
    [<Literal>]
    let NIM_SETVERSION = 0x00000004u

    [<Literal>]
    let NIF_MESSAGE = 0x00000001u
    [<Literal>]
    let NIF_ICON = 0x00000002u
    [<Literal>]
    let NIF_TIP = 0x00000004u
    [<Literal>]
    let NIF_STATE = 0x00000008u
    [<Literal>]
    let NIF_INFO = 0x00000010u
    [<Literal>]
    let NIF_GUID = 0x00000020u

    [<Literal>]
    let NOTIFYICON_VERSION_4 = 0x4u

    [<Literal>]
    let WM_CONTEXTMENU = 0x007Bu
    [<Literal>]
    let WM_USER = 0x0400u

    // Windows menu constants
    [<Literal>]
    let MF_STRING = 0x00000000u
    [<Literal>]
    let MF_SEPARATOR = 0x00000800u
    [<Literal>]
    let MF_GRAYED = 0x00000001u
    [<Literal>]
    let MF_DISABLED = 0x00000002u
    [<Literal>]
    let TPM_RIGHTBUTTON = 0x0002u
    [<Literal>]
    let TPM_RETURNCMD = 0x0100u
    [<Literal>]
    let WM_NULL = 0x0000u

    // Custom message for tray callbacks
    let WM_TRAYICON = WM_USER + 1u

    // Mouse message constants
    [<Literal>]
    let WM_LBUTTONUP = 0x0202
    [<Literal>]
    let WM_RBUTTONUP = 0x0205
    [<Literal>]
    let WM_LBUTTONDBLCLK = 0x0203

    // ===== Windows Native System Tray Functions =====

    /// Create Windows system tray icon using Shell_NotifyIcon
    let createWindowsSystemTrayIcon (hwnd: IntPtr) (iconPath: string) : Result<unit, string> =
        try
            let mutable nid = {
                cbSize = uint32 (Marshal.SizeOf<NOTIFYICONDATA>())
                hWnd = hwnd
                uID = 1u
                uFlags = NIF_MESSAGE ||| NIF_ICON ||| NIF_TIP
                uCallbackMessage = WM_TRAYICON
                hIcon = IntPtr.Zero  // We'll load this from the icon file
                szTip = "DisplaySwitch-Pro"
                dwState = 0u
                dwStateMask = 0u
                szInfo = ""
                uVersion = 0u
                szInfoTitle = ""
                dwInfoFlags = 0u
                guidItem = System.Guid.NewGuid()
                hBalloonIcon = IntPtr.Zero
            }

            // Load application icon from ICO file
            try
                let mutable hIcon = IntPtr.Zero

                try
                    // Try to load ICO file from Assets using file path
                    let assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location
                    let assemblyDir = System.IO.Path.GetDirectoryName(assemblyLocation)
                    let iconPath = System.IO.Path.Combine(assemblyDir, "Assets", "app-icon-new.ico")

                    if System.IO.File.Exists(iconPath) then
                        // Load ICO file directly using LoadImage
                        hIcon <- LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE ||| LR_DEFAULTSIZE)
                        if hIcon <> IntPtr.Zero then
                            nid.hIcon <- hIcon
                            Logging.logVerbose (sprintf "Loaded custom tray icon from: %s" iconPath)
                        else
                            Logging.logVerbose "Failed to load ICO file, trying executable resources"
                            // Try to extract icon from executable as fallback
                            let hInstance = GetModuleHandle(null)
                            hIcon <- LoadIcon(hInstance, IntPtr(1)) // Try first icon resource
                            if hIcon <> IntPtr.Zero then
                                nid.hIcon <- hIcon
                                Logging.logVerbose "Loaded icon from executable resources"
                            else
                                // Final fallback to system icon
                                hIcon <- LoadIcon(hInstance, IntPtr(32512)) // IDI_APPLICATION
                                if hIcon <> IntPtr.Zero then
                                    nid.hIcon <- hIcon
                                    Logging.logVerbose "Using default system application icon for tray"
                    else
                        Logging.logVerbose (sprintf "ICO file not found at: %s, using fallback" iconPath)
                        // Try executable resources
                        let hInstance = GetModuleHandle(null)
                        hIcon <- LoadIcon(hInstance, IntPtr(1)) // Try first icon resource
                        if hIcon <> IntPtr.Zero then
                            nid.hIcon <- hIcon
                            Logging.logVerbose "Loaded icon from executable resources"
                        else
                            // Final fallback to system icon
                            hIcon <- LoadIcon(hInstance, IntPtr(32512)) // IDI_APPLICATION
                            if hIcon <> IntPtr.Zero then
                                nid.hIcon <- hIcon
                                Logging.logVerbose "Using default system application icon for tray"
                with ex ->
                    Logging.logVerbose (sprintf "Icon loading failed: %s, using default application icon" ex.Message)
                    // Fallback to default application icon
                    let hInstance = GetModuleHandle(null)
                    hIcon <- LoadIcon(hInstance, IntPtr(32512)) // IDI_APPLICATION
                    if hIcon <> IntPtr.Zero then
                        nid.hIcon <- hIcon

                if hIcon = IntPtr.Zero then
                    // If we can't load any icon, continue without one
                    nid.uFlags <- NIF_MESSAGE ||| NIF_TIP // Remove NIF_ICON flag
                    Logging.logVerbose "No icon loaded, tray will use system default"
            with ex ->
                // If icon loading fails, continue without icon
                nid.uFlags <- NIF_MESSAGE ||| NIF_TIP
                Logging.logVerbose (sprintf "Icon loading failed: %s" ex.Message)

            let success = Shell_NotifyIcon(NIM_ADD, &nid)
            if success then
                // Set version to NOTIFYICON_VERSION_4 for proper behavior
                nid.uVersion <- NOTIFYICON_VERSION_4
                let versionSuccess = Shell_NotifyIcon(NIM_SETVERSION, &nid)
                if versionSuccess then
                    Ok ()
                else
                    Error "Failed to set tray icon version"
            else
                Error "Failed to add tray icon"
        with ex ->
            Error (sprintf "Exception creating Windows system tray icon: %s" ex.Message)

    /// Remove Windows system tray icon
    let removeWindowsSystemTrayIcon (hwnd: IntPtr) : Result<unit, string> =
        try
            let mutable nid = {
                cbSize = uint32 (Marshal.SizeOf<NOTIFYICONDATA>())
                hWnd = hwnd
                uID = 1u
                uFlags = 0u
                uCallbackMessage = 0u
                hIcon = IntPtr.Zero
                szTip = ""
                dwState = 0u
                dwStateMask = 0u
                szInfo = ""
                uVersion = 0u
                szInfoTitle = ""
                dwInfoFlags = 0u
                guidItem = System.Guid.Empty
                hBalloonIcon = IntPtr.Zero
            }

            let success = Shell_NotifyIcon(NIM_DELETE, &nid)
            if success then
                Ok ()
            else
                Error "Failed to remove tray icon"
        with ex ->
            Error (sprintf "Exception removing Windows system tray icon: %s" ex.Message)

    /// Handle Windows tray icon message (WM_TRAYICON callback)
    let handleWindowsTrayMessage (wParam: UIntPtr) (lParam: IntPtr) (actions: TrayAction list) (onMenuItemClick: TrayActionType -> unit) : unit =
        let iconId = uint32 wParam
        let message = uint32 (lParam.ToInt32() &&& 0xFFFF)

        match message with
        | m when m = WM_CONTEXTMENU ->
            // Right-click - show context menu at cursor position
            try
                let mainWindow = GetForegroundWindow()
                // We'll implement this after the menu functions are defined
                Logging.logVerbose "Windows tray right-click detected - would show context menu"
            with ex ->
                Logging.logError (sprintf "Exception in tray context menu: %s" ex.Message)
        | 0x0201u -> // WM_LBUTTONDOWN
            // Left-click - show main window
            onMenuItemClick ShowMainWindow
        | 0x0203u -> // WM_LBUTTONDBLCLK
            // Double-click - show main window
            onMenuItemClick ShowMainWindow
        | _ ->
            // Other messages - ignore
            ()

    // ===== Windows Native Menu Functions =====

    /// Create Windows native popup menu from TrayActions
    let createWindowsNativeMenu (actions: TrayAction list) : Result<IntPtr * Map<uint32, TrayActionType>, string> =
        try
            let menuHandle = CreatePopupMenu()
            if menuHandle = IntPtr.Zero then
                Error "Failed to create popup menu"
            else
                let mutable idCounter = 1000u
                let mutable idActionMap = Map.empty

                let success =
                    actions
                    |> List.fold (fun acc action ->
                        if not acc then acc
                        else
                            match action.Action with
                            | Separator ->
                                AppendMenu(menuHandle, MF_SEPARATOR, UIntPtr.Zero, null)
                            | _ ->
                                let flags =
                                    if action.IsEnabled then MF_STRING
                                    else MF_STRING ||| MF_GRAYED ||| MF_DISABLED

                                // Combine label with shortcut if available
                                let displayText =
                                    match action.Shortcut with
                                    | Some shortcut -> sprintf "%s\t%s" action.Label shortcut
                                    | None -> action.Label

                                let result = AppendMenu(menuHandle, flags, UIntPtr idCounter, displayText)
                                if result then
                                    idActionMap <- Map.add idCounter action.Action idActionMap
                                    idCounter <- idCounter + 1u
                                result
                    ) true

                if success then
                    Ok (menuHandle, idActionMap)
                else
                    DestroyMenu(menuHandle) |> ignore
                    Error "Failed to populate menu items"
        with ex ->
            Error (sprintf "Exception creating Windows menu: %s" ex.Message)

    /// Show Windows native menu at cursor position (proper tray positioning)
    let showWindowsNativeMenu (menuHandle: IntPtr) (mainWindow: IntPtr) : Result<uint32, string> =
        try
            // Get cursor position (Windows moves cursor to tray icon automatically)
            let mutable cursorPos = { X = 0; Y = 0 }
            let success = GetCursorPos(&cursorPos)
            if not success then
                Error "Failed to get cursor position"
            else
                // Essential: Set foreground window before showing menu
                SetForegroundWindow(mainWindow) |> ignore

                // Show menu at cursor position using proper flags
                let selectedId = TrackPopupMenu(
                    menuHandle,
                    TPM_RIGHTBUTTON ||| TPM_RETURNCMD,
                    cursorPos.X,
                    cursorPos.Y,
                    0,
                    mainWindow,
                    IntPtr.Zero
                )

                // Post null message to help with menu cleanup (recommended practice)
                PostMessage(mainWindow, WM_NULL, UIntPtr.Zero, IntPtr.Zero) |> ignore

                Ok (uint32 selectedId)
        with ex ->
            Error (sprintf "Exception showing Windows menu: %s" ex.Message)

    /// Handle Windows menu selection and execute action
    let handleWindowsMenuSelection
        (selectedId: uint32)
        (idActionMap: Map<uint32, TrayActionType>)
        (onMenuItemClick: TrayActionType -> unit) : unit =
        if selectedId > 0u then
            idActionMap
            |> Map.tryFind selectedId
            |> Option.iter onMenuItemClick

    /// Show Windows native context menu (complete pipeline)
    let showWindowsContextMenu
        (actions: TrayAction list)
        (onMenuItemClick: TrayActionType -> unit)
        (mainWindow: IntPtr) : Result<unit, string> =
        match createWindowsNativeMenu actions with
        | Error err -> Error err
        | Ok (menuHandle, idActionMap) ->
            try
                match showWindowsNativeMenu menuHandle mainWindow with
                | Error err ->
                    DestroyMenu(menuHandle) |> ignore
                    Error err
                | Ok selectedId ->
                    handleWindowsMenuSelection selectedId idActionMap onMenuItemClick
                    DestroyMenu(menuHandle) |> ignore
                    Ok ()
            with ex ->
                DestroyMenu(menuHandle) |> ignore
                Error (sprintf "Exception in Windows context menu pipeline: %s" ex.Message)

    // ===== Avalonia TrayIcon Integration Functions =====

    /// Create Avalonia TrayIcon with event handlers
    let createTrayIcon
        (onMenuItemClick: TrayActionType -> unit)
        (onTrayClick: unit -> unit)
        (onTrayDoubleClick: unit -> unit) : TrayIcon =
        let trayIcon = new TrayIcon()

        // Set icon properties
        trayIcon.ToolTipText <- "DisplaySwitch-Pro - Click for menu"

        // Set icon from embedded resource (use new improved icon)
        try
            let iconUri = new System.Uri("avares://DisplaySwitch-Pro/Assets/app-icon-new.png")
            let bitmap = new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(iconUri))
            trayIcon.Icon <- new Avalonia.Controls.WindowIcon(bitmap)
            Logging.logVerbose "Tray icon loaded successfully with improved icon"
        with ex ->
            Logging.logError (sprintf "Failed to load tray icon: %s" ex.Message)

        // Handle tray icon clicks
        trayIcon.Clicked.Add(fun _ -> onTrayClick())

        // Note: Avalonia TrayIcon doesn't distinguish between left/right clicks or double-clicks
        // All click handling is done through the onTrayClick callback

        trayIcon

    /// Update tray menu with actions (Avalonia NativeMenu - fallback for non-Windows)
    let updateAvaloniaMenu (trayIcon: TrayIcon) (actions: TrayAction list) (onMenuItemClick: TrayActionType -> unit) : unit =
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
            Logging.logVerbose "Avalonia tray menu updated successfully"
        with ex ->
            Logging.logError (sprintf "Failed to update Avalonia tray menu: %s" ex.Message)

    /// Create Windows native menu items that bypass Avalonia positioning
    let createWindowsNativeMenuItems (actions: TrayAction list) (onMenuItemClick: TrayActionType -> unit) (trayIcon: TrayIcon) : unit =
        try
            let menu = new NativeMenu()

            // Create native menu items that directly call our Windows native menu functions
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

                    // Instead of showing Avalonia menu, immediately call the action
                    menuItem.Click.Add(fun _ ->
                        try
                            onMenuItemClick action.Action
                        with ex ->
                            Logging.logError (sprintf "Menu item click failed: %s" ex.Message))

                    menu.Add(menuItem)
            )

            trayIcon.Menu <- menu
            Logging.logVerbose "Set up Windows-style native menu items"
        with ex ->
            Logging.logError (sprintf "Failed to create Windows native menu items: %s" ex.Message)

    /// Get Avalonia window handle for native Windows operations
    let getWindowHandle (window: Window) : IntPtr option =
        try
            match window.TryGetPlatformHandle() with
            | null -> None
            | handle -> Some handle.Handle
        with ex ->
            Logging.logError (sprintf "Failed to get window handle: %s" ex.Message)
            None

    /// Create a Windows native menu that replaces Avalonia's broken positioning
    let createWindowsNativeMenuWithHandle (window: Window) (actions: TrayAction list) (onMenuItemClick: TrayActionType -> unit) (trayIcon: TrayIcon) : unit =
        try
            match getWindowHandle window with
            | Some hwnd ->
                // Create a single menu item that triggers our properly positioned Windows menu
                let menu = new NativeMenu()
                let triggerItem = new NativeMenuItem()
                triggerItem.Header <- "DisplaySwitch-Pro Menu"
                triggerItem.Click.Add(fun _ ->
                    // Show our properly positioned Windows native menu
                    try
                        match showWindowsContextMenu actions onMenuItemClick hwnd with
                        | Ok () -> Logging.logVerbose "Windows native menu shown with correct positioning"
                        | Error err -> Logging.logError (sprintf "Windows native menu failed: %s" err)
                    with ex ->
                        Logging.logError (sprintf "Exception in Windows native menu: %s" ex.Message))

                menu.Add(triggerItem)
                trayIcon.Menu <- menu
                Logging.logVerbose "Set up Windows native menu with window handle"
            | None ->
                // Fallback to regular Avalonia menu
                updateAvaloniaMenu trayIcon actions onMenuItemClick
                Logging.logError "Could not get window handle, using Avalonia menu"
        with ex ->
            Logging.logError (sprintf "Failed to create Windows native menu with handle: %s" ex.Message)
            updateAvaloniaMenu trayIcon actions onMenuItemClick

    /// Update tray menu using platform-appropriate method
    let updateTrayMenu (trayIcon: TrayIcon) (actions: TrayAction list) (onMenuItemClick: TrayActionType -> unit) : unit =
        if isWindows then
            // On Windows, completely disable Avalonia's menu system
            trayIcon.Menu <- null
            Logging.logVerbose "Disabled Avalonia menu on Windows - will use native Windows menu only"
        else
            // On other platforms, use Avalonia's NativeMenu
            updateAvaloniaMenu trayIcon actions onMenuItemClick
            Logging.logVerbose "Updated Avalonia NativeMenu for cross-platform"

    // ===== Platform-Specific Menu Pipeline =====

    /// Show context menu using platform-appropriate method
    let showPlatformContextMenu
        (actions: TrayAction list)
        (onMenuItemClick: TrayActionType -> unit)
        (trayIcon: TrayIcon) : Result<unit, string> =
        if isWindows then
            // Use Windows native context menu for proper positioning
            try
                // Get main window handle for proper foreground setting
                let mainWindow = GetForegroundWindow()
                showWindowsContextMenu actions onMenuItemClick mainWindow
            with ex ->
                Logging.logError (sprintf "Windows native menu failed, falling back to Avalonia: %s" ex.Message)
                // Fallback to Avalonia NativeMenu
                updateAvaloniaMenu trayIcon actions onMenuItemClick
                Ok ()
        else
            // Use Avalonia's cross-platform NativeMenu for other platforms
            updateAvaloniaMenu trayIcon actions onMenuItemClick
            Ok ()

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
                Logging.logNormal "Application exit requested from tray"
                System.Environment.Exit(0)

            | Separator ->
                () // No action for separator

        with ex ->
            Logging.logError (sprintf "Tray action handling failed: %s" ex.Message)

    /// Create Avalonia TrayIcon fallback with proper setup
    let createAvaloniaFallbackTray (settings: TraySettings) (initialState: TrayApplicationState) (getPresets: unit -> Map<string, _>) : TrayApplicationState * TrayIcon =
        let trayIcon = createTrayIcon
                            handleTrayAction
                            (fun () -> handleTrayAction settings.TrayClickAction)
                            (fun () -> handleTrayAction settings.DoubleClickAction)
        let updatedState = updateTrayIcon (Some trayIcon) initialState
        let currentPresets = getPresets()
        let menuActions = buildTrayMenu currentPresets updatedState settings
        updateTrayMenu trayIcon menuActions handleTrayAction
        if settings.EnableSystemTray then showTrayIcon trayIcon else hideTrayIcon trayIcon
        (updatedState, trayIcon)

    /// Set up Windows message handling for system tray
    // Global reference to the subclass procedure to prevent GC collection
    let mutable traySubclassProc : SUBCLASSPROC option = None

    let setupWindowsMessageHandling (window: Window) (hwnd: IntPtr) (settings: TraySettings) (getPresets: unit -> Map<string, _>) : unit =
        try
            // Create subclass procedure for handling tray messages
            let subclassProc = SUBCLASSPROC(fun hwnd uMsg wParam lParam uIdSubclass dwRefData ->
                match uMsg with
                | msg when msg = WM_TRAYICON ->
                    let lParam = int lParam
                    match lParam with
                    | lp when lp = WM_LBUTTONUP ->
                        // Handle left click
                        Dispatcher.UIThread.InvokeAsync(fun () ->
                            handleTrayAction settings.TrayClickAction
                            Logging.logVerbose "Tray icon left clicked"
                        ) |> ignore
                    | lp when lp = WM_RBUTTONUP ->
                        // Handle right click - show context menu
                        Dispatcher.UIThread.InvokeAsync(fun () ->
                            try
                                let mutable cursorPos = { X = 0; Y = 0 }
                                if GetCursorPos(&cursorPos) then
                                    // Build menu actions with current presets
                                    let currentPresets = getPresets()
                                    let menuActions = buildTrayMenu currentPresets (createTrayState []) settings

                                    // Set foreground window for proper menu behavior
                                    SetForegroundWindow(hwnd) |> ignore

                                    // Show Windows context menu at cursor position
                                    showWindowsContextMenu menuActions handleTrayAction hwnd |> ignore

                                    Logging.logVerbose "Tray context menu shown via Windows API"
                                else
                                    Logging.logError "Failed to get cursor position for tray menu"
                            with ex ->
                                Logging.logError (sprintf "Failed to show tray context menu: %s" ex.Message)
                        ) |> ignore
                    | lp when lp = WM_LBUTTONDBLCLK ->
                        // Handle double click
                        Dispatcher.UIThread.InvokeAsync(fun () ->
                            handleTrayAction settings.DoubleClickAction
                            Logging.logVerbose "Tray icon double clicked"
                        ) |> ignore
                    | _ -> () // Ignore other messages
                | _ -> () // Not our message, pass to default

                // Always call default subclass procedure
                DefSubclassProc(hwnd, uMsg, wParam, lParam)
            )

            // Store reference to prevent GC
            traySubclassProc <- Some subclassProc

            // Install the subclass procedure
            let success = SetWindowSubclass(hwnd, subclassProc, UIntPtr(1u), UIntPtr.Zero)
            if success then
                Logging.logVerbose "Windows message handling for tray icon initialized successfully"
            else
                Logging.logError "Failed to install window subclass for tray message handling"
        with ex ->
            Logging.logError (sprintf "Failed to setup Windows message handling: %s" ex.Message)

    /// Create Windows-native system tray (bypassing Avalonia completely)
    let createWindowsNativeSystemTray (settings: TraySettings) (window: Window) (getPresets: unit -> Map<string, _>) : Result<unit, string> =
        try
            match getWindowHandle window with
            | Some hwnd ->
                // Create the Windows system tray icon
                match createWindowsSystemTrayIcon hwnd "" with
                | Ok () ->
                    // Set up message handling for tray icon clicks
                    setupWindowsMessageHandling window hwnd settings getPresets
                    Logging.logNormal "Windows native system tray created successfully"
                    Ok ()
                | Error err ->
                    Error (sprintf "Failed to create Windows system tray: %s" err)
            | None ->
                Error "Could not get window handle for Windows system tray"
        with ex ->
            Error (sprintf "Exception creating Windows system tray: %s" ex.Message)

    /// Create complete tray management system with preset callback
    let createTrayManagementWithPresets (settings: TraySettings) (window: Window option) (getPresets: unit -> Map<string, _>) : TrayApplicationState * TrayIcon =
        try
            // Ensure UICoordinator is initialized before creating tray
            UIEventSystem.UICoordinator.initialize()

            let initialState = createTrayState []

            if isWindows && window.IsSome then
                // On Windows, use pure Windows Shell_NotifyIcon
                match createWindowsNativeSystemTray settings window.Value getPresets with
                | Ok () ->
                    Logging.logNormal "Using Windows native system tray (no Avalonia TrayIcon)"
                    // Return minimal state - no Avalonia TrayIcon needed
                    let dummyTrayIcon = new TrayIcon()
                    dummyTrayIcon.IsVisible <- false  // Hide it completely
                    (initialState, dummyTrayIcon)
                | Error err ->
                    Logging.logError (sprintf "Windows native tray failed: %s" err)
                    // Fallback to Avalonia TrayIcon with proper preset integration
                    createAvaloniaFallbackTray settings initialState getPresets
            else
                // Cross-platform: use Avalonia TrayIcon with proper preset integration
                Logging.logNormal "Using Avalonia TrayIcon for cross-platform compatibility"
                createAvaloniaFallbackTray settings initialState getPresets
        with ex ->
            Logging.logError (sprintf "Failed to create tray management: %s" ex.Message)
            let fallbackIcon = new TrayIcon()
            (createTrayState [], fallbackIcon)

    /// Create complete tray management system with window reference for native Windows menus
    let createTrayManagementWithWindow (settings: TraySettings) (window: Window option) : TrayApplicationState * TrayIcon =
        // Legacy function that provides empty presets for backward compatibility
        let getPresets() = Map.empty
        createTrayManagementWithPresets settings window getPresets

    /// Create complete tray management system (backward compatibility)
    let createTrayManagement (settings: TraySettings) : TrayApplicationState * TrayIcon =
        createTrayManagementWithWindow settings None

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