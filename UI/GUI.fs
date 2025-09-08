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
                
                // Preserve the application-level IsEnabled state and user-set positions from current app state
                let preservedDisplays = 
                    freshDisplays |> List.map (fun freshDisplay ->
                        match Map.tryFind freshDisplay.Id (UIState.getCurrentAppState()).ConnectedDisplays with
                        | Some existingDisplay ->
                            // Preserve the application-level IsEnabled state and user-arranged positions
                            { freshDisplay with 
                                IsEnabled = existingDisplay.IsEnabled
                                Position = existingDisplay.Position  // Preserve compacted positions
                            }
                        | None ->
                            // New display, keep system state
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

