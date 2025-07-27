namespace DisplaySwitchPro

open System

// World represents the complete state of our ECS system
type World = {
    Components: Components
    LastUpdate: DateTime
}

module World =
    let create () = {
        Components = Components.empty
        LastUpdate = DateTime.Now
    }
    
    let processEvent (event: DisplayEvent) (world: World) : World =
        let updatedComponents = Components.addEvent event world.Components
        
        let finalComponents = 
            match event with
            | DisplayDetected display ->
                Components.addDisplay display updatedComponents
            | DisplayDisconnected displayId ->
                // Remove display from connected displays
                { updatedComponents with 
                    ConnectedDisplays = Map.remove displayId updatedComponents.ConnectedDisplays }
            | DisplayConfigurationChanged config ->
                Components.setCurrentConfiguration config updatedComponents
            | DisplayConfigurationFailed (error, _) ->
                // Log error but don't change state
                printfn "Display configuration failed: %s" error
                updatedComponents
            | PresetSaved (name, config) ->
                Components.savePreset name config updatedComponents
            | PresetLoaded (name, config) ->
                Components.setCurrentConfiguration config updatedComponents
            | PresetLoadFailed (name, error) ->
                // Log error but don't change state
                printfn "Preset load failed for '%s': %s" name error
                updatedComponents
        
        { world with 
            Components = finalComponents
            LastUpdate = DateTime.Now }

// Display Detection System
module DisplayDetectionSystem =
    let updateWorld (adapter: IPlatformAdapter) (world: World) : World =
        let displays = adapter.GetConnectedDisplays()
        List.fold (fun w display -> 
            World.processEvent (DisplayDetected display) w) world displays

// Configuration Application System
module ConfigurationSystem =
    let applyConfiguration (adapter: IPlatformAdapter) (config: DisplayConfiguration) (world: World) : World =
        match adapter.ApplyDisplayConfiguration config with
        | Ok () ->
            World.processEvent (DisplayConfigurationChanged config) world
        | Error msg ->
            printfn $"Error applying configuration: {msg}"
            world

// Preset System  
module PresetSystem =
    let saveCurrentAsPreset (name: string) (world: World) : World =
        match world.Components.CurrentConfiguration with
        | Some config ->
            let newConfig = { config with Name = name; CreatedAt = DateTime.Now }
            World.processEvent (PresetSaved (name, newConfig)) world
        | None ->
            world
    
    let loadPreset (name: string) (world: World) : World =
        match Map.tryFind name world.Components.SavedPresets with
        | Some config ->
            World.processEvent (PresetLoaded (name, config)) world
        | None ->
            world
    
    let listPresets (world: World) : string list =
        world.Components.SavedPresets |> Map.keys |> List.ofSeq