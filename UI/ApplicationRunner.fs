namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

// Application class and startup logic
module ApplicationRunner =
    
    // Immutable application data
    type AppData = {
        State: AppState option
        Adapter: IPlatformAdapter option
        TraySettings: TraySystem.TraySettings
        TrayIcon: Avalonia.Controls.TrayIcon option
    }
    
    // Avalonia Application class with functional state
    type App(appData: AppData ref) =
        inherit Application()
        
        override this.Initialize() =
            this.Styles.Add(FluentTheme())

            // Initialize theme resources
            Theme.initializeThemeResources this

            Logging.logNormal "Avalonia application initialized with theme support"
        
        override this.OnFrameworkInitializationCompleted() =
            match this.ApplicationLifetime with
            | :? IClassicDesktopStyleApplicationLifetime as desktop ->
                match (!appData).State, (!appData).Adapter with
                | Some state, Some adapter ->
                    let window = GUI.createMainWindow state adapter
                    desktop.MainWindow <- window

                    // Initialize system tray if enabled
                    let traySettings = (!appData).TraySettings
                    if traySettings.EnableSystemTray then
                        try
                            // Create preset callback that gets current presets from UIStateManager
                            let getPresets() =
                                let model = UIStateManager.StateManager.getModel()
                                model.AppState.SavedPresets

                            let (trayState, trayIcon) = TraySystem.createTrayManagementWithPresets traySettings (Some window) getPresets

                            // Update UIStateManager with tray state
                            UIStateManager.BackwardCompatibility.updateTrayState (Some trayState)
                            UIStateManager.BackwardCompatibility.updateTraySettings traySettings

                            // Update app data with tray icon
                            appData := { (!appData) with TrayIcon = Some trayIcon }

                            // Set up event handling for tray actions
                            this.SetupTrayEventHandling window trayIcon

                            // Set up window management for tray
                            this.SetupTrayWindowIntegration window trayIcon traySettings

                            Logging.logNormal "System tray initialized successfully"
                        with ex ->
                            Logging.logErrorf "Failed to initialize system tray: %s" ex.Message

                    // Show window unless starting minimized
                    if not traySettings.StartMinimized then
                        window.Show()
                        Logging.logVerbose "Window created and shown"
                    else
                        Logging.logVerbose "Window created, starting minimized to tray"

                | _ -> failwith "Application data not set"
            | _ ->
                Logging.logError "No desktop lifetime found"

            base.OnFrameworkInitializationCompleted()

        // Set up tray-window integration
        member private this.SetupTrayWindowIntegration
            (window: Window)
            (trayIcon: Avalonia.Controls.TrayIcon)
            (settings: TraySystem.TraySettings) =
            try
                // Handle window state changes for tray functionality
                window.PropertyChanged.Add(fun e ->
                    if e.Property.Name = "WindowState" then
                        match window.WindowState with
                        | WindowState.Minimized when settings.MinimizeToTray ->
                            window.Hide()
                            UIStateManager.StateUpdates.updateWindowStateForTray (fun ws ->
                                { ws with IsVisible = false; IsInTray = true; IsWindowMinimized = true }) |> ignore
                            Logging.logVerbose "Window minimized to tray"
                        | _ -> ()
                )

                // Handle window closing
                window.Closing.Add(fun e ->
                    if settings.CloseToTray then
                        e.Cancel <- true
                        window.Hide()
                        UIStateManager.StateUpdates.updateWindowStateForTray (fun ws ->
                            { ws with IsVisible = false; IsInTray = true }) |> ignore
                        Logging.logVerbose "Window closed to tray"
                )

                // Update tray menu when presets change
                UIStateManager.StateManager.subscribeToModelUpdates (fun model ->
                    try
                        let trayState = model.TrayState
                        match trayState with
                        | Some ts ->
                            let menuActions = TraySystem.buildTrayMenu model.AppState.SavedPresets ts settings
                            TraySystem.updateTrayMenu trayIcon menuActions TraySystem.handleTrayAction
                        | None -> ()
                    with ex ->
                        Logging.logErrorf "Failed to update tray menu: %s" ex.Message
                ) |> ignore

                Logging.logVerbose "Tray-window integration set up successfully"
            with ex ->
                Logging.logErrorf "Failed to set up tray-window integration: %s" ex.Message

        // Set up tray event handling
        member private this.SetupTrayEventHandling
            (window: Window)
            (trayIcon: Avalonia.Controls.TrayIcon) =
            try
                // Subscribe to UI events and handle them
                UIEventSystem.UICoordinator.subscribeToUIMessages (fun message ->
                    match message with
                    | UIEventSystem.UIEvent event ->
                        match event with
                        | UIEventSystem.RefreshMainWindow ->
                            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(fun () ->
                                window.Show()
                                window.WindowState <- WindowState.Normal
                                window.Activate()
                                window.Focus() |> ignore
                                Logging.logVerbose "Window restored from tray"
                            ) |> ignore

                        | UIEventSystem.DisplayDetectionRequested ->
                            Logging.logNormal "Display detection requested from tray - functionality needs implementation"

                        | UIEventSystem.PresetApplied presetName ->
                            Logging.logNormalf "Preset application requested from tray: %s - functionality needs implementation" presetName

                        | _ -> () // Ignore other events

                    | _ -> () // Ignore non-UI events
                ) |> ignore

                Logging.logVerbose "Tray event handling set up successfully"
            with ex ->
                Logging.logErrorf "Failed to set up tray event handling: %s" ex.Message

    // Functional application runner
    let run (adapter: IPlatformAdapter) (state: AppState) =
        let appDataRef = ref {
            State = Some state
            Adapter = Some adapter
            TraySettings = TraySystem.defaultTraySettings
            TrayIcon = None
        }

        // IMPORTANT: Initialize UIStateManager with the loaded state (including presets)
        UIStateManager.StateManager.updateModel (UIEventSystem.StateUpdate state) |> ignore
        UIStateManager.StateUpdates.updateAdapter adapter |> ignore
        Logging.logNormalf "UIStateManager initialized with %d presets" state.SavedPresets.Count
        try
            Logging.logNormal "Starting Avalonia application..."
            let result = 
                AppBuilder
                    .Configure<App>(fun () -> App(appDataRef))
                    .UsePlatformDetect()
                    .LogToTrace()
                    .StartWithClassicDesktopLifetime([||])
            Logging.logNormalf "Avalonia application finished with exit code: %d" result
            result
        with
        | ex ->
            Logging.logErrorf "Error starting Avalonia: %s" ex.Message
            Logging.logErrorf "Stack trace: %s" ex.StackTrace
            1

    // Functional application runner with custom tray settings
    let runWithTraySettings (adapter: IPlatformAdapter) (state: AppState) (traySettings: TraySystem.TraySettings) =
        let appDataRef = ref {
            State = Some state
            Adapter = Some adapter
            TraySettings = traySettings
            TrayIcon = None
        }
        try
            Logging.logNormal "Starting Avalonia application with custom tray settings..."
            let result =
                AppBuilder
                    .Configure<App>(fun () -> App(appDataRef))
                    .UsePlatformDetect()
                    .LogToTrace()
                    .StartWithClassicDesktopLifetime([||])
            Logging.logNormalf "Avalonia application finished with exit code: %d" result
            result
        with
        | ex ->
            Logging.logErrorf "Error starting Avalonia: %s" ex.Message
            Logging.logErrorf "Stack trace: %s" ex.StackTrace
            1