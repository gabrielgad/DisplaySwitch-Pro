namespace DisplaySwitchPro

open System

/// Core domain types for display management system
/// This module defines the fundamental types used throughout the ECS architecture

/// Unique identifier for a display device
/// Typically corresponds to the system's internal display ID
type DisplayId = string

/// Display resolution and refresh rate configuration
type Resolution = {
    /// Width in pixels (must be positive)
    Width: int
    /// Height in pixels (must be positive)  
    Height: int
    /// Refresh rate in Hz (typically 60, 75, 120, 144, etc.)
    RefreshRate: int
}

/// Screen position in virtual desktop coordinate space
type Position = {
    /// X coordinate (can be negative for displays left of primary)
    X: int
    /// Y coordinate (can be negative for displays above primary)
    Y: int
}

/// Display orientation for rotation support
type DisplayOrientation =
    | Landscape
    | Portrait
    | LandscapeFlipped
    | PortraitFlipped

/// Complete display information including hardware and configuration details
type DisplayInfo = {
    /// Unique system identifier for the display
    Id: DisplayId
    /// Human-readable display name (e.g., "Dell U2720Q", "Built-in Retina Display")
    Name: string
    /// Current resolution and refresh rate
    Resolution: Resolution
    /// Position in virtual desktop coordinate space
    Position: Position
    /// Display orientation (rotation)
    Orientation: DisplayOrientation
    /// Whether this display is the primary/main display
    IsPrimary: bool
    /// Whether this display is currently enabled/active
    IsEnabled: bool
}

/// Named collection of display configurations for preset management
type DisplayConfiguration = {
    /// List of displays with their configurations
    Displays: DisplayInfo list
    /// User-assigned name for this configuration
    Name: string
    /// When this configuration was created
    CreatedAt: DateTime
}

/// Comprehensive event model for the display management system
type DisplayEvent =
    | DisplayDetected of DisplayInfo
    | DisplayDisconnected of DisplayId  
    | DisplayConfigurationChanged of DisplayConfiguration
    | DisplayConfigurationFailed of error: string * attempted: DisplayConfiguration
    | PresetSaved of name: string * DisplayConfiguration
    | PresetLoaded of name: string * DisplayConfiguration
    | PresetLoadFailed of name: string * error: string

/// Validation functions for display domain types
module DisplayValidation =
    
    /// Validates that resolution values are reasonable
    let validateResolution (res: Resolution) : Result<Resolution, string> =
        if res.Width <= 0 || res.Height <= 0 then
            Error "Resolution dimensions must be positive"
        elif res.RefreshRate <= 0 then
            Error "Refresh rate must be positive"
        elif res.Width > 16384 || res.Height > 16384 then
            Error "Resolution dimensions exceed maximum supported values"
        else
            Ok res
    
    /// Validates complete display information
    let validateDisplayInfo (display: DisplayInfo) : Result<DisplayInfo, string> =
        if String.IsNullOrWhiteSpace(display.Name) then
            Error "Display name cannot be empty"
        else
            validateResolution display.Resolution
            |> Result.map (fun _ -> display)
    
    /// Validates display configuration
    let validateConfiguration (config: DisplayConfiguration) : Result<DisplayConfiguration, string> =
        if String.IsNullOrWhiteSpace(config.Name) then
            Error "Configuration name cannot be empty"
        elif List.isEmpty config.Displays then
            Error "Configuration must contain at least one display"
        else
            // Validate all displays in the configuration
            config.Displays
            |> List.map validateDisplayInfo
            |> List.fold (fun acc result ->
                match acc, result with
                | Ok displays, Ok display -> Ok (display :: displays)
                | Error e, _ -> Error e
                | _, Error e -> Error e
            ) (Ok [])
            |> Result.map (fun displays -> { config with Displays = List.rev displays })

/// Helper functions for working with display types
module DisplayHelpers =
    
    /// Calculate DPI for a display (requires physical dimensions)
    let calculateDPI (resolution: Resolution) (physicalWidthMM: float) (physicalHeightMM: float) =
        let widthInches = physicalWidthMM / 25.4
        let heightInches = physicalHeightMM / 25.4
        let dpiX = float resolution.Width / widthInches
        let dpiY = float resolution.Height / heightInches
        (dpiX, dpiY)
    
    /// Calculate aspect ratio
    let getAspectRatio (resolution: Resolution) =
        float resolution.Width / float resolution.Height
    
    /// Get total desktop area from all enabled displays
    let getTotalDesktopBounds (displays: DisplayInfo list) =
        let enabledDisplays = displays |> List.filter (fun d -> d.IsEnabled)
        if List.isEmpty enabledDisplays then
            None
        else
            let minX = enabledDisplays |> List.map (fun d -> d.Position.X) |> List.min
            let minY = enabledDisplays |> List.map (fun d -> d.Position.Y) |> List.min
            let maxX = enabledDisplays |> List.map (fun d -> d.Position.X + d.Resolution.Width) |> List.max
            let maxY = enabledDisplays |> List.map (fun d -> d.Position.Y + d.Resolution.Height) |> List.max
            Some (minX, minY, maxX - minX, maxY - minY)