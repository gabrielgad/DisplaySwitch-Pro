namespace DisplaySwitchPro

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Security.Cryptography

// JSON converter for F# discriminated union DisplayOrientation
type DisplayOrientationJsonConverter() =
    inherit JsonConverter<DisplayOrientation>()
    
    override _.Read(reader: byref<Utf8JsonReader>, typeToConvert: Type, options: JsonSerializerOptions) =
        let value = reader.GetString()
        match value with
        | "Landscape" -> Landscape
        | "Portrait" -> Portrait
        | "LandscapeFlipped" -> LandscapeFlipped
        | "PortraitFlipped" -> PortraitFlipped
        | _ -> failwith (sprintf "Unknown DisplayOrientation value: %s" value)
    
    override _.Write(writer: Utf8JsonWriter, value: DisplayOrientation, options: JsonSerializerOptions) =
        let stringValue = 
            match value with
            | Landscape -> "Landscape"
            | Portrait -> "Portrait"
            | LandscapeFlipped -> "LandscapeFlipped"
            | PortraitFlipped -> "PortraitFlipped"
        writer.WriteStringValue(stringValue)

// JSON converter for F# Map with tuple keys Map<(int * int), int list>
type GroupedResolutionsJsonConverter() =
    inherit JsonConverter<Map<(int * int), int list>>()
    
    override _.Read(reader: byref<Utf8JsonReader>, typeToConvert: Type, options: JsonSerializerOptions) =
        if reader.TokenType <> JsonTokenType.StartArray then
            failwith "Expected JSON array for GroupedResolutions"
        
        let mutable map = Map.empty
        reader.Read() |> ignore // Move past StartArray
        
        while reader.TokenType <> JsonTokenType.EndArray do
            if reader.TokenType <> JsonTokenType.StartObject then
                failwith "Expected JSON object for resolution group"
            
            reader.Read() |> ignore // Move past StartObject
            
            let mutable width = 0
            let mutable height = 0
            let mutable refreshRates = []
            
            while reader.TokenType <> JsonTokenType.EndObject do
                if reader.TokenType = JsonTokenType.PropertyName then
                    let propertyName = reader.GetString()
                    reader.Read() |> ignore // Move past property name
                    
                    match propertyName with
                    | "width" -> width <- reader.GetInt32()
                    | "height" -> height <- reader.GetInt32()
                    | "refreshRates" ->
                        if reader.TokenType <> JsonTokenType.StartArray then
                            failwith "Expected array for refreshRates"
                        reader.Read() |> ignore // Move past StartArray
                        let mutable rates = []
                        while reader.TokenType <> JsonTokenType.EndArray do
                            rates <- reader.GetInt32() :: rates
                            reader.Read() |> ignore
                        refreshRates <- List.rev rates
                    | _ -> reader.Skip()
                
                reader.Read() |> ignore
            
            map <- Map.add (width, height) refreshRates map
            reader.Read() |> ignore // Move past EndObject
        
        map
    
    override _.Write(writer: Utf8JsonWriter, value: Map<(int * int), int list>, options: JsonSerializerOptions) =
        writer.WriteStartArray()
        
        for KeyValue((width, height), refreshRates) in value do
            writer.WriteStartObject()
            writer.WriteNumber("width", width)
            writer.WriteNumber("height", height)
            writer.WritePropertyName("refreshRates")
            writer.WriteStartArray()
            for rate in refreshRates do
                writer.WriteNumberValue(rate)
            writer.WriteEndArray()
            writer.WriteEndObject()
        
        writer.WriteEndArray()

// Preset management with disk persistence
module PresetManager =
    
    // Configure JSON serializer options with F# discriminated union converters
    let private getJsonSerializerOptions() =
        let options = JsonSerializerOptions(WriteIndented = true)
        options.Converters.Add(DisplayOrientationJsonConverter())
        options.Converters.Add(GroupedResolutionsJsonConverter())
        options
    
    // File path for storing presets
    let private getPresetFilePath() =
        let appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplaySwitch-Pro")
        Directory.CreateDirectory(appFolder) |> ignore
        Path.Combine(appFolder, "display-presets.json")
    
    // Load presets from disk with error recovery
    let loadPresetsFromDisk() : Map<string, DisplayConfiguration> =
        try
            let filePath = getPresetFilePath()
            if File.Exists(filePath) then
                try
                    let json = File.ReadAllText(filePath)
                    if String.IsNullOrWhiteSpace(json) then
                        printfn "[WARNING] Preset file is empty, starting with empty preset collection"
                        Map.empty
                    else
                        let presets = JsonSerializer.Deserialize<DisplayConfiguration[]>(json, getJsonSerializerOptions())
                        if presets = null then
                            printfn "[WARNING] Failed to deserialize presets, starting with empty collection"
                            Map.empty
                        else
                            printfn "[DEBUG] Successfully loaded %d presets from disk" presets.Length
                            
                            // Validate loaded presets and filter out invalid ones
                            let validPresets = 
                                presets 
                                |> Array.choose (fun preset ->
                                    match DisplayValidation.validateConfiguration preset with
                                    | Ok validPreset -> 
                                        Some validPreset
                                    | Error err -> 
                                        printfn "[WARNING] Skipping invalid preset '%s': %s" preset.Name err
                                        None)
                            
                            if validPresets.Length < presets.Length then
                                printfn "[WARNING] Filtered out %d invalid presets, %d valid presets remain" 
                                    (presets.Length - validPresets.Length) validPresets.Length
                            
                            validPresets 
                            |> Array.fold (fun acc preset -> Map.add preset.Name preset acc) Map.empty
                with
                | :? System.Text.Json.JsonException as jsonEx ->
                    printfn "[ERROR] JSON parsing error: %s" jsonEx.Message
                    // Try to load from backup
                    let backupPath = filePath + ".backup"
                    if File.Exists(backupPath) then
                        printfn "[DEBUG] Attempting to restore from backup..."
                        try
                            let backupJson = File.ReadAllText(backupPath)
                            let backupPresets = JsonSerializer.Deserialize<DisplayConfiguration[]>(backupJson, getJsonSerializerOptions())
                            printfn "[SUCCESS] Restored %d presets from backup" backupPresets.Length
                            backupPresets 
                            |> Array.fold (fun acc preset -> Map.add preset.Name preset acc) Map.empty
                        with
                        | ex -> 
                            printfn "[ERROR] Backup restore failed: %s" ex.Message
                            Map.empty
                    else
                        printfn "[WARNING] No backup available, starting with empty preset collection"
                        Map.empty
            else
                printfn "[DEBUG] No preset file found, starting with empty collection"
                Map.empty
        with
        | ex -> 
            printfn "[ERROR] Failed to load presets from disk: %s" ex.Message
            Map.empty
    
    // Create backup of existing presets file
    let private createBackup filePath =
        try
            if File.Exists(filePath) then
                let backupPath = filePath + ".backup"
                File.Copy(filePath, backupPath, true)
                printfn "[DEBUG] Created backup: %s" backupPath
                Ok ()
            else
                Ok () // No existing file to backup
        with
        | ex -> 
            printfn "[WARNING] Failed to create backup: %s" ex.Message
            Ok () // Don't fail the save operation if backup fails

    // Save presets to disk with backup and validation
    let savePresetsToDisk (presets: Map<string, DisplayConfiguration>) =
        try
            let filePath = getPresetFilePath()
            let presetArray = presets |> Map.values |> Seq.toArray
            
            // Validate presets before saving
            let validationErrors = 
                presetArray 
                |> Array.choose (fun preset ->
                    match DisplayValidation.validateConfiguration preset with
                    | Ok _ -> None
                    | Error err -> Some (sprintf "Preset '%s': %s" preset.Name err))
            
            if validationErrors.Length > 0 then
                let errorMsg = sprintf "Invalid presets found: %s" (String.concat "; " validationErrors)
                printfn "[ERROR] %s" errorMsg
                Error errorMsg
            else
                // Create backup before saving
                match createBackup filePath with
                | Ok () ->
                    let options = getJsonSerializerOptions()
                    let json = JsonSerializer.Serialize(presetArray, options)
                    
                    // Write to temporary file first, then move to final location
                    let tempPath = filePath + ".tmp"
                    File.WriteAllText(tempPath, json)
                    
                    // Verify the temp file was written correctly
                    let writtenSize = (new FileInfo(tempPath)).Length
                    if writtenSize = 0L then
                        File.Delete(tempPath)
                        Error "Failed to write preset data (empty file)"
                    else
                        // Delete existing file if it exists, then move temp file
                        if File.Exists(filePath) then
                            File.Delete(filePath)
                        File.Move(tempPath, filePath)
                        printfn "[DEBUG] Successfully saved %d presets to disk (%d bytes)" presetArray.Length writtenSize
                        Ok ()
                | Error err -> Error err
        with
        | ex -> 
            printfn "[ERROR] Failed to save presets to disk: %s" ex.Message
            Error (sprintf "Failed to save presets: %s" ex.Message)
    
    // Create preset from current display state with comprehensive validation
    let createPresetFromCurrentState (name: string) (displays: DisplayInfo list) =
        printfn "[DEBUG] Creating preset '%s' from %d displays" name displays.Length
        
        // Log display information for debugging
        displays |> List.iteri (fun i d ->
            printfn "[DEBUG] Preset Display %d: %s (%s) - %dx%d@%dHz at (%d,%d), Primary=%b, Enabled=%b, Orientation=%A"
                i d.Id d.Name d.Resolution.Width d.Resolution.Height d.Resolution.RefreshRate
                d.Position.X d.Position.Y d.IsPrimary d.IsEnabled d.Orientation)
        
        // Use the enhanced helper function to create configuration with metadata
        DisplayHelpers.createDisplayConfiguration name displays
    
    // Apply preset to Windows display configuration with comprehensive restoration and graceful disconnected display handling
    let applyPreset (preset: DisplayConfiguration) =
        try
            printfn "[DEBUG] ========== Applying Preset: %s =========="  preset.Name
            printfn "[DEBUG] Preset created: %s" (preset.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))
            printfn "[DEBUG] Preset hash: %s" (preset.ConfigurationHash |> Option.defaultValue "None")
            printfn "[DEBUG] Total displays in preset: %d" preset.Displays.Length
            
            // Get currently connected displays for filtering
            let connectedDisplays = DisplayDetection.getConnectedDisplays() |> List.map (fun d -> d.Id) |> Set.ofList
            
            // Filter preset displays to only those currently connected
            let availableDisplays = preset.Displays |> List.filter (fun d -> Set.contains d.Id connectedDisplays)
            let unavailableDisplays = preset.Displays |> List.filter (fun d -> not (Set.contains d.Id connectedDisplays))
            
            if not unavailableDisplays.IsEmpty then
                printfn "[WARNING] Preset includes %d disconnected displays (will be skipped):" unavailableDisplays.Length
                unavailableDisplays |> List.iter (fun d -> printfn "[WARNING]   - %s (%s)" d.Id d.Name)
            
            printfn "[DEBUG] Processing %d available displays from preset" availableDisplays.Length
            
            // Detailed logging of available preset displays
            availableDisplays |> List.iteri (fun i d ->
                printfn "[DEBUG] Available Display %d: %s (%s)" i d.Id d.Name
                printfn "[DEBUG]   Resolution: %dx%d @ %dHz" d.Resolution.Width d.Resolution.Height d.Resolution.RefreshRate
                printfn "[DEBUG]   Position: (%d, %d)" d.Position.X d.Position.Y
                printfn "[DEBUG]   Primary: %b, Enabled: %b, Orientation: %A" d.IsPrimary d.IsEnabled d.Orientation)
            
            // Group available displays by their target state
            let enabledDisplays = availableDisplays |> List.filter (fun d -> d.IsEnabled)
            let disabledDisplays = availableDisplays |> List.filter (fun d -> not d.IsEnabled)
            
            printfn "[DEBUG] Available displays to enable: %d, Available displays to disable: %d" enabledDisplays.Length disabledDisplays.Length
            
            // Step 1: Disable displays first (to avoid conflicts)
            printfn "[DEBUG] Step 1: Disabling displays..."
            for display in disabledDisplays do
                printfn "[DEBUG] Disabling display %s (%s)" display.Id display.Name
                match DisplayControl.setDisplayEnabled display.Id false with
                | Ok () -> printfn "[SUCCESS] Disabled %s" display.Id
                | Error err -> printfn "[WARNING] Failed to disable %s: %s" display.Id err
            
            // Step 2: Enable and configure displays
            printfn "[DEBUG] Step 2: Enabling and configuring displays..."
            for display in enabledDisplays do
                printfn "[DEBUG] Processing display %s (%s)" display.Id display.Name
                
                // First enable the display
                match DisplayControl.setDisplayEnabled display.Id true with
                | Ok () -> 
                    printfn "[SUCCESS] Enabled %s" display.Id
                    
                    // Apply display mode (resolution, refresh rate, orientation)
                    let mode = {
                        Width = display.Resolution.Width
                        Height = display.Resolution.Height  
                        RefreshRate = display.Resolution.RefreshRate
                        BitsPerPixel = 32
                    }
                    
                    printfn "[DEBUG] Applying mode %dx%d@%dHz, orientation %A to %s" 
                        mode.Width mode.Height mode.RefreshRate display.Orientation display.Id
                    
                    match DisplayControl.applyDisplayMode display.Id mode display.Orientation with
                    | Ok () -> printfn "[SUCCESS] Applied display mode for %s" display.Id
                    | Error err -> printfn "[ERROR] Failed to apply mode for %s: %s" display.Id err
                        
                | Error err -> printfn "[ERROR] Failed to enable %s: %s" display.Id err
            
            // Step 3: Set primary display (must be done after all displays are enabled)
            printfn "[DEBUG] Step 3: Setting primary display..."
            match enabledDisplays |> List.tryFind (fun d -> d.IsPrimary) with
            | Some primaryDisplay ->
                printfn "[DEBUG] Setting %s as primary display" primaryDisplay.Id
                match DisplayControl.setPrimaryDisplay primaryDisplay.Id with
                | Ok () -> printfn "[SUCCESS] Set %s as primary" primaryDisplay.Id
                | Error err -> printfn "[ERROR] Failed to set %s as primary: %s" primaryDisplay.Id err
            | None ->
                printfn "[WARNING] No primary display specified in preset"
            
            // Step 4: Apply display positions using the existing positioning system
            printfn "[DEBUG] Step 4: Applying display positions..."
            let displayPositions = 
                enabledDisplays 
                |> List.map (fun d -> (d.Id, d.Position))
            
            if not displayPositions.IsEmpty then
                printfn "[DEBUG] Applying positions for %d displays:" displayPositions.Length
                displayPositions |> List.iter (fun (id, pos) ->
                    printfn "[DEBUG]   %s -> (%d, %d)" id pos.X pos.Y)
                
                match DisplayControl.applyMultipleDisplayPositions displayPositions with
                | Ok () -> printfn "[SUCCESS] Applied all display positions"
                | Error err -> printfn "[ERROR] Failed to apply positions: %s" err
            else
                printfn "[DEBUG] No enabled displays to position"
            
            // Step 5: Final validation
            printfn "[DEBUG] Step 5: Validating preset application..."
            // Note: Skip validation here to avoid recursion - can be added later if needed
            printfn "[DEBUG] Preset application validation skipped to avoid recursion"
            
            printfn "[DEBUG] ========== Preset Application Complete ==========" 
            Ok ()
        with
        | ex ->
            printfn "[ERROR] Exception applying preset %s: %s" preset.Name ex.Message
            Error (sprintf "Exception applying preset: %s" ex.Message)
    
    // Get current display configuration from actual Windows state
    let getCurrentConfiguration() =
        try
            printfn "[DEBUG] Capturing current Windows display configuration..."
            let displays = DisplayDetection.getConnectedDisplays()
            printfn "[DEBUG] Found %d connected displays" displays.Length
            
            // Log each display's current state
            displays |> List.iter (fun d ->
                printfn "[DEBUG] Display %s: %dx%d at (%d,%d), Primary=%b, Enabled=%b, Orientation=%A" 
                    d.Id d.Resolution.Width d.Resolution.Height d.Position.X d.Position.Y d.IsPrimary d.IsEnabled d.Orientation)
            
            // Create configuration with proper metadata
            let config = DisplayHelpers.createDisplayConfiguration "Current" displays
            printfn "[DEBUG] Generated configuration hash: %s" (config.ConfigurationHash |> Option.defaultValue "None")
            config
        with
        | ex ->
            printfn "[ERROR] Failed to get current configuration: %s" ex.Message
            // Return empty configuration as fallback
            DisplayHelpers.createDisplayConfiguration "Current" []
    
    // Delete a preset from both memory and disk
    let deletePreset (presetName: string) (presets: Map<string, DisplayConfiguration>) =
        try
            if Map.containsKey presetName presets then
                let updatedPresets = Map.remove presetName presets
                match savePresetsToDisk updatedPresets with
                | Ok () -> 
                    printfn "[DEBUG] Successfully deleted preset '%s' and saved to disk" presetName
                    Ok updatedPresets
                | Error err -> 
                    printfn "[ERROR] Failed to save presets after deletion: %s" err
                    Error (sprintf "Failed to save after deleting preset: %s" err)
            else
                let error = sprintf "Preset '%s' does not exist" presetName
                printfn "[ERROR] %s" error
                Error error
        with
        | ex -> 
            let error = sprintf "Exception deleting preset '%s': %s" presetName ex.Message
            printfn "[ERROR] %s" error
            Error error

    // Enhanced preset validation with more comprehensive checks
    let validatePreset (preset: DisplayConfiguration) =
        printfn "[DEBUG] Validating preset '%s'..." preset.Name
        
        try
            // Check 1: Basic preset structure validation
            if String.IsNullOrWhiteSpace(preset.Name) then
                Error "Preset name cannot be empty"
            elif List.isEmpty preset.Displays then
                Error "Preset must contain at least one display"
            else
                // Check 2: Verify all required displays are connected
                let connectedDisplays = DisplayDetection.getConnectedDisplays() |> List.map (fun d -> d.Id) |> Set.ofList
                let presetDisplays = preset.Displays |> List.map (fun d -> d.Id) |> Set.ofList
                let missingDisplays = Set.difference presetDisplays connectedDisplays
                
                if not (Set.isEmpty missingDisplays) then
                    let missingList = Set.toList missingDisplays
                    let error = sprintf "Preset requires disconnected displays: %s" (String.concat ", " missingList)
                    printfn "[WARNING] %s - allowing graceful handling" error
                    // Don't fail validation for missing displays - allow graceful handling
                    printfn "[DEBUG] Proceeding with available displays only"
                
                printfn "[DEBUG] Validating display modes for connected displays..."
                
                // Check 3: Validate display modes are supported (only for connected displays)
                let connectedDisplayMap = 
                    DisplayDetection.getConnectedDisplays() 
                    |> List.map (fun d -> (d.Id, d)) 
                    |> Map.ofList
                
                let modeValidationErrors = 
                    preset.Displays
                    |> List.choose (fun presetDisplay ->
                        if not presetDisplay.IsEnabled then 
                            None  // Skip disabled displays
                        elif not (Set.contains presetDisplay.Id connectedDisplays) then
                            None  // Skip disconnected displays - handle gracefully
                        else
                            match Map.tryFind presetDisplay.Id connectedDisplayMap with
                            | None -> Some (sprintf "Display %s not found in connected displays map" presetDisplay.Id)
                            | Some connectedDisplay ->
                                match connectedDisplay.Capabilities with
                                | None -> 
                                    printfn "[WARNING] No capabilities data for %s - assuming mode is valid" presetDisplay.Id
                                    None
                                | Some capabilities ->
                                    let targetMode = {
                                        Width = presetDisplay.Resolution.Width
                                        Height = presetDisplay.Resolution.Height
                                        RefreshRate = presetDisplay.Resolution.RefreshRate
                                        BitsPerPixel = 32
                                    }
                                    let modeSupported = 
                                        capabilities.AvailableModes
                                        |> List.exists (fun mode ->
                                            mode.Width = targetMode.Width &&
                                            mode.Height = targetMode.Height &&
                                            mode.RefreshRate = targetMode.RefreshRate)
                                    
                                    if modeSupported then 
                                        None
                                    else 
                                        Some (sprintf "Mode %dx%d@%dHz not supported by %s" 
                                            targetMode.Width targetMode.Height targetMode.RefreshRate presetDisplay.Id))
                
                if not modeValidationErrors.IsEmpty then
                    let error = sprintf "Unsupported display modes: %s" (String.concat "; " modeValidationErrors)
                    printfn "[ERROR] %s" error
                    Error error
                else
                    // Check 4: Ensure exactly one primary display is specified among enabled displays
                    let enabledDisplays = preset.Displays |> List.filter (fun d -> d.IsEnabled)
                    let connectedEnabledDisplays = enabledDisplays |> List.filter (fun d -> Set.contains d.Id connectedDisplays)
                    let primaryDisplays = connectedEnabledDisplays |> List.filter (fun d -> d.IsPrimary)
                    
                    match primaryDisplays.Length with
                    | 0 -> 
                        printfn "[WARNING] No primary display specified among connected displays - Windows will choose one"
                        Ok ()
                    | 1 -> 
                        printfn "[DEBUG] Primary display validation passed"
                        Ok ()
                    | n -> 
                        let error = sprintf "Multiple primary displays specified (%d) among connected displays - only one allowed" n
                        printfn "[ERROR] %s" error
                        Error error
        with
        | ex ->
            let error = sprintf "Exception during preset validation: %s" ex.Message
            printfn "[ERROR] %s" error
            Error error
    
    // Utility functions for preset management
    
    // Get preset statistics
    let getPresetStatistics (presets: Map<string, DisplayConfiguration>) =
        let totalPresets = Map.count presets
        let totalDisplaysInPresets = presets |> Map.values |> Seq.sumBy (fun p -> p.Displays.Length)
        let avgDisplaysPerPreset = if totalPresets > 0 then float totalDisplaysInPresets / float totalPresets else 0.0
        let presetSeq = presets |> Map.values |> Seq.toList
        let oldestPreset = if List.isEmpty presetSeq then None else Some (presetSeq |> List.minBy (fun p -> p.CreatedAt))
        let newestPreset = if List.isEmpty presetSeq then None else Some (presetSeq |> List.maxBy (fun p -> p.CreatedAt))
        
        {| TotalPresets = totalPresets
           TotalDisplaysInPresets = totalDisplaysInPresets
           AverageDisplaysPerPreset = avgDisplaysPerPreset
           OldestPreset = oldestPreset
           NewestPreset = newestPreset |}
    
    // Export presets to JSON string (for backup/sharing)
    let exportPresetsToJson (presets: Map<string, DisplayConfiguration>) =
        try
            let presetArray = presets |> Map.values |> Seq.toArray
            let options = getJsonSerializerOptions()
            let json = JsonSerializer.Serialize(presetArray, options)
            Ok json
        with
        | ex -> Error (sprintf "Failed to export presets: %s" ex.Message)
    
    // Import presets from JSON string
    let importPresetsFromJson (json: string) (existingPresets: Map<string, DisplayConfiguration>) =
        try
            if String.IsNullOrWhiteSpace(json) then
                Error "Import JSON is empty"
            else
                let importedPresets = JsonSerializer.Deserialize<DisplayConfiguration[]>(json, getJsonSerializerOptions())
                if importedPresets = null then
                    Error "Failed to parse import JSON"
                else
                    // Validate imported presets
                    let validPresets = 
                        importedPresets 
                        |> Array.choose (fun preset ->
                            match DisplayValidation.validateConfiguration preset with
                            | Ok validPreset -> Some validPreset
                            | Error err -> 
                                printfn "[WARNING] Skipping invalid imported preset '%s': %s" preset.Name err
                                None)
                    
                    if validPresets.Length = 0 then
                        Error "No valid presets found in import data"
                    else
                        // Merge with existing presets (imported presets override existing ones with same name)
                        let mergedPresets = 
                            (existingPresets, validPresets) 
                            ||> Array.fold (fun acc preset -> Map.add preset.Name preset acc)
                        
                        printfn "[SUCCESS] Imported %d valid presets (total: %d)" validPresets.Length (Map.count mergedPresets)
                        Ok (mergedPresets, validPresets.Length)
        with
        | ex -> Error (sprintf "Exception importing presets: %s" ex.Message)
    
    // Duplicate/clone a preset with a new name
    let duplicatePreset (originalName: string) (newName: string) (presets: Map<string, DisplayConfiguration>) =
        try
            if String.IsNullOrWhiteSpace(newName) then
                Error "New preset name cannot be empty"
            elif Map.containsKey newName presets then
                Error (sprintf "Preset '%s' already exists" newName)
            else
                match Map.tryFind originalName presets with
                | None -> Error (sprintf "Original preset '%s' not found" originalName)
                | Some originalPreset ->
                    let duplicatedPreset = {
                        originalPreset with 
                            Name = newName
                            CreatedAt = DateTime.Now
                            ConfigurationHash = None // Will be recalculated
                    }
                    let finalPreset = DisplayHelpers.createDisplayConfiguration newName duplicatedPreset.Displays
                    let updatedPresets = Map.add newName finalPreset presets
                    Ok updatedPresets
        with
        | ex -> Error (sprintf "Exception duplicating preset: %s" ex.Message)