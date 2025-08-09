namespace DisplaySwitchPro

open System

// Windows Display Detection System following ECS architecture
module WindowsDisplaySystem =
    
    // Initialize the display system
    let initialize() =
        printfn "[DEBUG] Initializing WindowsDisplaySystem - loading saved display states..."
        DisplayStateCache.initialize()

    // Main function to get all connected displays (following ECS pattern)
    let getConnectedDisplays() : DisplayInfo list =
        DisplayDetection.getConnectedDisplays()

    // Apply display mode changes (resolution, refresh rate, orientation)
    let applyDisplayMode (displayId: DisplayId) (mode: DisplayMode) (orientation: DisplayOrientation) =
        DisplayControl.applyDisplayMode displayId mode orientation

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
    
    // Initialize the module on load
    do initialize()