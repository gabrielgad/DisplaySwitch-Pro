namespace DisplaySwitchPro

open System
open System.Runtime.InteropServices
open System.Management
open System.Text

// Windows API structures for display detection
module WindowsAPI =
    
    [<StructLayout(LayoutKind.Sequential)>]
    type RECT = 
        struct
            val mutable left: int
            val mutable top: int
            val mutable right: int
            val mutable bottom: int
        end

    [<StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)>]
    type MONITORINFOEX =
        struct
            val mutable cbSize: int
            val mutable rcMonitor: RECT
            val mutable rcWork: RECT
            val mutable dwFlags: uint32
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)>]
            val mutable szDevice: string
        end

    [<StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)>]
    type DISPLAY_DEVICE =
        struct
            val mutable cb: int
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)>]
            val mutable DeviceName: string
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)>]
            val mutable DeviceString: string
            val mutable StateFlags: uint32
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)>]
            val mutable DeviceID: string
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)>]
            val mutable DeviceKey: string
        end

    [<StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)>]
    type DEVMODE =
        struct
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)>]
            val mutable dmDeviceName: string
            val mutable dmSpecVersion: uint16
            val mutable dmDriverVersion: uint16
            val mutable dmSize: uint16
            val mutable dmDriverExtra: uint16
            val mutable dmFields: uint32
            val mutable dmPositionX: int
            val mutable dmPositionY: int
            val mutable dmDisplayOrientation: uint32
            val mutable dmDisplayFixedOutput: uint32
            val mutable dmColor: int16
            val mutable dmDuplex: int16
            val mutable dmYResolution: int16
            val mutable dmTTOption: int16
            val mutable dmCollate: int16
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)>]
            val mutable dmFormName: string
            val mutable dmLogPixels: uint16
            val mutable dmBitsPerPel: uint32
            val mutable dmPelsWidth: uint32
            val mutable dmPelsHeight: uint32
            val mutable dmDisplayFlags: uint32
            val mutable dmDisplayFrequency: uint32
            val mutable dmICMMethod: uint32
            val mutable dmICMIntent: uint32
            val mutable dmMediaType: uint32
            val mutable dmDitherType: uint32
            val mutable dmReserved1: uint32
            val mutable dmReserved2: uint32
            val mutable dmPanningWidth: uint32
            val mutable dmPanningHeight: uint32
        end

    type MonitorEnumDelegate = delegate of IntPtr * IntPtr * byref<RECT> * IntPtr -> bool

    // Display device state flags
    module Flags =
        let DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001u
        let DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004u
        let DISPLAY_DEVICE_MIRRORING_DRIVER = 0x00000008u

    // P/Invoke declarations
    [<DllImport("user32.dll")>]
    extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, 
                                   MonitorEnumDelegate lpfnEnum, IntPtr dwData)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFOEX& lpmi)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern bool EnumDisplayDevices(string lpDevice, uint32 iDevNum, 
                                  DISPLAY_DEVICE& lpDisplayDevice, uint32 dwFlags)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, DEVMODE& lpDevMode)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern int ChangeDisplaySettings(DEVMODE& lpDevMode, uint32 dwFlags)

    [<DllImport("user32.dll", CharSet = CharSet.Auto)>]
    extern int ChangeDisplaySettingsEx(string lpszDeviceName, DEVMODE& lpDevMode, IntPtr hwnd, uint32 dwflags, IntPtr lParam)

    [<DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "ChangeDisplaySettingsExW")>]
    extern int ChangeDisplaySettingsExNull(string lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint32 dwflags, IntPtr lParam)

    // CCD (Connecting and Configuring Displays) API structures
    [<StructLayout(LayoutKind.Sequential)>]
    type LUID =
        struct
            val mutable LowPart: uint32
            val mutable HighPart: int32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_RATIONAL =
        struct
            val mutable Numerator: uint32
            val mutable Denominator: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_2DREGION =
        struct
            val mutable cx: uint32
            val mutable cy: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type POINTL =
        struct
            val mutable x: int32
            val mutable y: int32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_PATH_SOURCE_INFO =
        struct
            val mutable adapterId: LUID
            val mutable id: uint32
            val mutable modeInfoIdx: uint32
            val mutable statusFlags: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_PATH_TARGET_INFO =
        struct
            val mutable adapterId: LUID
            val mutable id: uint32
            val mutable modeInfoIdx: uint32
            val mutable outputTechnology: uint32
            val mutable rotation: uint32
            val mutable scaling: uint32
            val mutable refreshRate: DISPLAYCONFIG_RATIONAL
            val mutable scanLineOrdering: uint32
            val mutable targetAvailable: int32
            val mutable statusFlags: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_PATH_INFO =
        struct
            val mutable sourceInfo: DISPLAYCONFIG_PATH_SOURCE_INFO
            val mutable targetInfo: DISPLAYCONFIG_PATH_TARGET_INFO
            val mutable flags: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_SOURCE_MODE =
        struct
            val mutable width: uint32
            val mutable height: uint32
            val mutable pixelFormat: uint32
            val mutable position: POINTL
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_VIDEO_SIGNAL_INFO =
        struct
            val mutable pixelRate: uint64
            val mutable hSyncFreq: DISPLAYCONFIG_RATIONAL
            val mutable vSyncFreq: DISPLAYCONFIG_RATIONAL
            val mutable activeSize: DISPLAYCONFIG_2DREGION
            val mutable totalSize: DISPLAYCONFIG_2DREGION
            val mutable videoStandard: uint32
            val mutable scanLineOrdering: uint32
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_TARGET_MODE =
        struct
            val mutable targetVideoSignalInfo: DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        end

    [<StructLayout(LayoutKind.Explicit)>]
    type DISPLAYCONFIG_MODE_INFO_UNION =
        struct
            [<FieldOffset(0)>]
            val mutable targetMode: DISPLAYCONFIG_TARGET_MODE
            [<FieldOffset(0)>]
            val mutable sourceMode: DISPLAYCONFIG_SOURCE_MODE
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type DISPLAYCONFIG_MODE_INFO =
        struct
            val mutable infoType: uint32
            val mutable id: uint32
            val mutable adapterId: LUID
            val mutable modeInfo: DISPLAYCONFIG_MODE_INFO_UNION
        end




    // ChangeDisplaySettings flags
    module CDS =
        let CDS_UPDATEREGISTRY = 0x00000001u
        let CDS_TEST = 0x00000002u
        let CDS_FULLSCREEN = 0x00000004u
        let CDS_GLOBAL = 0x00000008u
        let CDS_SET_PRIMARY = 0x00000010u
        let CDS_VIDEOPARAMETERS = 0x00000020u
        let CDS_ENABLE_UNSAFE_MODES = 0x00000100u
        let CDS_DISABLE_UNSAFE_MODES = 0x00000200u
        let CDS_RESET = 0x40000000u
        let CDS_NORESET = 0x10000000u

    // ChangeDisplaySettings return values
    module DISP =
        let DISP_CHANGE_SUCCESSFUL = 0
        let DISP_CHANGE_RESTART = 1
        let DISP_CHANGE_FAILED = -1
        let DISP_CHANGE_BADMODE = -2
        let DISP_CHANGE_NOTUPDATED = -3
        let DISP_CHANGE_BADFLAGS = -4
        let DISP_CHANGE_BADPARAM = -5
        let DISP_CHANGE_BADDUALVIEW = -6

    // Display orientation values for DEVMODE.dmDisplayOrientation
    module DMDO =
        let DMDO_DEFAULT = 0u
        let DMDO_90 = 1u
        let DMDO_180 = 2u
        let DMDO_270 = 3u

    // CCD API constants
    module QDC =
        let QDC_ALL_PATHS = 0x00000001u
        let QDC_ONLY_ACTIVE_PATHS = 0x00000002u
        let QDC_DATABASE_CURRENT = 0x00000004u

    module SDC =
        let SDC_TOPOLOGY_INTERNAL = 0x00000001u
        let SDC_TOPOLOGY_CLONE = 0x00000002u
        let SDC_TOPOLOGY_EXTEND = 0x00000004u
        let SDC_TOPOLOGY_EXTERNAL = 0x00000008u
        let SDC_APPLY = 0x00000080u
        let SDC_NO_OPTIMIZATION = 0x00000100u
        let SDC_VALIDATE = 0x00000200u
        let SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020u
        let SDC_ALLOW_CHANGES = 0x00000400u
        let SDC_PATH_PERSIST_IF_REQUIRED = 0x00000800u
        let SDC_FORCE_MODE_ENUMERATION = 0x00001000u
        let SDC_ALLOW_PATH_ORDER_CHANGES = 0x00002000u

    module DISPLAYCONFIG_PATH =
        let DISPLAYCONFIG_PATH_ACTIVE = 0x00000001u

    module ERROR =
        let ERROR_SUCCESS = 0
        let ERROR_INVALID_PARAMETER = 87
        let ERROR_NOT_SUPPORTED = 50
        let ERROR_ACCESS_DENIED = 5
        let ERROR_INSUFFICIENT_BUFFER = 122
        let ERROR_GEN_FAILURE = 31

    // CCD (Connecting and Configuring Displays) API functions
    [<DllImport("user32.dll")>]
    extern int GetDisplayConfigBufferSizes(uint32 flags, uint32& numPathArrayElements, uint32& numModeInfoArrayElements)

    [<DllImport("user32.dll")>]
    extern int QueryDisplayConfig(uint32 flags, uint32& numPathArrayElements, 
                                  [<In; Out>] DISPLAYCONFIG_PATH_INFO[] pathArray,
                                  uint32& numModeInfoArrayElements,
                                  [<In; Out>] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
                                  IntPtr currentTopologyId)

    [<DllImport("user32.dll")>]
    extern int SetDisplayConfig(uint32 numPathArrayElements,
                                DISPLAYCONFIG_PATH_INFO[] pathArray,
                                uint32 numModeInfoArrayElements,
                                DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
                                uint32 flags)


// Windows Display Detection System following ECS architecture
module WindowsDisplaySystem =
    
    open WindowsAPI
    open System.Runtime.InteropServices
    open System.Collections.Generic
    open System.IO
    open System.Text.Json
    
    // Display state cache for remembering display configurations
    type DisplayStateCache = {
        DisplayId: string
        Position: Position
        Resolution: Resolution
        OrientationValue: int // Store as int instead of discriminated union
        IsPrimary: bool
        SavedAt: System.DateTime
    }
    
    // In-memory cache of display states
    let private displayStateCache = Dictionary<string, DisplayStateCache>()
    
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
                displayStateCache.Clear()
                for state in states do
                    displayStateCache.[state.DisplayId] <- state
                printfn "[DEBUG] Loaded %d display states from cache" states.Length
        with
        | ex -> 
            printfn "[DEBUG] Failed to load display states: %s" ex.Message
    
    // Save display states to file
    let private saveDisplayStates() =
        try
            let filePath = getStateCacheFilePath()
            let states = displayStateCache.Values |> Seq.toArray
            let options = JsonSerializerOptions(WriteIndented = true)
            let json = JsonSerializer.Serialize(states, options)
            File.WriteAllText(filePath, json)
            printfn "[DEBUG] Saved %d display states to cache" states.Length
        with
        | ex -> 
            printfn "[DEBUG] Failed to save display states: %s" ex.Message
    
    // Convert DisplayOrientation to Windows API orientation value
    let private orientationToWindows (orientation: DisplayOrientation) =
        match orientation with
        | Landscape -> DMDO.DMDO_DEFAULT
        | Portrait -> DMDO.DMDO_90
        | LandscapeFlipped -> DMDO.DMDO_180
        | PortraitFlipped -> DMDO.DMDO_270

    // Convert Windows API orientation value to DisplayOrientation
    let private windowsToOrientation (windowsOrientation: uint32) =
        match windowsOrientation with
        | x when x = DMDO.DMDO_DEFAULT -> Landscape
        | x when x = DMDO.DMDO_90 -> Portrait
        | x when x = DMDO.DMDO_180 -> LandscapeFlipped
        | x when x = DMDO.DMDO_270 -> PortraitFlipped
        | _ -> Landscape // Default fallback
    
    // Convert DisplayOrientation to int for JSON serialization
    let private orientationToInt (orientation: DisplayOrientation) =
        match orientation with
        | Landscape -> 0
        | Portrait -> 1
        | LandscapeFlipped -> 2
        | PortraitFlipped -> 3
    
    // Convert int to DisplayOrientation from JSON deserialization
    let private intToOrientation (value: int) =
        match value with
        | 0 -> Landscape
        | 1 -> Portrait
        | 2 -> LandscapeFlipped
        | 3 -> PortraitFlipped
        | _ -> Landscape // Default fallback
    
    // Save current display state to cache
    let private saveDisplayState (displayId: string) =
        try
            // Get current display information
            let mutable devMode = DEVMODE()
            devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
            
            let result = EnumDisplaySettings(displayId, -1, &devMode)
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
                
                displayStateCache.[displayId] <- state
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
    let private getSavedDisplayState (displayId: string) =
        match displayStateCache.TryGetValue(displayId) with
        | true, state -> Some state
        | false, _ -> None
    
    // Initialize - load saved states on module init
    let private initializeStateCache() =
        loadDisplayStates()

    // CCD API helper functions
    let private getDisplayPaths includeInactive =
        try
            let mutable pathCount = 0u
            let mutable modeCount = 0u
            
            // Get the required buffer sizes
            let flags = if includeInactive then QDC.QDC_ALL_PATHS else QDC.QDC_ONLY_ACTIVE_PATHS
            let sizeResult = GetDisplayConfigBufferSizes(flags, &pathCount, &modeCount)
            
            if sizeResult <> ERROR.ERROR_SUCCESS then
                printfn "[DEBUG] GetDisplayConfigBufferSizes failed with error: %d" sizeResult
                Error (sprintf "Failed to get display config buffer sizes: %d" sizeResult)
            else
                printfn "[DEBUG] Buffer sizes - Paths: %d, Modes: %d" pathCount modeCount
                
                // Allocate arrays
                let pathArray = Array.zeroCreate<DISPLAYCONFIG_PATH_INFO> (int pathCount)
                let modeArray = Array.zeroCreate<DISPLAYCONFIG_MODE_INFO> (int modeCount)
                
                // Query the display configuration
                let queryResult = QueryDisplayConfig(flags, &pathCount, pathArray, &modeCount, modeArray, IntPtr.Zero)
                
                if queryResult <> ERROR.ERROR_SUCCESS then
                    printfn "[DEBUG] QueryDisplayConfig failed with error: %d" queryResult
                    Error (sprintf "Failed to query display config: %d" queryResult)
                else
                    printfn "[DEBUG] Successfully queried %d paths and %d modes" pathCount modeCount
                    Ok (pathArray, modeArray, pathCount, modeCount)
        with
        | ex ->
            printfn "[DEBUG] Exception in getDisplayPaths: %s" ex.Message
            Error (sprintf "Exception getting display paths: %s" ex.Message)

    // Simplified but robust mapping that works for both enabling and disabling displays
    let private findDisplayPathByDevice displayId (paths: DISPLAYCONFIG_PATH_INFO[]) pathCount =
        try
            // Extract display number from "\\.\DISPLAY3" -> 3
            let displayNumber = 
                if (displayId: string).StartsWith(@"\\.\DISPLAY") then
                    let mutable result = 0
                    match System.Int32.TryParse((displayId: string).Substring(11), &result) with
                    | true -> Some result // Keep 1-based for easier debugging
                    | false -> None
                else None
            
            match displayNumber with
            | Some displayNum ->
                printfn "[DEBUG] Looking for display %d in %d paths" displayNum pathCount
                
                // Strategy 1: Look for paths by source ID (most reliable)
                let mutable foundPath = None
                let mutable foundIndex = -1
                
                // Try to find path where source ID matches display number - 1 (0-based)
                for i in 0 .. int pathCount - 1 do
                    let path = paths.[i]
                    if int path.sourceInfo.id = (displayNum - 1) then
                        printfn "[DEBUG] Found path with source ID %d matching display %d" path.sourceInfo.id displayNum
                        foundPath <- Some path
                        foundIndex <- i
                
                match foundPath with
                | Some path -> 
                    printfn "[DEBUG] Mapped display %s to path index %d (source ID match)" displayId foundIndex
                    Ok (path, foundIndex)
                | None ->
                    // Strategy 2: Use display index directly as fallback
                    let pathIndex = displayNum - 1 // Convert to 0-based
                    if pathIndex >= 0 && pathIndex < int pathCount then
                        let path = paths.[pathIndex]
                        printfn "[DEBUG] Mapped display %s to path index %d (direct index)" displayId pathIndex
                        Ok (path, pathIndex)
                    else
                        // Strategy 3: Search through all paths for any that could match
                        printfn "[DEBUG] Direct index %d out of range, searching all paths..." pathIndex
                        if int pathCount > 0 then
                            // Just take the path at index (displayNum - 1) mod pathCount to avoid out of bounds
                            let wrappedIndex = (displayNum - 1) % int pathCount
                            let path = paths.[wrappedIndex]
                            printfn "[DEBUG] Using wrapped index %d for display %s" wrappedIndex displayId
                            Ok (path, wrappedIndex)
                        else
                            Error (sprintf "No paths available for display %s" displayId)
            | None ->
                printfn "[DEBUG] Could not parse display number from %s" displayId
                Error (sprintf "Could not parse display number from %s" displayId)
        with
        | ex ->
            printfn "[DEBUG] Exception mapping display path: %s" ex.Message
            Error (sprintf "Exception mapping display path: %s" ex.Message)

    // Find the display path for a specific display ID (using simplified approach)
    let private findDisplayPath displayId (paths: DISPLAYCONFIG_PATH_INFO[]) pathCount =
        findDisplayPathByDevice displayId paths pathCount
    
    // Get monitor friendly names using WMI
    let private getMonitorFriendlyNames() =
        try
            let monitors = System.Collections.Generic.List<string>()
            
            // Query WmiMonitorID for friendly names
            use searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorID")
            use collection = searcher.Get()
            
            for obj in collection do
                use managementObj = obj :?> ManagementObject
                try
                    let instanceName = managementObj.["InstanceName"] :?> string
                    let userFriendlyNameObj = managementObj.["UserFriendlyName"]
                    let manufacturerNameObj = managementObj.["ManufacturerName"]
                    
                    let parseWmiString (obj: obj) =
                        if obj <> null then
                            match obj with
                            | :? (uint16[]) as uint16Array ->
                                uint16Array 
                                |> Array.filter (fun u -> u <> 0us)
                                |> Array.map char
                                |> String
                            | :? (byte[]) as byteArray ->
                                byteArray 
                                |> Array.filter (fun b -> b <> 0uy)
                                |> Array.map char
                                |> String
                            | _ -> ""
                        else ""
                    
                    let friendlyName = parseWmiString userFriendlyNameObj
                    let manufacturer = parseWmiString manufacturerNameObj
                    
                    if not (String.IsNullOrEmpty(friendlyName)) then
                        
                        let fullName = 
                            if not (String.IsNullOrEmpty(manufacturer)) then
                                sprintf "%s %s" manufacturer friendlyName
                            else friendlyName
                        
                        // Add to list - WMI monitors should appear in same order as displays
                        monitors.Add(fullName)
                        printfn "WMI Monitor: %s -> %s" instanceName fullName
                
                with ex -> 
                    printfn "Error processing WMI monitor: %s" ex.Message
            
            monitors
        with
        | ex -> 
            printfn "WMI monitor detection failed: %s" ex.Message
            System.Collections.Generic.List<string>()
    
    // Get all display devices (including inactive ones)
    let private getAllDisplayDevices() =
        let mutable devices = []
        let mutable deviceIndex = 0u
        let mutable continueEnum = true
        
        while continueEnum do
            let mutable displayDevice = DISPLAY_DEVICE()
            displayDevice.cb <- Marshal.SizeOf(typeof<DISPLAY_DEVICE>)
            
            let result = EnumDisplayDevices(null, deviceIndex, &displayDevice, 0u)
            if result then
                // Skip mirroring drivers
                let isMirror = (displayDevice.StateFlags &&& Flags.DISPLAY_DEVICE_MIRRORING_DRIVER) <> 0u
                if not isMirror then
                    devices <- displayDevice :: devices
                deviceIndex <- deviceIndex + 1u
            else
                continueEnum <- false
        
        List.rev devices
    
    // Get current display settings including refresh rate
    let private getCurrentDisplaySettings (deviceName: string) =
        try
            let mutable devMode = DEVMODE()
            devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
            
            // ENUM_CURRENT_SETTINGS = -1 to get current display mode
            let result = EnumDisplaySettings(deviceName, -1, &devMode)
            if result then
                Some {
                    Width = int devMode.dmPelsWidth
                    Height = int devMode.dmPelsHeight
                    RefreshRate = int devMode.dmDisplayFrequency
                    BitsPerPixel = int devMode.dmBitsPerPel
                }
            else
                printfn "Failed to get display settings for %s" deviceName
                None
        with
        | ex ->
            printfn "Error getting display settings for %s: %s" deviceName ex.Message
            None

    // Helper to create grouped resolutions from mode list
    let private createGroupedResolutions (modes: DisplayMode list) =
        modes
        |> List.groupBy (fun m -> (m.Width, m.Height))
        |> List.map (fun ((w, h), modelist) -> 
            ((w, h), modelist |> List.map (fun m -> m.RefreshRate) |> List.distinct |> List.sort))
        |> Map.ofList

    // Get exact DEVMODE structure for a specific mode
    let private getExactDevModeForMode (deviceName: string) (targetMode: DisplayMode) =
        try
            let mutable modeIndex = 0
            let mutable continueEnum = true
            let mutable foundMode = None
            
            while continueEnum && foundMode.IsNone do
                let mutable devMode = DEVMODE()
                devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
                
                let result = EnumDisplaySettings(deviceName, modeIndex, &devMode)
                if result then
                    let mode = {
                        Width = int devMode.dmPelsWidth
                        Height = int devMode.dmPelsHeight
                        RefreshRate = int devMode.dmDisplayFrequency
                        BitsPerPixel = int devMode.dmBitsPerPel
                    }
                    
                    if mode.Width = targetMode.Width && 
                       mode.Height = targetMode.Height && 
                       mode.RefreshRate = targetMode.RefreshRate then
                        foundMode <- Some devMode
                    
                    modeIndex <- modeIndex + 1
                else
                    continueEnum <- false
            
            foundMode
        with
        | ex ->
            printfn "[DEBUG] Error finding exact DEVMODE: %s" ex.Message
            None

    // Get all supported display modes for a display device
    let internal getAllDisplayModes (deviceName: string) =
        try
            let mutable modes = []
            let mutable modeIndex = 0
            let mutable continueEnum = true
            
            printfn "Enumerating all display modes for %s..." deviceName
            
            while continueEnum do
                let mutable devMode = DEVMODE()
                devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
                
                let result = EnumDisplaySettings(deviceName, modeIndex, &devMode)
                if result then
                    let mode = {
                        Width = int devMode.dmPelsWidth
                        Height = int devMode.dmPelsHeight
                        RefreshRate = int devMode.dmDisplayFrequency
                        BitsPerPixel = int devMode.dmBitsPerPel
                    }
                    
                    if modeIndex < 10 then // Log first 10 modes for debugging
                        printfn "[DEBUG] Mode %d: %dx%d @ %dHz, %d bpp" 
                                modeIndex mode.Width mode.Height mode.RefreshRate mode.BitsPerPixel
                    
                    // Only add valid modes (basic validation)
                    if mode.Width > 0 && mode.Height > 0 && mode.RefreshRate > 0 then
                        modes <- mode :: modes
                    else
                        if modeIndex < 10 then
                            printfn "[DEBUG] Skipping invalid mode at index %d" modeIndex
                        
                    modeIndex <- modeIndex + 1
                else
                    continueEnum <- false
                    printfn "[DEBUG] EnumDisplaySettings stopped at index %d" modeIndex
            
            let uniqueModes = modes |> List.rev |> List.distinct
            printfn "Found %d unique display modes for %s" uniqueModes.Length deviceName
            
            // Log first few modes for verification
            uniqueModes 
            |> List.take (min 5 uniqueModes.Length)
            |> List.iter (fun mode -> 
                printfn "  Mode: %dx%d @ %dHz" mode.Width mode.Height mode.RefreshRate)
            
            uniqueModes
            
        with
        | ex ->
            printfn "Error enumerating display modes for %s: %s" deviceName ex.Message
            []

    // Get monitor info for active displays
    let private getActiveMonitorInfo() =
        let mutable monitors = Map.empty
        
        let monitorCallback = 
            MonitorEnumDelegate(fun hMonitor hdcMonitor lprcMonitor dwData ->
                let mutable monitorInfo = MONITORINFOEX()
                monitorInfo.cbSize <- Marshal.SizeOf(typeof<MONITORINFOEX>)
                
                let result = GetMonitorInfo(hMonitor, &monitorInfo)
                if result then
                    monitors <- Map.add monitorInfo.szDevice monitorInfo monitors
                
                true // Continue enumeration
            )
        
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, monitorCallback, IntPtr.Zero) |> ignore
        monitors
    
    // Convert Windows data to domain types
    let private convertToDisplayInfo (device: DISPLAY_DEVICE) (monitorInfo: MONITORINFOEX option) (wmiMonitors: System.Collections.Generic.List<string>) (deviceIndex: int) =
        let isAttached = (device.StateFlags &&& Flags.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) <> 0u
        let isPrimary = (device.StateFlags &&& Flags.DISPLAY_DEVICE_PRIMARY_DEVICE) <> 0u
        
        // Try to get friendly name from WMI by index
        let monitorName = 
            if deviceIndex < wmiMonitors.Count then
                wmiMonitors.[deviceIndex]
            else
                device.DeviceString
        
        // Create friendly display names
        let displayNumber = device.DeviceName.Replace(@"\.\DISPLAY", "")
        let friendlyId = if isPrimary then sprintf "Display %s (Primary)" displayNumber else sprintf "Display %s" displayNumber
        let fullName = sprintf "%s (%s)" friendlyId monitorName
        
        match monitorInfo with
        | Some monitor ->
            // Active display with position and resolution
            let width = monitor.rcMonitor.right - monitor.rcMonitor.left
            let height = monitor.rcMonitor.bottom - monitor.rcMonitor.top
            let orientation = if width > height then Landscape else Portrait
            
            // Get actual display settings including refresh rate
            let actualSettings = getCurrentDisplaySettings device.DeviceName
            let resolution = 
                match actualSettings with
                | Some settings -> 
                    printfn "Display %s: %dx%d @ %dHz" device.DeviceName settings.Width settings.Height settings.RefreshRate
                    { Width = settings.Width; Height = settings.Height; RefreshRate = settings.RefreshRate }
                | None -> 
                    printfn "Using fallback resolution for %s: %dx%d @ 60Hz" device.DeviceName width height
                    { Width = width; Height = height; RefreshRate = 60 }
            
            // Get all available display modes for active displays
            let availableModes = getAllDisplayModes device.DeviceName
            let capabilities = 
                if availableModes.Length > 0 then
                    let currentMode = 
                        match actualSettings with
                        | Some settings -> settings
                        | None -> { Width = width; Height = height; RefreshRate = 60; BitsPerPixel = 32 }
                    
                    Some {
                        DisplayId = device.DeviceName
                        CurrentMode = currentMode
                        AvailableModes = availableModes
                        GroupedResolutions = createGroupedResolutions availableModes
                    }
                else None
            
            {
                Id = device.DeviceName
                Name = fullName
                Resolution = resolution
                Position = { X = monitor.rcMonitor.left; Y = monitor.rcMonitor.top }
                Orientation = orientation
                IsPrimary = isPrimary
                IsEnabled = true // Active displays are enabled
                Capabilities = capabilities
            }
        | None ->
            // Inactive display - position away from active displays to avoid overlap
            let inactiveOffset = 3000 + (deviceIndex * 2000) // Offset inactive displays to prevent overlap
            {
                Id = device.DeviceName
                Name = sprintf "%s [Inactive]" fullName
                Resolution = { Width = 1920; Height = 1080; RefreshRate = 60 }
                Position = { X = inactiveOffset; Y = 0 } // Position away from active displays
                Orientation = Landscape
                IsPrimary = isPrimary
                IsEnabled = false // Inactive displays are disabled
                Capabilities = None // No capabilities for inactive displays (can be populated later if needed)
            }
    
    // Main function to get all connected displays (following ECS pattern)
    let getConnectedDisplays() : DisplayInfo list =
        try
            printfn "Windows display detection: Enumerating all display devices..."
            
            // Get monitor friendly names from WMI
            let wmiMonitors = getMonitorFriendlyNames()
            printfn "Found %d WMI monitor entries" wmiMonitors.Count
            
            // Get all display devices (active and inactive)
            let allDevices = getAllDisplayDevices()
            printfn "Found %d display devices" allDevices.Length
            
            // Get active monitor information
            let activeMonitors = getActiveMonitorInfo()
            printfn "Found %d active monitors" activeMonitors.Count
            
            // Convert to domain types
            allDevices
            |> List.mapi (fun index device ->
                let monitorInfo = Map.tryFind device.DeviceName activeMonitors
                let displayInfo = convertToDisplayInfo device monitorInfo wmiMonitors index
                printfn "  %s: %s" displayInfo.Id (if displayInfo.IsEnabled then "ENABLED" else "DISABLED")
                displayInfo
            )
            
        with
        | ex -> 
            printfn "Windows display detection failed: %s" ex.Message
            []

    // Apply display mode changes (resolution, refresh rate, orientation)
    let applyDisplayMode (displayId: DisplayId) (mode: DisplayMode) (orientation: DisplayOrientation) =
        try
            printfn "[DEBUG] ========== Starting applyDisplayMode =========="
            printfn "[DEBUG] Display ID: %s" displayId
            printfn "[DEBUG] Target Mode: %dx%d @ %dHz (BitsPerPixel: %d)" mode.Width mode.Height mode.RefreshRate mode.BitsPerPixel
            printfn "[DEBUG] Target Orientation: %A (Windows value: %u)" orientation (orientationToWindows orientation)

            // Get current display settings to use as base
            let mutable devMode = DEVMODE()
            devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
            printfn "[DEBUG] DEVMODE structure size: %d bytes" devMode.dmSize
            
            let getCurrentResult = EnumDisplaySettings(displayId, -1, &devMode)
            if not getCurrentResult then
                printfn "[DEBUG] ERROR: EnumDisplaySettings failed for %s" displayId
                Error (sprintf "Could not get current display settings for %s" displayId)
            else
                printfn "[DEBUG] Current display settings retrieved successfully:"
                printfn "[DEBUG]   Current Resolution: %ux%u @ %uHz" devMode.dmPelsWidth devMode.dmPelsHeight devMode.dmDisplayFrequency
                printfn "[DEBUG]   Current BitsPerPixel: %u" devMode.dmBitsPerPel
                printfn "[DEBUG]   Current Orientation: %u" devMode.dmDisplayOrientation
                printfn "[DEBUG]   Current Position: (%d, %d)" devMode.dmPositionX devMode.dmPositionY
                printfn "[DEBUG]   Current dmFields: 0x%08X" devMode.dmFields
                
                // Store original values for comparison
                let originalWidth = devMode.dmPelsWidth
                let originalHeight = devMode.dmPelsHeight
                let originalFreq = devMode.dmDisplayFrequency
                let originalOrientation = devMode.dmDisplayOrientation
                
                // Update the settings we want to change
                devMode.dmPelsWidth <- uint32 mode.Width
                devMode.dmPelsHeight <- uint32 mode.Height  
                devMode.dmDisplayFrequency <- uint32 mode.RefreshRate
                devMode.dmBitsPerPel <- uint32 mode.BitsPerPixel
                devMode.dmDisplayOrientation <- orientationToWindows orientation
                
                // Try two approaches based on whether we're changing resolution
                let isResolutionChange = originalWidth <> uint32 mode.Width || originalHeight <> uint32 mode.Height
                
                let dmFields = 
                    if isResolutionChange then
                        // For resolution changes, use only the essential fields
                        printfn "[DEBUG] Resolution change detected - using minimal fields"
                        0x00080000u ||| 0x00040000u ||| 0x00020000u ||| 0x00400000u
                        // DM_BITSPERPEL | DM_PELSHEIGHT | DM_PELSWIDTH | DM_DISPLAYFREQUENCY
                    else
                        // For refresh rate only changes, preserve original fields
                        printfn "[DEBUG] Refresh rate only change - preserving original fields"
                        devMode.dmFields ||| 0x00400000u ||| 0x00000080u
                        // Original fields | DM_DISPLAYFREQUENCY | DM_DISPLAYORIENTATION
                
                devMode.dmFields <- dmFields
                
                printfn "[DEBUG] Updated DEVMODE values:"
                printfn "[DEBUG]   New Resolution: %ux%u @ %uHz" devMode.dmPelsWidth devMode.dmPelsHeight devMode.dmDisplayFrequency
                printfn "[DEBUG]   New BitsPerPixel: %u" devMode.dmBitsPerPel
                printfn "[DEBUG]   New Orientation: %u" devMode.dmDisplayOrientation
                printfn "[DEBUG]   New dmFields: 0x%08X" devMode.dmFields
                printfn "[DEBUG]   Fields breakdown:"
                printfn "[DEBUG]     DM_BITSPERPEL (0x00080000): %b" (dmFields &&& 0x00080000u <> 0u)
                printfn "[DEBUG]     DM_PELSHEIGHT (0x00040000): %b" (dmFields &&& 0x00040000u <> 0u)
                printfn "[DEBUG]     DM_PELSWIDTH (0x00020000): %b" (dmFields &&& 0x00020000u <> 0u)
                printfn "[DEBUG]     DM_DISPLAYFREQUENCY (0x00400000): %b" (dmFields &&& 0x00400000u <> 0u)
                printfn "[DEBUG]     DM_DISPLAYORIENTATION (0x00000080): %b" (dmFields &&& 0x00000080u <> 0u)

                // Before testing, verify this mode exists in our enumerated modes
                printfn "[DEBUG] Verifying mode exists in enumerated modes..."
                let allModes = getAllDisplayModes displayId
                let modeExists = allModes |> List.exists (fun m -> 
                    m.Width = mode.Width && m.Height = mode.Height && m.RefreshRate = mode.RefreshRate)
                printfn "[DEBUG] Mode %dx%d @ %dHz exists in enumerated modes: %b" mode.Width mode.Height mode.RefreshRate modeExists
                
                if not modeExists then
                    // Log available modes for this resolution
                    let availableForResolution = allModes |> List.filter (fun m -> m.Width = mode.Width && m.Height = mode.Height)
                    if availableForResolution.Length > 0 then
                        printfn "[DEBUG] Available refresh rates for %dx%d:" mode.Width mode.Height
                        availableForResolution |> List.iter (fun m -> printfn "[DEBUG]   - %dHz" m.RefreshRate)
                    else
                        printfn "[DEBUG] No modes found for resolution %dx%d" mode.Width mode.Height
                    
                    Error (sprintf "Mode %dx%d @ %dHz is not supported by display %s" mode.Width mode.Height mode.RefreshRate displayId)
                else
                    // Try to get the exact DEVMODE structure for the target mode
                    printfn "[DEBUG] Getting exact DEVMODE structure for target mode..."
                    let mutable finalDevMode = 
                        match getExactDevModeForMode displayId mode with
                        | Some exactDevMode ->
                            printfn "[DEBUG] Found exact DEVMODE - using it instead of modified current mode"
                            
                            // Use the exact mode but preserve orientation
                            let mutable targetDevMode = exactDevMode
                            targetDevMode.dmDisplayOrientation <- orientationToWindows orientation
                            
                            // Only add orientation field if it's different
                            if orientation <> Landscape then
                                targetDevMode.dmFields <- targetDevMode.dmFields ||| 0x00000080u // DM_DISPLAYORIENTATION
                            
                            printfn "[DEBUG] Using exact DEVMODE with fields: 0x%08X" targetDevMode.dmFields
                            targetDevMode
                        | None ->
                            printfn "[DEBUG] Could not find exact DEVMODE - using modified current mode"
                            devMode
                    
                    // Test the change first
                    printfn "[DEBUG] Testing display mode change with CDS_TEST flag..."
                    let testResult = ChangeDisplaySettingsEx(displayId, &finalDevMode, IntPtr.Zero, CDS.CDS_TEST, IntPtr.Zero)
                    printfn "[DEBUG] Test result: %d" testResult
                    
                    if testResult <> DISP.DISP_CHANGE_SUCCESSFUL then
                        let errorMsg = match testResult with
                                       | x when x = DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                                       | x when x = DISP.DISP_CHANGE_FAILED -> "Display driver failed the mode change"
                                       | x when x = DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                                       | x when x = DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                                       | x when x = DISP.DISP_CHANGE_NOTUPDATED -> "Unable to write settings to registry"
                                       | x when x = DISP.DISP_CHANGE_BADDUALVIEW -> "Bad dual view configuration"
                                       | _ -> sprintf "Unknown error code: %d" testResult
                        printfn "[DEBUG] ERROR: Display mode test failed: %s" errorMsg
                        Error (sprintf "Display mode test failed for %s: %s" displayId errorMsg)
                    else
                        // Apply the change
                        printfn "[DEBUG] Test successful! Applying display mode change..."
                        printfn "[DEBUG] Using flags: CDS_UPDATEREGISTRY (0x%08X)" CDS.CDS_UPDATEREGISTRY
                        
                        let applyResult = ChangeDisplaySettingsEx(displayId, &finalDevMode, IntPtr.Zero, CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                        printfn "[DEBUG] Apply result: %d" applyResult
                        
                        if applyResult = DISP.DISP_CHANGE_SUCCESSFUL then
                            printfn "[DEBUG] SUCCESS: Display mode applied successfully!"
                            
                            // Verify the change took effect
                            let mutable verifyMode = DEVMODE()
                            verifyMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
                            let verifyResult = EnumDisplaySettings(displayId, -1, &verifyMode)
                            if verifyResult then
                                printfn "[DEBUG] Verification - New settings:"
                                printfn "[DEBUG]   Resolution: %ux%u @ %uHz" verifyMode.dmPelsWidth verifyMode.dmPelsHeight verifyMode.dmDisplayFrequency
                                printfn "[DEBUG]   Orientation: %u" verifyMode.dmDisplayOrientation
                                
                                if verifyMode.dmPelsWidth <> uint32 mode.Width || 
                                   verifyMode.dmPelsHeight <> uint32 mode.Height ||
                                   verifyMode.dmDisplayFrequency <> uint32 mode.RefreshRate then
                                    printfn "[DEBUG] WARNING: Verified settings don't match requested settings!"
                            
                            Ok ()
                        elif applyResult = DISP.DISP_CHANGE_RESTART then
                            printfn "[DEBUG] Display mode applied, but system restart required"
                            Ok () // Still consider success, just inform user
                        else
                            let errorMsg = match applyResult with
                                           | x when x = DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                                           | x when x = DISP.DISP_CHANGE_FAILED -> "Display driver failed the mode change"
                                           | x when x = DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                                           | x when x = DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                                           | x when x = DISP.DISP_CHANGE_NOTUPDATED -> "Unable to write settings to registry"
                                           | x when x = DISP.DISP_CHANGE_BADDUALVIEW -> "Bad dual view configuration"
                                           | _ -> sprintf "Unknown error code: %d" applyResult
                            printfn "[DEBUG] ERROR: Failed to apply display mode: %s" errorMsg
                            Error (sprintf "Failed to apply display mode to %s: %s" displayId errorMsg)
        with
        | ex ->
            printfn "[DEBUG] EXCEPTION in applyDisplayMode: %s" ex.Message
            printfn "[DEBUG] Stack trace: %s" ex.StackTrace
            Error (sprintf "Exception applying display mode to %s: %s" displayId ex.Message)

    // Set display as primary
    let setPrimaryDisplay (displayId: DisplayId) =
        try
            printfn "Setting %s as primary display" displayId

            // Get current display settings
            let mutable devMode = DEVMODE()
            devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
            
            let getCurrentResult = EnumDisplaySettings(displayId, -1, &devMode)
            if not getCurrentResult then
                Error (sprintf "Could not get current display settings for %s" displayId)
            else
                // Set position to 0,0 and use SET_PRIMARY flag
                devMode.dmPositionX <- 0
                devMode.dmPositionY <- 0
                devMode.dmFields <- devMode.dmFields ||| 0x00000020u // DM_POSITION

                let result = ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, 
                                                     CDS.CDS_SET_PRIMARY ||| CDS.CDS_UPDATEREGISTRY ||| CDS.CDS_NORESET, 
                                                     IntPtr.Zero)
                
                if result = DISP.DISP_CHANGE_SUCCESSFUL then
                    printfn "Successfully set %s as primary display" displayId
                    Ok ()
                else
                    let errorMsg = match result with
                                   | x when x = DISP.DISP_CHANGE_FAILED -> "Failed to set as primary"
                                   | x when x = DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                                   | x when x = DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                                   | _ -> sprintf "Unknown error code: %d" result
                    Error (sprintf "Failed to set %s as primary: %s" displayId errorMsg)
        with
        | ex ->
            Error (sprintf "Exception setting %s as primary: %s" displayId ex.Message)

    // Enable or disable a display using CCD API
    let setDisplayEnabled (displayId: DisplayId) (enabled: bool) =
        try
            printfn "[DEBUG] ========== Starting setDisplayEnabled =========="
            printfn "[DEBUG] Display ID: %s" displayId
            printfn "[DEBUG] Target Enabled State: %b" enabled

            // Use the CCD API approach which properly disables/enables displays
            printfn "[DEBUG] Using Windows CCD API to %s display..." (if enabled then "enable" else "disable")
            
            // Get all display paths (including inactive ones for enabling)
            match getDisplayPaths true with
            | Error err ->
                printfn "[DEBUG] Failed to get display paths: %s" err
                Error (sprintf "Failed to get display configuration: %s" err)
            | Ok (pathArray, modeArray, pathCount, modeCount) ->
                printfn "[DEBUG] Got %d paths and %d modes" pathCount modeCount
                
                if enabled then
                    // Enable display - find inactive path and activate it
                    printfn "[DEBUG] Looking for inactive display path to enable..."
                    
                    // Find the path for our target display
                    match findDisplayPath displayId pathArray pathCount with
                    | Error err ->
                        printfn "[DEBUG] Could not find display path: %s" err
                        Error (sprintf "Display %s not found in configuration: %s" displayId err)
                    | Ok (targetPath, pathIndex) ->
                        printfn "[DEBUG] Found target display at path index %d" pathIndex
                        printfn "[DEBUG] Current path flags: 0x%08X (active: %b)" 
                                targetPath.flags 
                                ((targetPath.flags &&& DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE) <> 0u)
                        
                        // Check if already enabled
                        if (targetPath.flags &&& DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE) <> 0u then
                            printfn "[DEBUG] Display is already enabled"
                            Ok ()
                        else
                            // Enable the display by setting the active flag and ensuring valid mode configuration
                            let mutable updatedPath = pathArray.[pathIndex]
                            
                            printfn "[DEBUG] Current source mode index: %d, target mode index: %d" 
                                    updatedPath.sourceInfo.modeInfoIdx updatedPath.targetInfo.modeInfoIdx
                            
                            // Check if we need to set valid mode indices
                            if updatedPath.sourceInfo.modeInfoIdx = 0xFFFFFFFFu || updatedPath.targetInfo.modeInfoIdx = 0xFFFFFFFFu then
                                printfn "[DEBUG] Path has invalid mode indices, searching for valid modes..."
                                
                                // Find or create valid mode indices
                                // Look for existing modes that match this adapter
                                let mutable sourceIdx = 0xFFFFFFFFu
                                let mutable targetIdx = 0xFFFFFFFFu
                                
                                for i in 0 .. int modeCount - 1 do
                                    let mode = modeArray.[i]
                                    // Check if this mode belongs to our adapter
                                    if mode.adapterId.LowPart = updatedPath.sourceInfo.adapterId.LowPart &&
                                       mode.adapterId.HighPart = updatedPath.sourceInfo.adapterId.HighPart then
                                        if mode.infoType = 1u && sourceIdx = 0xFFFFFFFFu then // DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE
                                            sourceIdx <- uint32 i
                                            printfn "[DEBUG] Found source mode at index %d" i
                                        elif mode.infoType = 2u && targetIdx = 0xFFFFFFFFu then // DISPLAYCONFIG_MODE_INFO_TYPE_TARGET
                                            targetIdx <- uint32 i
                                            printfn "[DEBUG] Found target mode at index %d" i
                                
                                // If we found valid modes, use them
                                if sourceIdx <> 0xFFFFFFFFu then
                                    updatedPath.sourceInfo.modeInfoIdx <- sourceIdx
                                if targetIdx <> 0xFFFFFFFFu then
                                    updatedPath.targetInfo.modeInfoIdx <- targetIdx
                                    
                                printfn "[DEBUG] Updated mode indices - source: %d, target: %d" 
                                        updatedPath.sourceInfo.modeInfoIdx updatedPath.targetInfo.modeInfoIdx
                            
                            // Set the active flag
                            updatedPath.flags <- updatedPath.flags ||| DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                            
                            // Ensure target is available
                            updatedPath.targetInfo.targetAvailable <- 1
                            
                            pathArray.[pathIndex] <- updatedPath
                            
                            printfn "[DEBUG] Enabling display - setting DISPLAYCONFIG_PATH_ACTIVE flag"
                            printfn "[DEBUG] New path flags: 0x%08X" updatedPath.flags
                            printfn "[DEBUG] Target available: %d" updatedPath.targetInfo.targetAvailable
                            
                            // Try with different flag combinations for better compatibility
                            let flags = SDC.SDC_APPLY ||| SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| SDC.SDC_ALLOW_CHANGES ||| SDC.SDC_ALLOW_PATH_ORDER_CHANGES
                            
                            printfn "[DEBUG] Attempting SetDisplayConfig with flags: 0x%08X" flags
                            let setResult = SetDisplayConfig(pathCount, pathArray, modeCount, modeArray, flags)
                            
                            if setResult = ERROR.ERROR_SUCCESS then
                                printfn "[DEBUG] SUCCESS: Display %s enabled via CCD API!" displayId
                                Ok ()
                            else
                                // If that fails, try a different approach using ChangeDisplaySettingsEx
                                printfn "[DEBUG] CCD API failed (%d), trying ChangeDisplaySettingsEx approach..." setResult
                                
                                // First try topology extend to refresh the configuration
                                let extendFlags = SDC.SDC_APPLY ||| SDC.SDC_TOPOLOGY_EXTEND
                                let _ = SetDisplayConfig(0u, null, 0u, null, extendFlags)
                                
                                // Now use ChangeDisplaySettingsEx to enable the display
                                printfn "[DEBUG] Using ChangeDisplaySettingsEx to enable display..."
                                
                                // Check if we have saved state for this display
                                let savedStateResult = 
                                    match getSavedDisplayState displayId with
                                    | Some savedState ->
                                        printfn "[DEBUG] Found saved state for display %s from %s" displayId (savedState.SavedAt.ToString())
                                        printfn "[DEBUG] Restoring to: %dx%d @ %dHz at position (%d, %d)" 
                                                savedState.Resolution.Width savedState.Resolution.Height 
                                                savedState.Resolution.RefreshRate savedState.Position.X savedState.Position.Y
                                        
                                        // Create a DEVMODE structure with saved state
                                        let mutable devMode = DEVMODE()
                                        devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
                                        devMode.dmPelsWidth <- uint32 savedState.Resolution.Width
                                        devMode.dmPelsHeight <- uint32 savedState.Resolution.Height
                                        devMode.dmDisplayFrequency <- uint32 savedState.Resolution.RefreshRate
                                        devMode.dmBitsPerPel <- 32u // Default to 32-bit color
                                        devMode.dmDisplayOrientation <- orientationToWindows (intToOrientation savedState.OrientationValue)
                                        devMode.dmPositionX <- savedState.Position.X
                                        devMode.dmPositionY <- savedState.Position.Y
                                        
                                        // Set all required fields for enabling
                                        devMode.dmFields <- 0x00020000u ||| 0x00040000u ||| 0x00080000u ||| 0x00400000u ||| 0x00000020u ||| 0x00000080u
                                        // DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL | DM_DISPLAYFREQUENCY | DM_POSITION | DM_DISPLAYORIENTATION
                                        
                                        // Apply the display settings
                                        printfn "[DEBUG] Calling ChangeDisplaySettingsEx to restore display to saved state..."
                                        let changeResult = ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                                        
                                        if changeResult = DISP.DISP_CHANGE_SUCCESSFUL then
                                            printfn "[DEBUG] SUCCESS: Display %s restored to saved state!" displayId
                                            Some (Ok ())
                                        else
                                            // Fall back to default behavior if restore fails
                                            printfn "[DEBUG] Failed to restore saved state (error %d), falling back to auto-detect..." changeResult
                                            None
                                    
                                    | None ->
                                        printfn "[DEBUG] No saved state found for display %s, using auto-detection..." displayId
                                        None
                                
                                // If saved state restore succeeded, use that result; otherwise fall back
                                match savedStateResult with
                                | Some result -> result
                                | None ->
                                    // Fallback: auto-detect best mode and position
                                    // Get available modes for this display
                                    let availableModes = getAllDisplayModes displayId
                                    if not availableModes.IsEmpty then
                                        // Find the best mode - prefer highest resolution, then highest refresh rate
                                        let preferredMode = 
                                            availableModes
                                            |> List.sortByDescending (fun m -> (m.Width * m.Height, m.RefreshRate))
                                            |> List.head
                                            
                                        printfn "[DEBUG] Enabling display with mode: %dx%d @ %dHz" 
                                                preferredMode.Width preferredMode.Height preferredMode.RefreshRate
                                        
                                        // Create a DEVMODE structure for enabling the display
                                        let mutable devMode = DEVMODE()
                                        devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
                                        devMode.dmPelsWidth <- uint32 preferredMode.Width
                                        devMode.dmPelsHeight <- uint32 preferredMode.Height
                                        devMode.dmDisplayFrequency <- uint32 preferredMode.RefreshRate
                                        devMode.dmBitsPerPel <- uint32 preferredMode.BitsPerPixel
                                        devMode.dmDisplayOrientation <- DMDO.DMDO_DEFAULT
                                        
                                        // Smart positioning - find a good position for the display
                                        let (posX, posY) = 
                                            try
                                                // Get current active displays to find a good position
                                                let activeMonitors = getActiveMonitorInfo()
                                                if activeMonitors.Count > 0 then
                                                    // Find the rightmost display and place our display to its right
                                                    let rightmostX = 
                                                        activeMonitors
                                                        |> Map.toSeq
                                                        |> Seq.map (fun (_, monitor) -> monitor.rcMonitor.right)
                                                        |> Seq.max
                                                    (rightmostX, 0)
                                                else
                                                    // No active displays found, use default position
                                                    (0, 0)
                                            with
                                            | _ -> 
                                                printfn "[DEBUG] Could not determine smart position, using default"
                                                (0, 0)
                                        
                                        devMode.dmPositionX <- posX
                                        devMode.dmPositionY <- posY
                                        
                                        printfn "[DEBUG] Positioning display at (%d, %d)" posX posY
                                    
                                        // Set all required fields for enabling
                                        devMode.dmFields <- 0x00020000u ||| 0x00040000u ||| 0x00080000u ||| 0x00400000u ||| 0x00000020u
                                        // DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL | DM_DISPLAYFREQUENCY | DM_POSITION
                                        
                                        // Apply the display settings
                                        printfn "[DEBUG] Calling ChangeDisplaySettingsEx to enable display..."
                                        let changeResult = ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                                        
                                        if changeResult = DISP.DISP_CHANGE_SUCCESSFUL then
                                            printfn "[DEBUG] SUCCESS: Display %s enabled via ChangeDisplaySettingsEx!" displayId
                                            Ok ()
                                        else
                                            let errorMsg = match changeResult with
                                                           | x when x = DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                                                           | x when x = DISP.DISP_CHANGE_FAILED -> "Display driver failed"
                                                           | x when x = DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                                                           | x when x = DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                                                           | x when x = DISP.DISP_CHANGE_NOTUPDATED -> "Unable to update registry"
                                                           | x when x = DISP.DISP_CHANGE_RESTART -> "Restart required"
                                                           | _ -> sprintf "Unknown error code: %d" changeResult
                                            printfn "[DEBUG] ERROR: ChangeDisplaySettingsEx failed: %s" errorMsg
                                            Error (sprintf "Failed to enable display %s: %s" displayId errorMsg)
                                    else
                                        printfn "[DEBUG] ERROR: No display modes available for %s" displayId
                                        Error (sprintf "No display modes available for %s" displayId)
                else
                    // Disable display - use only active paths and exclude the target
                    printfn "[DEBUG] Getting active display paths to disable one..."
                    
                    // Get only active display paths
                    match getDisplayPaths false with
                    | Error err ->
                        printfn "[DEBUG] Failed to get active display paths: %s" err
                        Error (sprintf "Failed to get active display configuration: %s" err)
                    | Ok (activePathArray, activeModeArray, activePathCount, activeModeCount) ->
                        printfn "[DEBUG] Got %d active paths" activePathCount
                        
                        // Find the path for our target display in active paths
                        match findDisplayPath displayId activePathArray activePathCount with
                        | Error err ->
                            printfn "[DEBUG] Could not find display in active paths (may already be disabled): %s" err
                            Ok () // Already disabled
                        | Ok (targetPath, pathIndex) ->
                            printfn "[DEBUG] Found active display at path index %d" pathIndex
                            
                            // Save display state before disabling
                            printfn "[DEBUG] Saving display state before disabling..."
                            let stateSaved = saveDisplayState displayId
                            if stateSaved then
                                printfn "[DEBUG] Display state saved successfully"
                            else
                                printfn "[DEBUG] Warning: Failed to save display state, continuing with disable"
                            
                            // Method 1: Try to disable by clearing active flag first
                            let mutable updatedPath = activePathArray.[pathIndex]
                            updatedPath.flags <- updatedPath.flags &&& ~~~DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                            activePathArray.[pathIndex] <- updatedPath
                            
                            printfn "[DEBUG] Attempting to disable by clearing DISPLAYCONFIG_PATH_ACTIVE flag..."
                            let clearFlagResult = SetDisplayConfig(activePathCount, activePathArray, activeModeCount, activeModeArray,
                                                                 SDC.SDC_APPLY ||| SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| SDC.SDC_ALLOW_CHANGES)
                            
                            if clearFlagResult = ERROR.ERROR_SUCCESS then
                                printfn "[DEBUG] SUCCESS: Display disabled by clearing active flag!"
                                Ok ()
                            else
                                printfn "[DEBUG] Clearing active flag failed (%d), trying path removal..." clearFlagResult
                                
                                // Method 2: Remove the display path entirely from the configuration
                                if activePathCount > 1u then
                                    // Create new arrays without the target display path
                                    let newPathArray = 
                                        if pathIndex = 0 then
                                            Array.sub activePathArray 1 (int activePathCount - 1)
                                        elif pathIndex = int activePathCount - 1 then
                                            Array.sub activePathArray 0 pathIndex
                                        else
                                            let beforeTarget = Array.sub activePathArray 0 pathIndex
                                            let afterTarget = Array.sub activePathArray (pathIndex + 1) (int activePathCount - pathIndex - 1)
                                            Array.append beforeTarget afterTarget
                                    
                                    let newPathCount = activePathCount - 1u
                                    
                                    // Keep all modes as Windows will ignore unused ones
                                    let newModeArray = activeModeArray
                                    let newModeCount = activeModeCount
                                    
                                    printfn "[DEBUG] Removing display path - new configuration has %d paths (was %d)" newPathCount activePathCount
                                    
                                    // Apply the configuration without the disabled display
                                    let setResult = SetDisplayConfig(newPathCount, newPathArray, newModeCount, newModeArray, 
                                                                   SDC.SDC_APPLY ||| SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| SDC.SDC_ALLOW_CHANGES)
                                    
                                    if setResult = ERROR.ERROR_SUCCESS then
                                        printfn "[DEBUG] SUCCESS: Display %s disabled via path removal!" displayId
                                        Ok ()
                                    else
                                        let errorMsg = match setResult with
                                                       | x when x = ERROR.ERROR_INVALID_PARAMETER -> "Invalid parameter"
                                                       | x when x = ERROR.ERROR_NOT_SUPPORTED -> "Operation not supported"
                                                       | x when x = ERROR.ERROR_ACCESS_DENIED -> "Access denied"
                                                       | x when x = ERROR.ERROR_GEN_FAILURE -> "General failure"
                                                       | _ -> sprintf "Unknown error code: %d" setResult
                                        printfn "[DEBUG] ERROR: Failed to disable display: %s" errorMsg
                                        Error (sprintf "Failed to disable display %s: %s" displayId errorMsg)
                                else
                                    printfn "[DEBUG] ERROR: Cannot disable the last remaining display"
                                    Error "Cannot disable the last remaining display"
        with
        | ex ->
            printfn "[DEBUG] EXCEPTION in setDisplayEnabled: %s" ex.Message
            printfn "[DEBUG] Stack trace: %s" ex.StackTrace
            Error (sprintf "Exception setting display %s enabled state: %s" displayId ex.Message)

    // Test display mode temporarily for 15 seconds then revert
    let testDisplayMode (displayId: DisplayId) (mode: DisplayMode) (orientation: DisplayOrientation) onComplete =
        async {
            try
                printfn "[DEBUG] ========== Starting testDisplayMode =========="
                printfn "[DEBUG] Testing mode %dx%d @ %dHz for 15 seconds" mode.Width mode.Height mode.RefreshRate
                
                // Get current display settings to restore later
                let mutable currentDevMode = DEVMODE()
                currentDevMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
                let getCurrentResult = EnumDisplaySettings(displayId, -1, &currentDevMode)
                
                if not getCurrentResult then
                    printfn "[DEBUG] ERROR: Could not get current display settings for test mode"
                    onComplete (Error "Could not get current display settings")
                else
                    let originalMode = {
                        Width = int currentDevMode.dmPelsWidth
                        Height = int currentDevMode.dmPelsHeight
                        RefreshRate = int currentDevMode.dmDisplayFrequency
                        BitsPerPixel = int currentDevMode.dmBitsPerPel
                    }
                    let originalOrientation = windowsToOrientation currentDevMode.dmDisplayOrientation
                    
                    printfn "[DEBUG] Original mode: %dx%d @ %dHz, orientation: %A" 
                            originalMode.Width originalMode.Height originalMode.RefreshRate originalOrientation
                    
                    // Apply the test mode
                    match applyDisplayMode displayId mode orientation with
                    | Ok _ ->
                        printfn "[DEBUG] Test mode applied successfully, waiting 15 seconds..."
                        
                        // Wait for 15 seconds
                        do! Async.Sleep(15000)
                        
                        // Revert to original mode
                        printfn "[DEBUG] Reverting to original mode..."
                        match applyDisplayMode displayId originalMode originalOrientation with
                        | Ok _ ->
                            printfn "[DEBUG] Successfully reverted to original mode"
                            onComplete (Ok "Test completed - reverted to original mode")
                        | Error err ->
                            printfn "[DEBUG] ERROR: Failed to revert to original mode: %s" err
                            onComplete (Error (sprintf "Test completed but failed to revert: %s" err))
                    | Error err ->
                        printfn "[DEBUG] ERROR: Failed to apply test mode: %s" err
                        onComplete (Error (sprintf "Failed to apply test mode: %s" err))
            with
            | ex ->
                printfn "[DEBUG] EXCEPTION in testDisplayMode: %s" ex.Message
                onComplete (Error (sprintf "Exception during test: %s" ex.Message))
        }
    
    // Initialize the module - load saved display states
    do
        printfn "[DEBUG] Initializing WindowsDisplaySystem - loading saved display states..."
        initializeStateCache()