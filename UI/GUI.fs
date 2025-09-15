namespace DisplaySwitchPro

open System
open Avalonia.Controls

// GUI coordinator module with functional display monitoring
module GUI =

    // Local state for display monitoring (using ref for interop with timer)
    let private monitorStateRef : DisplayMonitor.MonitorState option ref = ref None
    
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
    
    // Handle display change events from the monitor (pure function)
    let private onDisplayChanged (changeEvent: DisplayMonitor.DisplayChangeEvent) =
        printfn "[DisplayMonitor] Display change detected: %A" changeEvent.ChangeType
        printfn "[DisplayMonitor] Previous displays: %d, Current displays: %d"
            changeEvent.PreviousDisplays.Length changeEvent.CurrentDisplays.Length

        // Refresh the UI to reflect display changes
        refreshMainWindowContent()

    // Start display monitoring functionally
    let private startDisplayMonitoring() =
        match !monitorStateRef with
        | Some _ ->
            printfn "[GUI] Display monitoring already active"
        | None ->
            let monitorState = DisplayMonitor.startMonitoring onDisplayChanged 2000
            monitorStateRef := Some monitorState
            printfn "[GUI] Started display change monitoring"

    // Stop display monitoring functionally
    let private stopDisplayMonitoring() =
        match !monitorStateRef with
        | Some monitorState ->
            DisplayMonitor.stopMonitoring monitorState
            monitorStateRef := None
            printfn "[GUI] Stopped display monitoring"
        | None ->
            printfn "[GUI] No active display monitoring to stop"

    // Initialize the GUI modules with refresh function references
    let private initializeModules() =
        MainContentPanel.setRefreshFunction refreshMainWindowContent
        WindowManager.setRefreshFunction refreshMainWindowContent
        UIComponents.setRefreshFunction refreshMainWindowContent

        // Start display monitoring
        startDisplayMonitoring()
    
    // Create main window
    let createMainWindow (appState: AppState) (adapter: IPlatformAdapter) =
        initializeModules()
        WindowManager.createMainWindow appState adapter

