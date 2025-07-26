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
                // For now, return mock data that represents real displays
                // In a real implementation, this would call platform-specific APIs
                // Always return multiple displays for testing the visual arrangement
                [
                    {
                        Id = "DISPLAY1"
                        Name = "Primary Monitor" 
                        Resolution = { Width = 1920; Height = 1080; RefreshRate = 60 }
                        Position = { X = 0; Y = 0 }
                        IsPrimary = true
                        IsEnabled = true
                    }
                    {
                        Id = "DISPLAY2"
                        Name = "Secondary Monitor"
                        Resolution = { Width = 1920; Height = 1080; RefreshRate = 60 }
                        Position = { X = 1920; Y = 0 }
                        IsPrimary = false
                        IsEnabled = true
                    }
                    {
                        Id = "DISPLAY3"
                        Name = "Vertical Monitor"
                        Resolution = { Width = 1080; Height = 1920; RefreshRate = 60 }
                        Position = { X = 3840; Y = -420 }
                        IsPrimary = false
                        IsEnabled = false
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