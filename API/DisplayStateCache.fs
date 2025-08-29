namespace DisplaySwitchPro

open System
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open System.Text.Json
open WindowsAPI

// Display state persistence for remembering display configurations
module DisplayStateCache =
    
    // Display state cache for remembering display configurations
    type DisplayStateCache = {
        DisplayId: string
        Position: Position
        Resolution: Resolution
        OrientationValue: int // Store as int instead of discriminated union
        IsPrimary: bool
        SavedAt: System.DateTime
    }
    
    // Immutable in-memory cache of display states
    let private displayStateCache = ref Map.empty<string, DisplayStateCache>
    
    // File path for persisting display states
    let private getStateCacheFilePath() =
        let appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData)
        let appFolder = Path.Combine(appDataPath, "DisplaySwitchPro")
        Directory.CreateDirectory(appFolder) |> ignore
        Path.Combine(appFolder, "display-states.json")
    
    // Load display states from file
    let private loadDisplayStates() =
        try
            let filePath = getStateCacheFilePath()
            if File.Exists(filePath) then
                let json = File.ReadAllText(filePath)
                let states = JsonSerializer.Deserialize<DisplayStateCache[]>(json)
                displayStateCache := 
                    states 
                    |> Array.fold (fun acc state -> Map.add state.DisplayId state acc) Map.empty
                printfn "[DEBUG] Loaded %d display states from cache" states.Length
        with
        | ex -> 
            printfn "[DEBUG] Failed to load display states: %s" ex.Message
    
    // Save display states to file
    let private saveDisplayStates() =
        try
            let filePath = getStateCacheFilePath()
            let states = (!displayStateCache) |> Map.values |> Seq.toArray
            let options = JsonSerializerOptions(WriteIndented = true)
            let json = JsonSerializer.Serialize(states, options)
            File.WriteAllText(filePath, json)
            printfn "[DEBUG] Saved %d display states to cache" states.Length
        with
        | ex -> 
            printfn "[DEBUG] Failed to save display states: %s" ex.Message
    
    // Convert DisplayOrientation to Windows API orientation value
    let orientationToWindows (orientation: DisplayOrientation) =
        match orientation with
        | Landscape -> WindowsAPI.DMDO.DMDO_DEFAULT
        | Portrait -> WindowsAPI.DMDO.DMDO_90
        | LandscapeFlipped -> WindowsAPI.DMDO.DMDO_180
        | PortraitFlipped -> WindowsAPI.DMDO.DMDO_270

    // Convert Windows API orientation value to DisplayOrientation
    let windowsToOrientation (windowsOrientation: uint32) =
        match windowsOrientation with
        | x when x = WindowsAPI.DMDO.DMDO_DEFAULT -> Landscape
        | x when x = WindowsAPI.DMDO.DMDO_90 -> Portrait
        | x when x = WindowsAPI.DMDO.DMDO_180 -> LandscapeFlipped
        | x when x = WindowsAPI.DMDO.DMDO_270 -> PortraitFlipped
        | _ -> Landscape // Default fallback
    
    // Convert DisplayOrientation to int for JSON serialization
    let orientationToInt (orientation: DisplayOrientation) =
        match orientation with
        | Landscape -> 0
        | Portrait -> 1
        | LandscapeFlipped -> 2
        | PortraitFlipped -> 3
    
    // Convert int to DisplayOrientation from JSON deserialization
    let intToOrientation (value: int) =
        match value with
        | 0 -> Landscape
        | 1 -> Portrait
        | 2 -> LandscapeFlipped
        | 3 -> PortraitFlipped
        | _ -> Landscape // Default fallback
    
    // Save current display state to cache
    let saveDisplayState (displayId: string) =
        try
            // Get current display information
            let mutable devMode = WindowsAPI.DEVMODE()
            devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
            
            let result = WindowsAPI.EnumDisplaySettings(displayId, -1, &devMode)
            if result then
                let state = {
                    DisplayId = displayId
                    Position = { X = devMode.dmPositionX; Y = devMode.dmPositionY }
                    Resolution = { 
                        Width = int devMode.dmPelsWidth
                        Height = int devMode.dmPelsHeight
                        RefreshRate = int devMode.dmDisplayFrequency
                    }
                    OrientationValue = orientationToInt (windowsToOrientation devMode.dmDisplayOrientation)
                    IsPrimary = (devMode.dmPositionX = 0 && devMode.dmPositionY = 0) // Simple check
                    SavedAt = System.DateTime.Now
                }
                
                displayStateCache := Map.add displayId state (!displayStateCache)
                saveDisplayStates() // Persist to file
                
                printfn "[DEBUG] Saved display state for %s: %dx%d @ %dHz at (%d, %d)" 
                        displayId state.Resolution.Width state.Resolution.Height 
                        state.Resolution.RefreshRate state.Position.X state.Position.Y
                true
            else
                printfn "[DEBUG] Failed to get current settings for %s" displayId
                false
        with
        | ex ->
            printfn "[DEBUG] Error saving display state: %s" ex.Message
            false
    
    // Get saved display state from cache
    let getSavedDisplayState (displayId: string) =
        Map.tryFind displayId (!displayStateCache)
    
    // Initialize the cache - load saved states
    let initialize() =
        loadDisplayStates()