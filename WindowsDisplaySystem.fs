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

// Windows Display Detection System following ECS architecture
module WindowsDisplaySystem =
    
    open WindowsAPI
    
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