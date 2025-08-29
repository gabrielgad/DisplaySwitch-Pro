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
                
                // Preserve the application-level IsEnabled state from current world
                let preservedDisplays = 
                    freshDisplays |> List.map (fun freshDisplay ->
                        match Map.tryFind freshDisplay.Id (UIState.getCurrentWorld()).Components.ConnectedDisplays with
                        | Some existingDisplay ->
                            // Preserve the application-level IsEnabled state
                            { freshDisplay with IsEnabled = existingDisplay.IsEnabled }
                        | None ->
                            // New display, keep system state
                            freshDisplay
                    )
                
                // Update the world with preserved display data
                let currentWorld = UIState.getCurrentWorld()
                let updatedComponents = 
                    preservedDisplays |> List.fold (fun components display ->
                        Components.addDisplay display components
                    ) currentWorld.Components
                
                UIState.updateWorld { currentWorld with Components = updatedComponents }
                
                // Recreate the UI with updated display info
                let content = MainContentPanel.createMainContentPanel (UIState.getCurrentWorld()) adapter
                window.Content <- content
                printfn "[DEBUG GUI] UI refreshed with %d displays" preservedDisplays.Length
            | None -> ()
        | None -> ()
    
    // Initialize the GUI modules with refresh function references
    let private initializeModules() =
        MainContentPanel.setRefreshFunction refreshMainWindowContent
        WindowManager.setRefreshFunction refreshMainWindowContent
    
    // Create main window
    let createMainWindow (world: World) (adapter: IPlatformAdapter) =
        initializeModules()
        WindowManager.createMainWindow world adapter

