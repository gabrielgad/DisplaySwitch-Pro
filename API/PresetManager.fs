namespace DisplaySwitchPro

open System
open System.IO
open System.Text.Json

// Preset management with disk persistence
module PresetManager =
    
    // File path for storing presets
    let private getPresetFilePath() =
        let appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplaySwitch-Pro")
        Directory.CreateDirectory(appFolder) |> ignore
        Path.Combine(appFolder, "display-presets.json")
    
    // Load presets from disk
    let loadPresetsFromDisk() : Map<string, DisplayConfiguration> =
        try
            let filePath = getPresetFilePath()
            if File.Exists(filePath) then
                let json = File.ReadAllText(filePath)
                let presets = JsonSerializer.Deserialize<DisplayConfiguration[]>(json)
                presets 
                |> Array.fold (fun acc preset -> Map.add preset.Name preset acc) Map.empty
            else
                Map.empty
        with
        | ex -> 
            printfn "[ERROR] Failed to load presets from disk: %s" ex.Message
            Map.empty
    
    // Save presets to disk
    let savePresetsToDisk (presets: Map<string, DisplayConfiguration>) =
        try
            let filePath = getPresetFilePath()
            let presetArray = presets |> Map.values |> Seq.toArray
            let options = JsonSerializerOptions(WriteIndented = true)
            let json = JsonSerializer.Serialize(presetArray, options)
            File.WriteAllText(filePath, json)
            printfn "[DEBUG] Saved %d presets to disk" presetArray.Length
            Ok ()
        with
        | ex -> 
            printfn "[ERROR] Failed to save presets to disk: %s" ex.Message
            Error (sprintf "Failed to save presets: %s" ex.Message)
    
    // Create preset from current display state
    let createPresetFromCurrentState (name: string) (displays: DisplayInfo list) =
        {
            Displays = displays
            Name = name
            CreatedAt = DateTime.Now
        }
    
    // Apply preset to Windows display configuration
    let applyPreset (preset: DisplayConfiguration) =
        try
            printfn "Applying preset: %s (%d displays)" preset.Name preset.Displays.Length
            
            // Group displays by their enable state
            let enabledDisplays = preset.Displays |> List.filter (fun d -> d.IsEnabled)
            let disabledDisplays = preset.Displays |> List.filter (fun d -> not d.IsEnabled)
            
            // Step 1: Enable displays that should be enabled
            for display in enabledDisplays do
                printfn "Enabling display %s" display.Id
                match DisplayControl.setDisplayEnabled display.Id true with
                | Ok () -> 
                    printfn "Successfully enabled %s" display.Id
                    
                    // Apply mode settings (resolution, refresh rate, orientation)
                    let mode = {
                        Width = display.Resolution.Width
                        Height = display.Resolution.Height  
                        RefreshRate = display.Resolution.RefreshRate
                        BitsPerPixel = 32
                    }
                    
                    match DisplayControl.applyDisplayMode display.Id mode display.Orientation with
                    | Ok () -> printfn "Applied mode settings for %s" display.Id
                    | Error err -> printfn "Failed to apply mode for %s: %s" display.Id err
                    
                    // Set as primary if specified
                    if display.IsPrimary then
                        match DisplayControl.setPrimaryDisplay display.Id with
                        | Ok () -> printfn "Set %s as primary" display.Id
                        | Error err -> printfn "Failed to set %s as primary: %s" display.Id err
                        
                | Error err -> printfn "Failed to enable %s: %s" display.Id err
            
            // Step 2: Apply positions for all enabled displays
            let displayPositions = 
                enabledDisplays 
                |> List.map (fun d -> (d.Id, d.Position))
            
            if not displayPositions.IsEmpty then
                match DisplayControl.applyMultipleDisplayPositions displayPositions with
                | Ok () -> printfn "Successfully applied all display positions"
                | Error err -> printfn "Failed to apply positions: %s" err
            
            // Step 3: Disable displays that should be disabled
            for display in disabledDisplays do
                printfn "Disabling display %s" display.Id
                match DisplayControl.setDisplayEnabled display.Id false with
                | Ok () -> printfn "Successfully disabled %s" display.Id
                | Error err -> printfn "Failed to disable %s: %s" display.Id err
            
            Ok ()
        with
        | ex ->
            Error (sprintf "Exception applying preset: %s" ex.Message)
    
    // Get current display configuration from Windows
    let getCurrentConfiguration() =
        let displays = DisplayDetection.getConnectedDisplays()
        createPresetFromCurrentState "Current" displays
    
    // Validate preset can be applied (check if displays are still connected)
    let validatePreset (preset: DisplayConfiguration) =
        let connectedDisplays = DisplayDetection.getConnectedDisplays() |> List.map (fun d -> d.Id) |> Set.ofList
        let presetDisplays = preset.Displays |> List.map (fun d -> d.Id) |> Set.ofList
        let missingDisplays = Set.difference presetDisplays connectedDisplays
        
        if Set.isEmpty missingDisplays then
            Ok ()
        else
            let missingList = Set.toList missingDisplays
            Error (sprintf "Preset requires disconnected displays: %s" (String.concat ", " missingList))