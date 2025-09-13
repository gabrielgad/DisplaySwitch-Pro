namespace DisplaySwitchPro

open System
open Avalonia.Controls

// GUI coordinator module
module GUI =
    
    // Refresh main window content
    let rec refreshMainWindowContent () =
        match UIState.getMainWindow() with
        | Some window ->
            match UIState.getCurrentAdapter() with
            | Some adapter ->
                // Re-detect displays to get updated information, but preserve application-level disabled states
                printfn "[DEBUG GUI] Refreshing display information..."
                let freshDisplays = adapter.GetConnectedDisplays()
                
                // Use fresh Windows positions and states - don't preserve old positions
                let preservedDisplays = 
                    freshDisplays |> List.map (fun freshDisplay ->
                        // Use fresh Windows positions and IsEnabled state
                        // This ensures UI canvas reflects actual Windows display configuration
                        freshDisplay
                    )
                
                // Update the app state with preserved display data
                let currentAppState = UIState.getCurrentAppState()
                let updatedAppState = AppState.updateDisplays preservedDisplays currentAppState
                UIState.updateAppState updatedAppState
                
                // Recreate the UI with updated display info
                let content = MainContentPanel.createMainContentPanel (UIState.getCurrentAppState()) adapter
                window.Content <- content
                printfn "[DEBUG GUI] UI refreshed with %d displays" preservedDisplays.Length
            | None -> ()
        | None -> ()
    
    // Initialize the GUI modules with refresh function references
    let private initializeModules() =
        MainContentPanel.setRefreshFunction refreshMainWindowContent
        WindowManager.setRefreshFunction refreshMainWindowContent
        UIComponents.setRefreshFunction refreshMainWindowContent
    
    // Create main window
    let createMainWindow (appState: AppState) (adapter: IPlatformAdapter) =
        initializeModules()
        WindowManager.createMainWindow appState adapter

