namespace DisplaySwitchPro

open System

// Windows Display Detection System following ECS architecture
module WindowsDisplaySystem =
    
    // Initialize the display system
    let initialize() =
        Logging.logVerbosef " Initializing WindowsDisplaySystem - loading saved display states..."
        DisplayStateCache.initialize()

    // Main function to get all connected displays (following ECS pattern)
    let getConnectedDisplays() : DisplayInfo list =
        DisplayDetection.getConnectedDisplays()

    // Apply display mode changes (resolution, refresh rate, orientation)
    let applyDisplayMode (displayId: DisplayId) (mode: DisplayMode) (orientation: DisplayOrientation) =
        DisplayControl.applyDisplayMode displayId mode orientation

    // Set display orientation only (preserving current resolution and refresh rate)
    let setDisplayOrientation (displayId: DisplayId) (orientation: DisplayOrientation) =
        DisplayControl.setDisplayOrientation displayId orientation

    // Set display as primary
    let setPrimaryDisplay (displayId: DisplayId) =
        DisplayControl.setPrimaryDisplay displayId

    // Enable or disable a display using the proper modern Windows API approach
    let setDisplayEnabled (displayId: DisplayId) (enabled: bool) =
        DisplayControl.setDisplayEnabled displayId enabled

    // Test display mode temporarily for 15 seconds then revert
    let testDisplayMode (displayId: DisplayId) (mode: DisplayMode) (orientation: DisplayOrientation) onComplete =
        DisplayControl.testDisplayMode displayId mode orientation onComplete
    
    // Get all supported display modes for a display device  
    let getAllDisplayModes (deviceName: string) =
        DisplayDetection.getAllDisplayModes deviceName
    
    // Set display position - applies canvas drag changes to Windows using CCD API
    let setDisplayPosition (displayId: DisplayId) (newPosition: Position) =
        DisplayControl.setDisplayPosition displayId newPosition

    // Apply multiple display positions atomically using CCD API with compacting (legacy version)
    let applyMultipleDisplayPositions (displayPositions: (DisplayId * Position) list) =
        DisplayControl.applyMultipleDisplayPositions displayPositions

    // Apply multiple display positions atomically using CCD API with compacting (with provided display info)
    let applyMultipleDisplayPositionsWithInfo (displayPositionsWithInfo: (DisplayId * Position * DisplayInfo) list) =
        DisplayControl.applyMultipleDisplayPositionsWithInfo displayPositionsWithInfo

    // Apply enable/disable to multiple displays with best-effort approach
    let applyMultipleDisplayEnabled (displayOperations: (DisplayId * bool) list) =
        DisplayControl.applyMultipleDisplayEnabled displayOperations

    // Apply display modes to multiple displays with best-effort approach
    let applyMultipleDisplayModes (displayModeOperations: (DisplayId * DisplayMode * DisplayOrientation) list) =
        DisplayControl.applyMultipleDisplayModes displayModeOperations

    // Apply orientation changes to multiple displays with best-effort approach
    let applyMultipleDisplayOrientations (displayOrientationOperations: (DisplayId * DisplayOrientation) list) =
        DisplayControl.applyMultipleDisplayOrientations displayOrientationOperations
    
    // Initialize the module on load
    do initialize()