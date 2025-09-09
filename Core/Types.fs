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

/// Individual display mode (resolution + refresh rate combination)
type DisplayMode = {
    /// Width in pixels
    Width: int
    /// Height in pixels
    Height: int
    /// Refresh rate in Hz
    RefreshRate: int
    /// Color depth in bits per pixel (typically 32)
    BitsPerPixel: int
}

/// Complete capabilities and available modes for a display
type DisplayCapabilities = {
    /// Display identifier
    DisplayId: DisplayId
    /// Currently active display mode
    CurrentMode: DisplayMode
    /// All supported display modes
    AvailableModes: DisplayMode list
    /// Grouped resolutions map: (width,height) -> list of refresh rates
    GroupedResolutions: Map<(int * int), int list>
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
    /// Display capabilities including all available modes (optional for backward compatibility)
    Capabilities: DisplayCapabilities option
}

/// Named collection of display configurations for preset management
type DisplayConfiguration = {
    /// List of displays with their configurations
    Displays: DisplayInfo list
    /// User-assigned name for this configuration
    Name: string
    /// When this configuration was created
    CreatedAt: DateTime
    /// Total desktop bounds for validation
    TotalDesktopBounds: (int * int * int * int) option
    /// Hash of the configuration for change detection
    ConfigurationHash: string option
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
    
    /// Generate hash for a display configuration to detect changes
    let generateConfigurationHash (config: DisplayConfiguration) =
        let displayHashes = 
            config.Displays 
            |> List.sortBy (fun d -> d.Id)  // Sort for consistent hashing
            |> List.map (fun d -> 
                sprintf "%s:%d:%d:%d:%d:%b:%b:%A" 
                    d.Id d.Position.X d.Position.Y 
                    d.Resolution.Width d.Resolution.Height 
                    d.IsPrimary d.IsEnabled d.Orientation)
            |> String.concat "|"
        
        use sha256 = System.Security.Cryptography.SHA256.Create()
        let hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(displayHashes))
        Convert.ToBase64String(hash).[0..15]  // Take first 16 chars for readability
    
    /// Create a complete display configuration with metadata
    let createDisplayConfiguration (name: string) (displays: DisplayInfo list) =
        let bounds = getTotalDesktopBounds displays
        let config = {
            Displays = displays
            Name = name
            CreatedAt = DateTime.Now
            TotalDesktopBounds = bounds
            ConfigurationHash = None  // Will be set after creation
        }
        let hash = generateConfigurationHash config
        { config with ConfigurationHash = Some hash }
    
    /// Check if two configurations are equivalent
    let areConfigurationsEquivalent (config1: DisplayConfiguration) (config2: DisplayConfiguration) =
        match config1.ConfigurationHash, config2.ConfigurationHash with
        | Some hash1, Some hash2 -> hash1 = hash2
        | _ -> 
            // Fallback to manual comparison if hashes aren't available
            config1.Displays |> List.sortBy (fun d -> d.Id) = (config2.Displays |> List.sortBy (fun d -> d.Id))