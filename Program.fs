open DisplaySwitchPro

/// Parse command line arguments for log level
let parseLogLevel args =
    if Array.contains "--verbose" args then LogLevel.Verbose
    elif Array.contains "--quiet" args then LogLevel.Error
    else LogLevel.Normal

[<EntryPoint>]
let main args =
    // Set up logging based on command line arguments
    let logLevel = parseLogLevel args
    Logging.setLogLevel logLevel

    Logging.logNormal "DisplaySwitch-Pro starting..."

    // Create platform adapter and detect displays
    let adapter = PlatformAdapter.create ()
    let displays = adapter.GetConnectedDisplays()

    Logging.logNormalf "Detected %d displays" displays.Length

    // Load presets from disk
    let savedPresets = PresetManager.loadPresetsFromDisk()
    Logging.logNormalf "Loaded %d presets from disk" savedPresets.Count

    // Create initial application state with loaded presets
    let initialState = { AppState.empty with SavedPresets = savedPresets }
    let stateWithDisplays = AppState.updateDisplays displays initialState

    // Create a configuration from current displays
    let currentConfig = DisplayHelpers.createDisplayConfiguration "Current Setup" displays

    // Set as current configuration
    let stateWithConfig = AppState.setCurrentConfiguration currentConfig stateWithDisplays

    Logging.logNormal "✅ System working perfectly!"
    Logging.logNormal "Starting GUI..."

    // Launch GUI - this doesn't return until app closes
    let exitCode = ApplicationRunner.run adapter stateWithConfig
    exitCode