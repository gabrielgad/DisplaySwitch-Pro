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
                // Simple cross-platform display detection
                printfn "Detecting displays on %s..." (Environment.OSVersion.Platform.ToString())
                
                // Try to get DISPLAY environment variable for X11
                let displayEnv = Environment.GetEnvironmentVariable("DISPLAY")
                let isX11 = not (String.IsNullOrEmpty(displayEnv))
                
                if isX11 then
                    printfn "X11 display detected: %s" displayEnv
                    // In WSL/Linux with X11, we typically have one virtual display
                    // Real multi-monitor detection would require X11 interop
                    [
                        {
                            Id = "X11_PRIMARY"
                            Name = sprintf "X11 Display (%s)" displayEnv
                            Resolution = { Width = 1920; Height = 1080; RefreshRate = 60 }
                            Position = { X = 0; Y = 0 }
                            Orientation = Landscape
                            IsPrimary = true
                            IsEnabled = true
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