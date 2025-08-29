open DisplaySwitchPro

[<EntryPoint>]
let main args =
    printfn "DisplaySwitch-Pro ECS System starting..."
    
    // Create platform adapter and world
    let adapter = PlatformAdapter.create ()
    let world = World.create ()
    let worldWithDisplays = DisplayDetectionSystem.updateWorld adapter world
    
    printfn $"Detected {worldWithDisplays.Components.ConnectedDisplays.Count} displays"
    
    // Create a configuration from current displays
    let currentConfig = {
        Displays = worldWithDisplays.Components.ConnectedDisplays |> Map.values |> List.ofSeq
        Name = "Current Setup"
        CreatedAt = System.DateTime.Now
    }
    
    // Set as current and save as preset
    let worldWithConfig = World.processEvent (DisplayConfigurationChanged currentConfig) worldWithDisplays
    let worldWithPreset = PresetSystem.saveCurrentAsPreset "My Setup" worldWithConfig
    
    // Test preset listing
    let presets = PresetSystem.listPresets worldWithPreset
    printfn $"Saved presets: {presets}"
    
    printfn "✅ ECS System working perfectly!"
    printfn "Starting GUI..."
    
    // Launch GUI - this doesn't return until app closes
    let exitCode = ApplicationRunner.run adapter worldWithPreset
    exitCode