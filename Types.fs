namespace DisplaySwitchPro

// Core domain types for display management
type DisplayId = string

type Resolution = {
    Width: int
    Height: int
    RefreshRate: int
}

type Position = {
    X: int
    Y: int
}

type DisplayInfo = {
    Id: DisplayId
    Name: string
    Resolution: Resolution
    Position: Position
    IsPrimary: bool
    IsEnabled: bool
}

type DisplayConfiguration = {
    Displays: DisplayInfo list
    Name: string
    CreatedAt: System.DateTime
}

// Events for our system
type DisplayEvent =
    | DisplayDetected of DisplayInfo
    | DisplayConfigurationChanged of DisplayConfiguration
    | PresetSaved of name: string * DisplayConfiguration
    | PresetLoaded of name: string * DisplayConfiguration