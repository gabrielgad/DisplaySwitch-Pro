namespace DisplaySwitchPro

open System
open System.Threading
open Avalonia.Controls

/// Unified state management system replacing mutable references
/// This module provides a single source of truth for all UI state
module UIStateManager =

    // Unified UI model containing all application state
    type UIModel = {
        AppState: AppState
        UISettings: UISettings
        Theme: Theme.Theme
        WindowState: WindowState
        TrayState: TraySystem.TrayApplicationState option
        TraySettings: TraySystem.TraySettings
        EventLog: UIEventSystem.UIEvent list
        LastUpdate: DateTime
        Adapter: IPlatformAdapter option
    }

    and UISettings = {
        WindowSize: float * float
        WindowPosition: (float * float) option
        AutoRefreshInterval: TimeSpan option
        ShowAdvancedOptions: bool
        EnableDisplayMonitoring: bool
        LogVerbosity: string
    }

    and WindowState = {
        MainWindow: Window option
        DisplaySettingsDialog: Window option
        CurrentDialogDisplay: DisplayInfo option
        IsWindowMinimized: bool
        IsWindowMaximized: bool
        IsVisible: bool
        IsInTray: bool
    }

    // Default instances
    let private defaultUISettings = {
        WindowSize = (1200.0, 700.0)
        WindowPosition = None
        AutoRefreshInterval = Some (TimeSpan.FromSeconds(2.0))
        ShowAdvancedOptions = false
        EnableDisplayMonitoring = true
        LogVerbosity = "Normal"
    }

    let private defaultWindowState = {
        MainWindow = None
        DisplaySettingsDialog = None
        CurrentDialogDisplay = None
        IsWindowMinimized = false
        IsWindowMaximized = false
        IsVisible = true
        IsInTray = false
    }

    let empty = {
        AppState = AppState.empty
        UISettings = defaultUISettings
        Theme = Theme.Light
        WindowState = defaultWindowState
        TrayState = None
        TraySettings = TraySystem.defaultTraySettings
        EventLog = []
        LastUpdate = DateTime.MinValue
        Adapter = None
    }

    // Thread-safe state manager
    module StateManager =
        let private stateLock = obj()
        let private currentModel = ref empty
        let private modelUpdateBus = UIEventSystem.EventBus.create<UIModel>()

        // Process individual UI events (internal function)
        let private processUIEventInternal (event: UIEventSystem.UIEvent) (model: UIModel) : UIModel =
            match event with
            | UIEventSystem.RefreshMainWindow ->
                model // UI side effect only, no state change needed

            | UIEventSystem.DisplayToggled (displayId, enabled) ->
                match Map.tryFind displayId model.AppState.ConnectedDisplays with
                | Some display ->
                    let updatedDisplay = { display with IsEnabled = enabled }
                    let updatedAppState = AppState.addDisplay updatedDisplay model.AppState
                    { model with AppState = updatedAppState }
                | None ->
                    Logging.logError (sprintf "Display not found for toggle: %s" displayId)
                    model

            | UIEventSystem.PresetApplied presetName ->
                match AppState.loadPreset presetName model.AppState with
                | Some newAppState -> { model with AppState = newAppState }
                | None ->
                    Logging.logError (sprintf "Failed to load preset: %s" presetName)
                    model

            | UIEventSystem.PresetSaved presetName ->
                Logging.logNormal (sprintf "Preset saved: %s" presetName)
                model // State change handled elsewhere

            | UIEventSystem.PresetDeleted presetName ->
                let updatedAppState = AppState.deletePreset presetName model.AppState
                { model with AppState = updatedAppState }

            | UIEventSystem.ThemeChanged newTheme ->
                { model with Theme = newTheme }

            | UIEventSystem.WindowResized (width, height) ->
                let updatedSettings = { model.UISettings with WindowSize = (width, height) }
                { model with UISettings = updatedSettings }

            | UIEventSystem.DisplayDetectionRequested ->
                model // Triggers side effect, no state change

            | UIEventSystem.DisplayPositionChanged (displayId, position) ->
                match Map.tryFind displayId model.AppState.ConnectedDisplays with
                | Some display ->
                    let updatedDisplay = { display with Position = position }
                    let updatedAppState = AppState.addDisplay updatedDisplay model.AppState
                    { model with AppState = updatedAppState }
                | None ->
                    Logging.logError (sprintf "Display not found for position update: %s" displayId)
                    model

            | UIEventSystem.DisplayDragCompleted (displayId, position) ->
                // Similar to position change but might trigger additional logic
                match Map.tryFind displayId model.AppState.ConnectedDisplays with
                | Some display ->
                    let updatedDisplay = { display with Position = position }
                    let updatedAppState = AppState.addDisplay updatedDisplay model.AppState
                    { model with AppState = updatedAppState }
                | None ->
                    Logging.logError (sprintf "Display not found for drag completion: %s" displayId)
                    model

            | UIEventSystem.DisplaySettingsChanged (displayId, displayInfo) ->
                let updatedAppState = AppState.addDisplay displayInfo model.AppState
                { model with AppState = updatedAppState }

            | UIEventSystem.ErrorOccurred error ->
                Logging.logError error
                model

            | UIEventSystem.UIInitialized ->
                Logging.logNormal "UI system initialized"
                model

            | UIEventSystem.UIShutdown ->
                Logging.logNormal "UI system shutting down"
                model

        // Get current model safely
        let getModel() : UIModel =
            lock stateLock (fun () -> !currentModel)

        // Subscribe to model updates
        let subscribeToModelUpdates (handler: UIModel -> unit) : IDisposable =
            modelUpdateBus.Subscribe handler

        // Update model with a function
        let updateModelWith (updateFunc: UIModel -> UIModel) : UIModel =
            lock stateLock (fun () ->
                let newModel = updateFunc !currentModel
                let timestampedModel = { newModel with LastUpdate = DateTime.Now }
                currentModel := timestampedModel
                modelUpdateBus.Publish timestampedModel
                timestampedModel)

        // Update model with a message
        let updateModel (message: UIEventSystem.UIMessage) : UIModel =
            updateModelWith (fun model ->
                match message with
                | UIEventSystem.UIEvent event ->
                    let updatedModel = processUIEventInternal event model
                    { updatedModel with
                        EventLog = event :: (List.take 99 updatedModel.EventLog) // Keep last 100 events
                        LastUpdate = DateTime.Now }

                | UIEventSystem.StateUpdate newAppState ->
                    { model with
                        AppState = newAppState
                        LastUpdate = DateTime.Now }

                | UIEventSystem.ConfigurationChanged config ->
                    let updatedAppState = AppState.setCurrentConfiguration config model.AppState
                    { model with
                        AppState = updatedAppState
                        LastUpdate = DateTime.Now }

                | UIEventSystem.SystemMessage msg ->
                    Logging.logNormal (sprintf "System message: %s" msg)
                    model)

        // Clear all state (for testing/reset)
        let resetState() : UIModel =
            updateModelWith (fun _ -> empty)

        // Get specific state components
        let getCurrentAppState() = (getModel()).AppState
        let getCurrentAdapter() = (getModel()).Adapter
        let getMainWindow() = (getModel()).WindowState.MainWindow
        let getDisplaySettingsDialog() = (getModel()).WindowState.DisplaySettingsDialog
        let getCurrentDialogDisplay() = (getModel()).WindowState.CurrentDialogDisplay
        let getCurrentTheme() = (getModel()).Theme
        let getUISettings() = (getModel()).UISettings
        let getTrayState() = (getModel()).TrayState
        let getTraySettings() = (getModel()).TraySettings
        let getWindowState() = (getModel()).WindowState



    // Enhanced state update functions with validation
    module StateUpdates =

        // Update adapter with validation
        let updateAdapter (adapter: IPlatformAdapter) : UIEventSystem.UIResult<UIModel> =
            try
                let newModel = StateManager.updateModelWith (fun model ->
                    { model with Adapter = Some adapter })
                Ok newModel
            with ex ->
                Error (sprintf "Failed to update adapter: %s" ex.Message)

        // Update main window reference
        let setMainWindow (window: Window) : UIEventSystem.UIResult<UIModel> =
            try
                let newModel = StateManager.updateModelWith (fun model ->
                    { model with WindowState = { model.WindowState with MainWindow = Some window } })
                Ok newModel
            with ex ->
                Error (sprintf "Failed to set main window: %s" ex.Message)

        // Update display settings dialog
        let setDisplaySettingsDialog (dialog: Window option) : UIEventSystem.UIResult<UIModel> =
            try
                let newModel = StateManager.updateModelWith (fun model ->
                    { model with WindowState = { model.WindowState with DisplaySettingsDialog = dialog } })
                Ok newModel
            with ex ->
                Error (sprintf "Failed to set display settings dialog: %s" ex.Message)

        // Update current dialog display
        let setCurrentDialogDisplay (display: DisplayInfo option) : UIEventSystem.UIResult<UIModel> =
            try
                let newModel = StateManager.updateModelWith (fun model ->
                    { model with WindowState = { model.WindowState with CurrentDialogDisplay = display } })
                Ok newModel
            with ex ->
                Error (sprintf "Failed to set current dialog display: %s" ex.Message)

        // Update UI settings
        let updateUISettings (updateFunc: UISettings -> UISettings) : UIEventSystem.UIResult<UIModel> =
            try
                let newModel = StateManager.updateModelWith (fun model ->
                    { model with UISettings = updateFunc model.UISettings })
                Ok newModel
            with ex ->
                Error (sprintf "Failed to update UI settings: %s" ex.Message)

        // Update theme
        let updateTheme (theme: Theme.Theme) : UIEventSystem.UIResult<UIModel> =
            try
                let newModel = StateManager.updateModelWith (fun model ->
                    { model with Theme = theme })
                UIEventSystem.UICoordinator.notifyThemeChanged theme
                Ok newModel
            with ex ->
                Error (sprintf "Failed to update theme: %s" ex.Message)

        // Update tray state
        let updateTrayState (trayState: TraySystem.TrayApplicationState option) : UIEventSystem.UIResult<UIModel> =
            try
                let newModel = StateManager.updateModelWith (fun model ->
                    { model with TrayState = trayState })
                Ok newModel
            with ex ->
                Error (sprintf "Failed to update tray state: %s" ex.Message)

        // Update tray settings
        let updateTraySettings (settings: TraySystem.TraySettings) : UIEventSystem.UIResult<UIModel> =
            try
                let newModel = StateManager.updateModelWith (fun model ->
                    { model with TraySettings = settings })
                Ok newModel
            with ex ->
                Error (sprintf "Failed to update tray settings: %s" ex.Message)

        // Update window state for tray operations
        let updateWindowStateForTray (updateFunc: WindowState -> WindowState) : UIEventSystem.UIResult<UIModel> =
            try
                let newModel = StateManager.updateModelWith (fun model ->
                    { model with WindowState = updateFunc model.WindowState })
                Ok newModel
            with ex ->
                Error (sprintf "Failed to update window state for tray: %s" ex.Message)

    // Backward compatibility layer for existing code
    module BackwardCompatibility =

        // Legacy functions that now use the unified state manager
        let updateAppState (newAppState: AppState) : unit =
            StateManager.updateModel (UIEventSystem.StateUpdate newAppState) |> ignore

        let updateAdapter (adapter: IPlatformAdapter) : unit =
            StateUpdates.updateAdapter adapter |> ignore

        let setMainWindow (window: Window) : unit =
            StateUpdates.setMainWindow window |> ignore

        let setDisplaySettingsDialog (dialog: Window option) : unit =
            StateUpdates.setDisplaySettingsDialog dialog |> ignore

        let setCurrentDialogDisplay (display: DisplayInfo option) : unit =
            StateUpdates.setCurrentDialogDisplay display |> ignore

        // Legacy getters
        let getCurrentAppState() = StateManager.getCurrentAppState()
        let getCurrentAdapter() = StateManager.getCurrentAdapter()
        let getMainWindow() = StateManager.getMainWindow()
        let getDisplaySettingsDialog() = StateManager.getDisplaySettingsDialog()
        let getCurrentDialogDisplay() = StateManager.getCurrentDialogDisplay()
        let getTrayState() = StateManager.getTrayState()
        let getTraySettings() = StateManager.getTraySettings()

        // Tray management functions
        let updateTrayState (trayState: TraySystem.TrayApplicationState option) : unit =
            StateUpdates.updateTrayState trayState |> ignore

        let updateTraySettings (settings: TraySystem.TraySettings) : unit =
            StateUpdates.updateTraySettings settings |> ignore

    // Diagnostic and monitoring functions
    module Diagnostics =

        let getStateInfo() = {|
            ModelTimestamp = (StateManager.getModel()).LastUpdate
            EventCount = (StateManager.getModel()).EventLog.Length
            HasAdapter = (StateManager.getModel()).Adapter.IsSome
            HasMainWindow = (StateManager.getModel()).WindowState.MainWindow.IsSome
            CurrentTheme = (StateManager.getModel()).Theme.ToString()
            ConnectedDisplayCount = (StateManager.getModel()).AppState.ConnectedDisplays.Count
            SavedPresetCount = (StateManager.getModel()).AppState.SavedPresets.Count
            HasTrayState = (StateManager.getModel()).TrayState.IsSome
            TrayEnabled = (StateManager.getModel()).TraySettings.EnableSystemTray
            WindowVisible = (StateManager.getModel()).WindowState.IsVisible
            WindowInTray = (StateManager.getModel()).WindowState.IsInTray
        |}

        let getRecentEvents (count: int) : UIEventSystem.UIEvent list =
            (StateManager.getModel()).EventLog |> List.take (min count 100)

        let logCurrentState() =
            let info = getStateInfo()
            Logging.logNormal (sprintf "UI State - Displays: %d, Presets: %d, Theme: %s, Tray: %b, Window: %s, Last Update: %A"
                info.ConnectedDisplayCount info.SavedPresetCount info.CurrentTheme info.TrayEnabled
                (if info.WindowVisible then "Visible" else if info.WindowInTray then "In Tray" else "Hidden")
                info.ModelTimestamp)