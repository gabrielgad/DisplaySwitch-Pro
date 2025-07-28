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
                    
                    // Only add valid modes (basic validation)
                    if mode.Width > 0 && mode.Height > 0 && mode.RefreshRate > 0 then
                        modes <- mode :: modes
                        
                    modeIndex <- modeIndex + 1
                else
                    continueEnum <- false
            
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

    // Apply display mode changes (resolution, refresh rate, orientation)
    let applyDisplayMode (displayId: DisplayId) (mode: DisplayMode) (orientation: DisplayOrientation) =
        try
            printfn "Applying display mode to %s: %dx%d @ %dHz, orientation: %A" 
                    displayId mode.Width mode.Height mode.RefreshRate orientation

            // Get current display settings to use as base
            let mutable devMode = DEVMODE()
            devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<DEVMODE>))
            
            let getCurrentResult = EnumDisplaySettings(displayId, -1, &devMode)
            if not getCurrentResult then
                Error (sprintf "Could not get current display settings for %s" displayId)
            else
                // Update the settings we want to change
                devMode.dmPelsWidth <- uint32 mode.Width
                devMode.dmPelsHeight <- uint32 mode.Height  
                devMode.dmDisplayFrequency <- uint32 mode.RefreshRate
                devMode.dmBitsPerPel <- uint32 mode.BitsPerPixel
                devMode.dmDisplayOrientation <- orientationToWindows orientation
                
                // Set fields that indicate which settings to change
                devMode.dmFields <- 0x00020000u ||| 0x00040000u ||| 0x00400000u ||| 0x00080000u ||| 0x00000080u
                // DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY | DM_BITSPERPEL | DM_DISPLAYORIENTATION

                // Test the change first
                let testResult = ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, CDS.CDS_TEST, IntPtr.Zero)
                if testResult <> DISP.DISP_CHANGE_SUCCESSFUL then
                    let errorMsg = match testResult with
                                   | x when x = DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                                   | x when x = DISP.DISP_CHANGE_FAILED -> "Display driver failed the mode change"
                                   | x when x = DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                                   | x when x = DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                                   | _ -> sprintf "Unknown error code: %d" testResult
                    Error (sprintf "Display mode test failed for %s: %s" displayId errorMsg)
                else
                    // Apply the change
                    let applyResult = ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                    if applyResult = DISP.DISP_CHANGE_SUCCESSFUL then
                        printfn "Successfully applied display mode to %s" displayId
                        Ok ()
                    elif applyResult = DISP.DISP_CHANGE_RESTART then
                        printfn "Display mode applied to %s, restart required" displayId
                        Ok () // Still consider success, just inform user
                    else
                        let errorMsg = match applyResult with
                                       | x when x = DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                                       | x when x = DISP.DISP_CHANGE_FAILED -> "Display driver failed the mode change"
                                       | x when x = DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                                       | x when x = DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                                       | _ -> sprintf "Unknown error code: %d" applyResult
                        Error (sprintf "Failed to apply display mode to %s: %s" displayId errorMsg)
        with
        | ex ->
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