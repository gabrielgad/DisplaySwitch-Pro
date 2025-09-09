namespace DisplaySwitchPro

open System

// Simple application state management (replaces ECS)
type AppState = {
    ConnectedDisplays: Map<DisplayId, DisplayInfo>
    CurrentConfiguration: DisplayConfiguration option
    SavedPresets: Map<string, DisplayConfiguration>
    LastUpdate: DateTime
}

module AppState =
    let empty = {
        ConnectedDisplays = Map.empty
        CurrentConfiguration = None
        SavedPresets = Map.empty
        LastUpdate = DateTime.Now
    }
    
    // Display management
    let addDisplay (display: DisplayInfo) (state: AppState) =
        { state with 
            ConnectedDisplays = Map.add display.Id display state.ConnectedDisplays
            LastUpdate = DateTime.Now }
    
    let removeDisplay (displayId: DisplayId) (state: AppState) =
        { state with 
            ConnectedDisplays = Map.remove displayId state.ConnectedDisplays
            LastUpdate = DateTime.Now }
    
    let updateDisplays (displays: DisplayInfo list) (state: AppState) =
        let displayMap = displays |> List.fold (fun acc d -> Map.add d.Id d acc) Map.empty
        { state with 
            ConnectedDisplays = displayMap
            LastUpdate = DateTime.Now }
    
    // Configuration management
    let setCurrentConfiguration (config: DisplayConfiguration) (state: AppState) =
        { state with 
            CurrentConfiguration = Some config
            LastUpdate = DateTime.Now }
    
    // Preset management
    let savePreset (name: string) (config: DisplayConfiguration) (state: AppState) =
        let namedConfig = { config with Name = name; CreatedAt = DateTime.Now }
        printfn "[DEBUG] AppState: Saving preset '%s' with %d displays" name namedConfig.Displays.Length
        { state with 
            SavedPresets = Map.add name namedConfig state.SavedPresets
            LastUpdate = DateTime.Now }
    
    let loadPreset (name: string) (state: AppState) : AppState option =
        match Map.tryFind name state.SavedPresets with
        | Some config ->
            Some { state with 
                     CurrentConfiguration = Some config
                     LastUpdate = DateTime.Now }
        | None -> None
    
    let listPresets (state: AppState) : string list =
        state.SavedPresets |> Map.keys |> List.ofSeq
    
    let getPreset (name: string) (state: AppState) : DisplayConfiguration option =
        Map.tryFind name state.SavedPresets
    
    let deletePreset (name: string) (state: AppState) : AppState =
        printfn "[DEBUG] AppState: Deleting preset '%s'" name
        { state with 
            SavedPresets = Map.remove name state.SavedPresets
            LastUpdate = DateTime.Now }
    
    let hasPreset (name: string) (state: AppState) : bool =
        Map.containsKey name state.SavedPresets