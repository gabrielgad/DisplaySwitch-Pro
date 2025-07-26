open DisplaySwitchPro
open System

// Test the visual display arrangement functionality
[<EntryPoint>]
let main args =
    printfn "Testing Visual Display Arrangement..."
    
    // Create test world
    let adapter = PlatformAdapter.create()
    let world = World.create()
    let worldWithDisplays = DisplayDetectionSystem.updateWorld adapter world
    
    printfn $"Detected {worldWithDisplays.Components.ConnectedDisplays.Count} displays"
    
    // Simulate moving a display
    let display2 = worldWithDisplays.Components.ConnectedDisplays.["DISPLAY2"]
    let movedDisplay2 = { display2 with Position = { X = 100; Y = 200 } }
    let worldAfterMove = { worldWithDisplays with 
        Components = Components.addDisplay movedDisplay2 worldWithDisplays.Components }
    
    // Save as preset
    let config = {
        Displays = worldAfterMove.Components.ConnectedDisplays |> Map.values |> List.ofSeq
        Name = "Test Layout"
        CreatedAt = DateTime.Now
    }
    let worldWithPreset = PresetSystem.saveCurrentAsPreset "Test Layout" worldAfterMove
    
    // Verify preset contains moved position
    match Map.tryFind "Test Layout" worldWithPreset.Components.SavedPresets with
    | Some preset ->
        let savedDisplay2 = preset.Displays |> List.find (fun d -> d.Id = "DISPLAY2")
        printfn $"Display2 saved position: ({savedDisplay2.Position.X}, {savedDisplay2.Position.Y})"
        if savedDisplay2.Position.X = 100 && savedDisplay2.Position.Y = 200 then
            printfn "✅ Visual position correctly saved to preset!"
        else
            printfn "❌ Position not saved correctly"
    | None ->
        printfn "❌ Preset not found"
    
    0