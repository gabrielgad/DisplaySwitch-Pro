namespace DisplaySwitchPro

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open WindowsAPI
open WindowsDisplayNumbering
open WindowsDisplayEnumeration
open WMIHardwareDetection

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
            Logging.logVerbosef " Error finding exact DEVMODE: %s" ex.Message
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
                //     Logging.logVerbosef " Mode %d: %dx%d @ %dHz, %d bpp" 
                //             index mode.Width mode.Height mode.RefreshRate mode.BitsPerPixel
                loop (index + 1) (mode :: acc)
            | None -> 
                // Logging.logVerbosef " EnumDisplaySettings stopped at index %d" index
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
    

    // Helper to calculate non-overlapping positions for inactive displays
    let private calculateInactiveDisplayPosition (inactiveDisplayIndex: int) =
        let baseOffset = DetectionConstants.InactiveDisplayOffset
        let displayWidth = DetectionConstants.DefaultDisplayWidth
        let xOffset = baseOffset + (inactiveDisplayIndex * (displayWidth + 100)) // Add 100px spacing between inactive displays
        { X = xOffset; Y = 0 }

    // Convert enumerated display to DisplayInfo with business logic for disabled displays
    let private convertEnumeratedDisplayToDisplayInfo (enumDisplay: EnumeratedDisplay) : DisplayInfo =
        // Business logic: Check if this display was recently disabled
        // If the enumeration shows it as active but it has no monitor bounds, it's likely disabled
        let isActuallyActive =
            enumDisplay.IsActive &&
            enumDisplay.HasMonitorInfo &&
            enumDisplay.IsAttachedToDesktop

        // Override the enumeration result with business logic
        let effectiveIsActive =
            if enumDisplay.IsActive && not enumDisplay.HasMonitorInfo then
                printfn "[BUSINESS LOGIC] Display %s marked as inactive (no monitor bounds despite CCD active)" enumDisplay.APIDeviceName
                false
            else
                isActuallyActive
        // Create display name with proper Windows Display numbering
        let displayId =
            if enumDisplay.IsPrimary then
                sprintf "Display %d (Primary)" enumDisplay.WindowsDisplayNumber
            else
                sprintf "Display %d" enumDisplay.WindowsDisplayNumber

        let fullName = sprintf "%s (%s)" displayId enumDisplay.FriendlyName

        // Get position from monitor bounds or use default for inactive displays
        let position =
            if effectiveIsActive then
                match enumDisplay.MonitorBounds with
                | Some (left, top, right, bottom) ->
                    { X = left; Y = top }
                | None ->
                    // Active display without bounds - shouldn't happen, but use default
                    { X = 0; Y = 0 }
            else
                // Position inactive displays away from active ones
                { X = DetectionConstants.InactiveDisplayOffset; Y = 0 }

        // Get resolution from current settings or use default
        let resolution, orientation =
            if effectiveIsActive then
                match getCurrentDisplaySettings enumDisplay.APIDeviceName with
                | Some settings ->
                    let res = { Width = settings.Width; Height = settings.Height; RefreshRate = settings.RefreshRate }
                    let orient = if settings.Width > settings.Height then Landscape else Portrait
                    (res, orient)
                | None ->
                    // Fallback to bounds if available
                    match enumDisplay.MonitorBounds with
                    | Some (left, top, right, bottom) ->
                        let width = right - left
                        let height = bottom - top
                        let res = { Width = width; Height = height; RefreshRate = DetectionConstants.DefaultRefreshRate }
                        let orient = if width > height then Landscape else Portrait
                        (res, orient)
                    | None ->
                        let res = { Width = DetectionConstants.DefaultDisplayWidth; Height = DetectionConstants.DefaultDisplayHeight; RefreshRate = DetectionConstants.DefaultRefreshRate }
                        (res, Landscape)
            else
                // Default resolution for inactive displays
                let res = { Width = DetectionConstants.DefaultDisplayWidth; Height = DetectionConstants.DefaultDisplayHeight; RefreshRate = DetectionConstants.DefaultRefreshRate }
                (res, Landscape)

        // Get capabilities for active displays
        let capabilities =
            if effectiveIsActive then
                let availableModes = getAllDisplayModes enumDisplay.APIDeviceName
                if availableModes.Length > 0 then
                    let currentMode =
                        match getCurrentDisplaySettings enumDisplay.APIDeviceName with
                        | Some settings -> settings
                        | None -> { Width = resolution.Width; Height = resolution.Height; RefreshRate = resolution.RefreshRate; BitsPerPixel = DetectionConstants.DefaultBitsPerPixel }

                    Some {
                        DisplayId = sprintf "Display%d" enumDisplay.WindowsDisplayNumber  // Use Windows Display Number as unique ID
                        CurrentMode = currentMode
                        AvailableModes = availableModes
                        GroupedResolutions = createGroupedResolutions availableModes
                    }
                else None
            else None

        // Create enhanced display name for inactive displays
        let finalName = if not enumDisplay.IsActive then sprintf "%s [Inactive]" fullName else fullName

        {
            Id = sprintf "Display%d" enumDisplay.WindowsDisplayNumber  // Use Windows Display Number as unique ID
            Name = finalName
            Resolution = resolution
            Position = position
            Orientation = orientation
            IsPrimary = enumDisplay.IsPrimary
            IsEnabled = effectiveIsActive
            Capabilities = capabilities
        }

    // Main function using clean enumeration module
    let getConnectedDisplays() : DisplayInfo list =
        printfn "Detecting displays using Windows Display enumeration..."

        // Get all displays (active and inactive) using our clean enumeration
        let enumeratedDisplays = WindowsDisplayEnumeration.getAllDisplaysWithStatus()

        // Convert to DisplayInfo with business logic
        let displayInfos = enumeratedDisplays |> List.map convertEnumeratedDisplayToDisplayInfo

        // Fix positioning for inactive displays to prevent overlap
        let (activeDisplays, inactiveDisplays) = displayInfos |> List.partition (fun d -> d.IsEnabled)

        let repositionedInactiveDisplays =
            inactiveDisplays
            |> List.mapi (fun index display ->
                let newPosition = calculateInactiveDisplayPosition index
                { display with Position = newPosition })

        let finalDisplayInfos = activeDisplays @ repositionedInactiveDisplays

        Logging.logVerbose (sprintf "Found %d displays total" finalDisplayInfos.Length)
        let activeCount = activeDisplays.Length
        let inactiveCount = repositionedInactiveDisplays.Length
        Logging.logVerbose (sprintf "  - %d active displays" activeCount)
        Logging.logVerbose (sprintf "  - %d inactive/connected displays" inactiveCount)

        if inactiveCount > 0 then
            Logging.logVerbosef " Positioned inactive displays at non-overlapping locations:"
            repositionedInactiveDisplays |> List.iteri (fun i display ->
                Logging.logVerbosef "   %s at (%d, %d)" display.Id display.Position.X display.Position.Y)

        finalDisplayInfos

    // Mapping function to convert Windows Display Number back to API device name for legacy API calls
    let getAPIDeviceNameForDisplayId (displayId: DisplayId) : string option =
        // Extract Windows Display Number from displayId format "Display1" -> 1
        if displayId.StartsWith("Display") then
            let numberPart = displayId.Substring(7) // Remove "Display" prefix
            match System.Int32.TryParse(numberPart) with
            | true, windowsDisplayNumber ->
                // Get current enumeration to find the API device name for this Windows Display number
                let enumeratedDisplays = WindowsDisplayEnumeration.getAllDisplaysWithStatus()
                enumeratedDisplays
                |> List.tryFind (fun d -> d.WindowsDisplayNumber = windowsDisplayNumber)
                |> Option.map (fun d -> d.APIDeviceName)
            | false, _ -> None
        else
            None