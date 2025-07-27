namespace DisplaySwitchPro

open System
open System.Runtime.InteropServices

// Platform adapter interface for display operations
type IPlatformAdapter = 
    abstract member GetConnectedDisplays: unit -> DisplayInfo list
    abstract member ApplyDisplayConfiguration: DisplayConfiguration -> Result<unit, string>

// Cross-platform implementation using .NET APIs
type CrossPlatformAdapter() =
    interface IPlatformAdapter with
        member _.GetConnectedDisplays() =
            try
                printfn "Detecting displays on %s..." (Environment.OSVersion.Platform.ToString())
                
                // Use platform-specific detection
                if Environment.OSVersion.Platform = PlatformID.Win32NT then
                    // Use Windows-specific display detection
                    WindowsDisplaySystem.getConnectedDisplays()
                else
                    // Cross-platform fallback for Linux/WSL/macOS
                    let displayEnv = Environment.GetEnvironmentVariable("DISPLAY")
                    let isX11 = not (String.IsNullOrEmpty(displayEnv))
                    
                    if isX11 then
                        printfn "X11 display detected: %s" displayEnv
                        [
                            {
                                Id = "X11_PRIMARY"
                                Name = sprintf "X11 Display (%s)" displayEnv
                                Resolution = { Width = 1920; Height = 1080; RefreshRate = 60 }
                                Position = { X = 0; Y = 0 }
                                Orientation = Landscape
                                IsPrimary = true
                                IsEnabled = true
                                Capabilities = None // X11 capabilities not implemented yet
                            }
                        ]
                    else
                        // Fallback for other platforms
                        [
                            {
                                Id = "PRIMARY"
                                Name = "Primary Display"
                                Resolution = { Width = 1920; Height = 1080; RefreshRate = 60 }
                                Position = { X = 0; Y = 0 }
                                Orientation = Landscape
                                IsPrimary = true
                                IsEnabled = true
                                Capabilities = None // Generic platform capabilities not implemented
                            }
                        ]
            with
            | ex -> 
                printfn $"Warning: Could not detect displays: {ex.Message}"
                []
                
        member _.ApplyDisplayConfiguration(config: DisplayConfiguration) =
            try
                // Mock implementation - in reality this would call OS APIs
                printfn $"Applying configuration: {config.Name}"
                for display in config.Displays do
                    printfn $"  Display {display.Id}: {display.Resolution.Width}x{display.Resolution.Height} at ({display.Position.X},{display.Position.Y})"
                Ok ()
            with
            | ex -> Error $"Failed to apply configuration: {ex.Message}"

module PlatformAdapter =
    let create () : IPlatformAdapter = 
        CrossPlatformAdapter() :> IPlatformAdapter