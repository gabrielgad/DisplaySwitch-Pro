namespace DisplaySwitchPro

open System
open Avalonia.Controls

// Functional UI state management with encapsulated references
module UIState =
    
    // Immutable state type (renamed to avoid conflict with AppState.fs)
    type UIAppState = {
        AppState: AppState
        Adapter: IPlatformAdapter option
        MainWindow: Window option
        DisplaySettingsDialog: Window option
        CurrentDialogDisplay: DisplayInfo option
    }
    
    // Default state
    let private defaultState = {
        AppState = AppState.empty
        Adapter = None
        MainWindow = None
        DisplaySettingsDialog = None
        CurrentDialogDisplay = None
    }
    
    // Encapsulated mutable reference (single source of truth)
    let private globalState = ref defaultState
    
    // Functional state update helpers
    let updateAppState newAppState =
        let currentState = !globalState
        globalState := { currentState with AppState = newAppState }
    
    let updateAdapter adapter =
        let currentState = !globalState
        globalState := { currentState with Adapter = Some adapter }
    
    let setMainWindow window =
        let currentState = !globalState
        globalState := { currentState with MainWindow = Some window }
        
    let setDisplaySettingsDialog dialog =
        let currentState = !globalState
        globalState := { currentState with DisplaySettingsDialog = dialog }
        
    let setCurrentDialogDisplay display =
        let currentState = !globalState
        globalState := { currentState with CurrentDialogDisplay = display }
        
    // Functional getters - return immutable values
    let getCurrentAppState() = (!globalState).AppState
    let getCurrentAdapter() = (!globalState).Adapter
    let getMainWindow() = (!globalState).MainWindow
    let getDisplaySettingsDialog() = (!globalState).DisplaySettingsDialog
    let getCurrentDialogDisplay() = (!globalState).CurrentDialogDisplay