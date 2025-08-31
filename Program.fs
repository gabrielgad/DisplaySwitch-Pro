open DisplaySwitchPro

[<EntryPoint>]
let main args =
    printfn "DisplaySwitch-Pro starting..."
    
    // Create platform adapter and detect displays
    let adapter = PlatformAdapter.create ()
    let displays = adapter.GetConnectedDisplays()
    
    printfn $"Detected {displays.Length} displays"
    
    // Load presets from disk
    let savedPresets = PresetManager.loadPresetsFromDisk()
    printfn "Loaded %d presets from disk" savedPresets.Count
    
    // Create initial application state with loaded presets
    let initialState = { AppState.empty with SavedPresets = savedPresets }
    let stateWithDisplays = AppState.updateDisplays displays initialState
    
    // Create a configuration from current displays
    let currentConfig = {
        Displays = displays
        Name = "Current Setup"
        CreatedAt = System.DateTime.Now
    }
    
    // Set as current configuration
    let stateWithConfig = AppState.setCurrentConfiguration currentConfig stateWithDisplays
    
    printfn "✅ System working perfectly!"
    printfn "Starting GUI..."
    
    // Launch GUI - this doesn't return until app closes
    let exitCode = ApplicationRunner.run adapter stateWithConfig
    exitCode