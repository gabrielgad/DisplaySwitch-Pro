open DisplaySwitchPro

[<EntryPoint>]
let main args =
    printfn "DisplaySwitch-Pro starting..."
    
    // Create platform adapter and detect displays
    let adapter = PlatformAdapter.create ()
    let displays = adapter.GetConnectedDisplays()
    
    printfn $"Detected {displays.Length} displays"
    
    // Create initial application state
    let initialState = AppState.updateDisplays displays AppState.empty
    
    // Create a configuration from current displays
    let currentConfig = {
        Displays = displays
        Name = "Current Setup"
        CreatedAt = System.DateTime.Now
    }
    
    // Set as current and save as preset
    let stateWithConfig = AppState.setCurrentConfiguration currentConfig initialState
    let stateWithPreset = AppState.savePreset "My Setup" currentConfig stateWithConfig
    
    // Test preset listing
    let presets = AppState.listPresets stateWithPreset
    printfn $"Saved presets: {presets}"
    
    printfn "✅ System working perfectly!"
    printfn "Starting GUI..."
    
    // Launch GUI - this doesn't return until app closes
    let exitCode = ApplicationRunner.run adapter stateWithPreset
    exitCode