namespace DisplaySwitchPro

open System
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media

// Window creation and management
module WindowManager =
    
    // Import the refreshMainWindowContent function reference
    // This will need to be set by the main GUI module
    let mutable refreshMainWindowContentRef: (unit -> unit) option = None
    
    // Set the refresh function reference
    let setRefreshFunction refreshFunc =
        refreshMainWindowContentRef <- Some refreshFunc
    
    // Helper function to call refresh
    let private refreshMainWindowContent() =
        match refreshMainWindowContentRef with
        | Some refreshFunc -> refreshFunc()
        | None -> printfn "[WARNING] Refresh function not set"
    
    // Handle keyboard shortcuts for preset switching
    let private setupKeyboardShortcuts (window: Window) =
        window.KeyDown.Add(fun e ->
            let modifiers = e.KeyModifiers
            let key = e.Key
            
            // Check for Ctrl+Shift+1-9 for preset shortcuts
            if modifiers.HasFlag(KeyModifiers.Control) && modifiers.HasFlag(KeyModifiers.Shift) then
                let presetIndex = 
                    match key with
                    | Key.D1 -> Some 0 | Key.D2 -> Some 1 | Key.D3 -> Some 2 | Key.D4 -> Some 3 | Key.D5 -> Some 4
                    | Key.D6 -> Some 5 | Key.D7 -> Some 6 | Key.D8 -> Some 7 | Key.D9 -> Some 8
                    | _ -> None
                
                match presetIndex with
                | Some index ->
                    let availablePresets = PresetSystem.listPresets (UIState.getCurrentWorld())
                    if index < availablePresets.Length then
                        let presetName = availablePresets.[index]
                        printfn "Debug: Keyboard shortcut triggered - loading preset: %s (Ctrl+Shift+%d)" presetName (index + 1)
                        
                        // Use the same logic as the preset click handler
                        match Map.tryFind presetName (UIState.getCurrentWorld()).Components.SavedPresets with
                        | Some config ->
                            printfn "Debug: Found preset config with %d displays" config.Displays.Length
                            printfn "Debug: ========== APPLYING PRESET: %s ==========" presetName
                            
                            let updatedWorld = PresetSystem.loadPreset presetName (UIState.getCurrentWorld())
                            UIState.updateWorld updatedWorld
                            
                            let mutable updatedComponents = (UIState.getCurrentWorld()).Components
                            
                            // Apply each display's settings to the physical hardware
                            for display in config.Displays do
                                printfn "Debug: Applying display %s - Position: (%d, %d), Enabled: %b, Primary: %b" 
                                        display.Id display.Position.X display.Position.Y display.IsEnabled display.IsPrimary
                                printfn "Debug: Resolution: %dx%d @ %dHz, Orientation: %A" 
                                        display.Resolution.Width display.Resolution.Height display.Resolution.RefreshRate display.Orientation
                                
                                // Update component state
                                updatedComponents <- Components.addDisplay display updatedComponents
                                
                                // Apply settings to physical display if it's enabled
                                if display.IsEnabled then
                                    let mode = { 
                                        Width = display.Resolution.Width
                                        Height = display.Resolution.Height
                                        RefreshRate = display.Resolution.RefreshRate
                                        BitsPerPixel = 32 
                                    }
                                    
                                    printfn "Debug: Applying physical display mode to %s" display.Id
                                    match WindowsDisplaySystem.applyDisplayMode display.Id mode display.Orientation with
                                    | Ok () ->
                                        printfn "Debug: Successfully applied display mode to %s" display.Id
                                        
                                        // Set as primary if specified
                                        if display.IsPrimary then
                                            printfn "Debug: Setting %s as primary display" display.Id
                                            match WindowsDisplaySystem.setPrimaryDisplay display.Id with
                                            | Ok () -> printfn "Debug: Successfully set %s as primary" display.Id
                                            | Error err -> printfn "Debug: Failed to set %s as primary: %s" display.Id err
                                            
                                    | Error err ->
                                        printfn "Debug: Failed to apply display mode to %s: %s" display.Id err
                                else
                                    printfn "Debug: Skipping disabled display %s" display.Id
                            
                            UIState.updateWorld { UIState.getCurrentWorld() with Components = updatedComponents }
                            
                            printfn "Debug: Preset application completed, refreshing UI"
                            refreshMainWindowContent ()
                            
                            printfn "Debug: Loading preset %s completed successfully via keyboard shortcut" presetName
                            
                            e.Handled <- true
                        | None ->
                            printfn "Debug: Preset %s not found!" presetName
                    else
                        printfn "Debug: No preset assigned to Ctrl+Shift+%d" (index + 1)
                | None -> ()
        )
    
    // Create the main application window
    let createMainWindow (world: World) (adapter: IPlatformAdapter) =
        let window = Window()
        window.Title <- "DisplaySwitch-Pro"
        window.Width <- 1200.0
        window.Height <- 700.0
        window.MinWidth <- 900.0
        window.MinHeight <- 600.0
        
        window.TransparencyLevelHint <- [WindowTransparencyLevel.AcrylicBlur; WindowTransparencyLevel.Blur]
        window.Background <- Brushes.Transparent
        window.ExtendClientAreaToDecorationsHint <- false
        window.CanResize <- true
        
        // Set window reference in UI state
        UIState.setMainWindow window
        
        // Setup keyboard shortcuts
        setupKeyboardShortcuts window
        
        // Create and set the main content
        let content = MainContentPanel.createMainContentPanel world adapter
        window.Content <- content
        
        window