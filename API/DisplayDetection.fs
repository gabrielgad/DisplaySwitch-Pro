namespace DisplaySwitchPro

open System
open System.Collections.Generic
open System.Management
open System.Runtime.InteropServices
open WindowsAPI

// Configuration constants for display detection
module private DetectionConstants =
    // Positioning constants
    let InactiveDisplayOffset = 5000 // Pixels to offset inactive displays to prevent overlap
    
    // Fallback display values
    let DefaultDisplayWidth = 1920
    let DefaultDisplayHeight = 1080
    let DefaultRefreshRate = 60
    let DefaultBitsPerPixel = 32

// Display enumeration and detection functionality
module DisplayDetection =
    
    // WMI monitor information structure
    type WmiMonitorInfo = {
        InstanceName: string
        FriendlyName: string
        TargetId: uint32 option
    }

    // Extract target ID from WMI instance name
    let private extractTargetId (instanceName: string) =
        try
            // Example: "DISPLAY\SAM713F\5&12e08716&0&UID176390_0" → 176390
            let uidMatch = System.Text.RegularExpressions.Regex.Match(instanceName, @"UID(\d+)")
            if uidMatch.Success then
                let targetIdStr = uidMatch.Groups.[1].Value
                match System.UInt32.TryParse(targetIdStr) with
                | true, targetId -> Some targetId
                | false, _ -> None
            else
                None
        with
        | ex ->
            printfn "Error extracting target ID from %s: %s" instanceName ex.Message
            None

    // Get monitor friendly names and target IDs using WMI
    let private getWmiMonitorInfo() =
        try
            let monitors = System.Collections.Generic.List<WmiMonitorInfo>()
            
            // Query WmiMonitorID for friendly names and instance names
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
                        
                        let targetId = extractTargetId instanceName
                        
                        let monitorInfo = {
                            InstanceName = instanceName
                            FriendlyName = fullName
                            TargetId = targetId
                        }
                        
                        // Add to list - WMI monitors should appear in same order as displays
                        monitors.Add(monitorInfo)
                        
                        // Debug logging for target ID mapping
                        match targetId with
                        | Some id -> printfn "[DEBUG] WMI Monitor: %s -> %s (Target ID: %u)" instanceName fullName id
                        | None -> printfn "[DEBUG] WMI Monitor: %s -> %s (No Target ID found)" instanceName fullName
                
                with ex -> 
                    printfn "Error processing WMI monitor: %s" ex.Message
            
            monitors
        with
        | ex -> 
            printfn "WMI monitor detection failed: %s" ex.Message
            System.Collections.Generic.List<WmiMonitorInfo>()

    // Legacy compatibility function for existing code
    let private getMonitorFriendlyNames() =
        let wmiInfo = getWmiMonitorInfo()
        let friendlyNames = System.Collections.Generic.List<string>()
        for info in wmiInfo do
            friendlyNames.Add(info.FriendlyName)
        friendlyNames
    
    // Get all display devices (including inactive ones)
    // Helper function to try getting a single display device at index
    let private tryGetDisplayDevice (index: uint32) =
        let mutable displayDevice = WindowsAPI.DISPLAY_DEVICE()
        displayDevice.cb <- Marshal.SizeOf(typeof<WindowsAPI.DISPLAY_DEVICE>)
        
        let result = WindowsAPI.EnumDisplayDevices(null, index, &displayDevice, 0u)
        if result then
            // Skip mirroring drivers
            let isMirror = (displayDevice.StateFlags &&& WindowsAPI.Flags.DISPLAY_DEVICE_MIRRORING_DRIVER) <> 0u
            if not isMirror then
                Some displayDevice
            else
                Some displayDevice // Include mirror devices but mark them
        else
            None
    
    // Functional tail-recursive device enumeration
    let private enumerateDisplayDevicesRec() =
        let rec loop index acc =
            match tryGetDisplayDevice index with
            | Some device -> 
                // Filter out mirroring drivers
                let isMirror = (device.StateFlags &&& WindowsAPI.Flags.DISPLAY_DEVICE_MIRRORING_DRIVER) <> 0u
                let newAcc = if not isMirror then device :: acc else acc
                loop (index + 1u) newAcc
            | None -> 
                List.rev acc
        loop 0u []
    
    // Pure functional wrapper
    let private getAllDisplayDevices() =
        enumerateDisplayDevicesRec()
    
    // Get current display settings including refresh rate
    let getCurrentDisplaySettings (deviceName: string) =
        try
            let mutable devMode = WindowsAPI.DEVMODE()
            devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
            
            // ENUM_CURRENT_SETTINGS = -1 to get current display mode
            let result = WindowsAPI.EnumDisplaySettings(deviceName, -1, &devMode)
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
    let createGroupedResolutions (modes: DisplayMode list) =
        modes
        |> List.groupBy (fun m -> (m.Width, m.Height))
        |> List.map (fun ((w, h), modelist) -> 
            ((w, h), modelist |> List.map (fun m -> m.RefreshRate) |> List.distinct |> List.sort))
        |> Map.ofList

    // Get exact DEVMODE structure for a specific mode
    let getExactDevModeForMode (deviceName: string) (targetMode: DisplayMode) =
        try
            let mutable modeIndex = 0
            let mutable continueEnum = true
            let mutable foundMode = None
            
            while continueEnum && foundMode.IsNone do
                let mutable devMode = WindowsAPI.DEVMODE()
                devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
                
                let result = WindowsAPI.EnumDisplaySettings(deviceName, modeIndex, &devMode)
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
    // Helper function to try getting a single display mode at index
    let private tryGetDisplayMode (deviceName: string) (index: int) =
        let mutable devMode = WindowsAPI.DEVMODE()
        devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
        
        let result = WindowsAPI.EnumDisplaySettings(deviceName, index, &devMode)
        if result then
            let mode = {
                Width = int devMode.dmPelsWidth
                Height = int devMode.dmPelsHeight
                RefreshRate = int devMode.dmDisplayFrequency
                BitsPerPixel = int devMode.dmBitsPerPel
            }
            
            // Validate mode
            if mode.Width > 0 && mode.Height > 0 && mode.RefreshRate > 0 then
                Some mode
            else
                None
        else
            None
    
    // Functional tail-recursive enumeration
    let private enumerateModesRec (deviceName: string) =
        let rec loop index acc =
            match tryGetDisplayMode deviceName index with
            | Some mode -> 
                // Commented out verbose mode logging - only log if needed for debugging
                // if index < 10 then
                //     printfn "[DEBUG] Mode %d: %dx%d @ %dHz, %d bpp" 
                //             index mode.Width mode.Height mode.RefreshRate mode.BitsPerPixel
                loop (index + 1) (mode :: acc)
            | None -> 
                // printfn "[DEBUG] EnumDisplaySettings stopped at index %d" index
                List.rev acc
        loop 0 []
    
    // Main function with Result type for error handling
    let getAllDisplayModes (deviceName: string) =
        try 
            // Commented out verbose mode enumeration logging
            // printfn "Enumerating all display modes for %s..." deviceName
            let modes = enumerateModesRec deviceName
            let uniqueModes = modes |> List.distinct
            // Commented out verbose mode listing - only log count
            // printfn "Found %d unique display modes for %s" uniqueModes.Length deviceName
            // uniqueModes 
            // |> List.take (min 5 uniqueModes.Length)
            // |> List.iter (fun mode -> 
            //     printfn "  Mode: %dx%d @ %dHz" mode.Width mode.Height mode.RefreshRate)
            
            uniqueModes
        with 
        | ex -> 
            printfn "Error enumerating display modes for %s: %s" deviceName ex.Message
            []

    // Get monitor info for active displays
    let private getActiveMonitorInfo() =
        let mutable monitors = Map.empty
        
        let monitorCallback = 
            WindowsAPI.MonitorEnumDelegate(fun hMonitor hdcMonitor lprcMonitor dwData ->
                let mutable monitorInfo = WindowsAPI.MONITORINFOEX()
                monitorInfo.cbSize <- Marshal.SizeOf(typeof<WindowsAPI.MONITORINFOEX>)
                
                let result = WindowsAPI.GetMonitorInfo(hMonitor, &monitorInfo)
                if result then
                    monitors <- Map.add monitorInfo.szDevice monitorInfo monitors
                
                true // Continue enumeration
            )
        
        WindowsAPI.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, monitorCallback, IntPtr.Zero) |> ignore
        monitors
    
    // Convert Windows data to domain types
    let private convertToDisplayInfoWithName (device: WindowsAPI.DISPLAY_DEVICE) (monitorInfo: WindowsAPI.MONITORINFOEX option) (monitorName: string) =
        let isAttached = (device.StateFlags &&& WindowsAPI.Flags.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) <> 0u
        let isPrimary = (device.StateFlags &&& WindowsAPI.Flags.DISPLAY_DEVICE_PRIMARY_DEVICE) <> 0u
        
        // Use the provided monitor name (already resolved from WMI)
        
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
                    { Width = width; Height = height; RefreshRate = DetectionConstants.DefaultRefreshRate }
            
            // Get all available display modes for active displays
            let availableModes = getAllDisplayModes device.DeviceName
            let capabilities = 
                if availableModes.Length > 0 then
                    let currentMode = 
                        match actualSettings with
                        | Some settings -> settings
                        | None -> { Width = width; Height = height; RefreshRate = DetectionConstants.DefaultRefreshRate; BitsPerPixel = DetectionConstants.DefaultBitsPerPixel }
                    
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
            let inactiveOffset = DetectionConstants.InactiveDisplayOffset
            {
                Id = device.DeviceName
                Name = sprintf "%s [Inactive]" fullName
                Resolution = { Width = DetectionConstants.DefaultDisplayWidth; Height = DetectionConstants.DefaultDisplayHeight; RefreshRate = DetectionConstants.DefaultRefreshRate }
                Position = { X = inactiveOffset; Y = 0 } // Position away from active displays
                Orientation = Landscape
                IsPrimary = isPrimary
                IsEnabled = false // Inactive displays are disabled
                Capabilities = None // No capabilities for inactive displays (can be populated later if needed)
            }
    
    // Create mapping from display ID to correct target ID using raw Windows API calls
    let getDisplayTargetIdMapping() =
        try
            printfn "[DEBUG] Building display-to-target ID mapping using raw CCD API..."
            
            // Use raw Windows API calls to get display paths (avoid module dependency)
            let mutable pathCount = 0u
            let mutable modeCount = 0u
            
            // Get buffer sizes - use ALL_PATHS to get complete enumeration order
            let sizeResult = WindowsAPI.GetDisplayConfigBufferSizes(WindowsAPI.QDC.QDC_ALL_PATHS, &pathCount, &modeCount)
            if sizeResult = 0 then
                let pathArray = Array.zeroCreate<WindowsAPI.DISPLAYCONFIG_PATH_INFO> (int pathCount)
                let modeArray = Array.zeroCreate<WindowsAPI.DISPLAYCONFIG_MODE_INFO> (int modeCount)
                
                // Query display configuration - use ALL_PATHS to get complete enumeration order
                let queryResult = WindowsAPI.QueryDisplayConfig(WindowsAPI.QDC.QDC_ALL_PATHS, &pathCount, pathArray, &modeCount, modeArray, IntPtr.Zero)
                if queryResult = 0 then
                    printfn "[DEBUG] Found %d CCD paths for mapping" pathCount
                    
                    // Get WMI monitor information with target IDs  
                    let wmiMonitors = getWmiMonitorInfo()
                    let wmiByTargetId = 
                        wmiMonitors 
                        |> Seq.choose (fun wmi -> 
                            match wmi.TargetId with 
                            | Some targetId -> Some (targetId, wmi.FriendlyName)
                            | None -> None)
                        |> Map.ofSeq
                    
                    // Build mapping by matching CCD source IDs to Windows display device names
                    // Only include paths that have corresponding WMI hardware information
                    let mapping = 
                        pathArray
                        |> Array.take (int pathCount)
                        |> Array.choose (fun path ->
                            // Convert source ID to Windows display device name (sourceId 0 = DISPLAY1, etc.)
                            let displayName = sprintf "\\\\.\\DISPLAY%d" (int path.sourceInfo.id + 1)
                            let targetId = path.targetInfo.id
                            
                            // Only include paths that have WMI information (filter out "Unknown" paths)
                            match Map.tryFind targetId wmiByTargetId with
                            | Some friendlyName ->
                                printfn "[DEBUG] CCD Path: %s (Source %d) -> Target ID %u (%s)" displayName (int path.sourceInfo.id) targetId friendlyName
                                Some (displayName, targetId)
                            | None ->
                                printfn "[DEBUG] Skipping CCD Path: %s (Source %d) -> Target ID %u (No WMI data)" displayName (int path.sourceInfo.id) targetId
                                None)
                        |> Array.groupBy fst  // Group by display name
                        |> Array.map (fun (displayName, paths) ->
                            // If multiple paths for same display, match to correct target ID
                            let bestPath = 
                                if Array.length paths > 1 then
                                    // Get the display number from the name (\\.\DISPLAY4 -> 4)
                                    let displayNum = displayName.Substring(11) |> int
                                    // Get WMI monitors in enumeration order (same as Windows display order)
                                    let wmiList = getWmiMonitorInfo() |> Seq.toList
                                    if displayNum <= wmiList.Length then
                                        let expectedWmi = wmiList.[displayNum - 1]
                                        match expectedWmi.TargetId with
                                        | Some expectedTargetId ->
                                            // Find path that matches this display's actual target ID
                                            paths |> Array.tryFind (fun (_, targetId) -> targetId = expectedTargetId)
                                            |> Option.defaultValue (paths |> Array.head)
                                        | None -> paths |> Array.head
                                    else
                                        paths |> Array.head
                                else
                                    paths |> Array.head
                            bestPath)
                        |> Map.ofArray
                    
                    printfn "[DEBUG] Created CCD mapping for %d displays" (Map.count mapping)
                    mapping
                else
                    printfn "[ERROR] QueryDisplayConfig failed with code: %d" queryResult
                    Map.empty
            else
                printfn "[ERROR] GetDisplayConfigBufferSizes failed with code: %d" sizeResult
                Map.empty
                
        with
        | ex ->
            printfn "[ERROR] Failed to build display-target ID mapping: %s" ex.Message
            Map.empty

    // Main function to get all connected displays using simple index-based WMI mapping
    let getConnectedDisplays() : DisplayInfo list =
        try
            printfn "Detecting displays on Win32NT..."
            
            // Get monitor friendly names from WMI (simple approach that works)
            let wmiMonitors = getMonitorFriendlyNames()
            printfn "[DEBUG] Found %d WMI monitor entries" wmiMonitors.Count
            
            // Get all display devices (enumerated by Windows) 
            let allDevices = getAllDisplayDevices()
            printfn "Found %d display devices" allDevices.Length
            
            // Get active monitor information
            let activeMonitors = getActiveMonitorInfo()
            
            // Convert each display device using simple index-based WMI mapping
            allDevices
            |> List.mapi (fun index device ->
                let monitorInfo = Map.tryFind device.DeviceName activeMonitors
                
                // Get friendly name using simple index-based mapping (proven to work)
                let monitorName = 
                    if index < wmiMonitors.Count then
                        wmiMonitors.[index]
                    else
                        device.DeviceString
                
                convertToDisplayInfoWithName device monitorInfo monitorName
            )
            
        with
        | ex -> 
            printfn "Windows display detection failed: %s" ex.Message
            []