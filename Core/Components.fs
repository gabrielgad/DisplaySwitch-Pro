namespace DisplaySwitchPro

open System
open System.Collections.Generic

// Simple component storage for our ECS
type Components = {
    ConnectedDisplays: Map<DisplayId, DisplayInfo>
    CurrentConfiguration: DisplayConfiguration option
    SavedPresets: Map<string, DisplayConfiguration>
    Events: DisplayEvent list
}

module Components =
    let empty = {
        ConnectedDisplays = Map.empty
        CurrentConfiguration = None
        SavedPresets = Map.empty
        Events = []
    }
    
    let addDisplay (display: DisplayInfo) (components: Components) =
        { components with 
            ConnectedDisplays = Map.add display.Id display components.ConnectedDisplays }
    
    let setCurrentConfiguration (config: DisplayConfiguration) (components: Components) =
        { components with CurrentConfiguration = Some config }
    
    let savePreset (name: string) (config: DisplayConfiguration) (components: Components) =
        { components with 
            SavedPresets = Map.add name config components.SavedPresets }
    
    let addEvent (event: DisplayEvent) (components: Components) =
        { components with Events = event :: components.Events }