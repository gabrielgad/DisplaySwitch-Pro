namespace DisplaySwitchPro

open System
open System.Collections.Generic
open System.Management
open System.Runtime.InteropServices
open WindowsAPI

// Display enumeration and detection functionality
module DisplayDetection =
    
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
            let mutable displayDevice = WindowsAPI.DISPLAY_DEVICE()
            displayDevice.cb <- Marshal.SizeOf(typeof<WindowsAPI.DISPLAY_DEVICE>)
            
            let result = WindowsAPI.EnumDisplayDevices(null, deviceIndex, &displayDevice, 0u)
            if result then
                // Skip mirroring drivers
                let isMirror = (displayDevice.StateFlags &&& WindowsAPI.Flags.DISPLAY_DEVICE_MIRRORING_DRIVER) <> 0u
                if not isMirror then
                    devices <- displayDevice :: devices
                deviceIndex <- deviceIndex + 1u
            else
                continueEnum <- false
        
        List.rev devices
    
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
    let getAllDisplayModes (deviceName: string) =
        try
            let mutable modes = []
            let mutable modeIndex = 0
            let mutable continueEnum = true
            
            printfn "Enumerating all display modes for %s..." deviceName
            
            while continueEnum do
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
    let private convertToDisplayInfo (device: WindowsAPI.DISPLAY_DEVICE) (monitorInfo: WindowsAPI.MONITORINFOEX option) (wmiMonitors: System.Collections.Generic.List<string>) (deviceIndex: int) =
        let isAttached = (device.StateFlags &&& WindowsAPI.Flags.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) <> 0u
        let isPrimary = (device.StateFlags &&& WindowsAPI.Flags.DISPLAY_DEVICE_PRIMARY_DEVICE) <> 0u
        
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