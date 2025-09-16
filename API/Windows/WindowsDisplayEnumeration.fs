namespace DisplaySwitchPro

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open WindowsAPI
open WindowsDisplayNumbering
open MonitorBoundsDetection

// Complete Windows display enumeration using multiple APIs for comprehensive information
module WindowsDisplayEnumeration =

    // Complete display enumeration result
    type EnumeratedDisplay = {
        APIDeviceName: string                    // e.g., \\.\DISPLAY1
        WindowsDisplayNumber: int                // Windows Display Settings number (1, 2, 3, 4)
        FriendlyName: string                     // Hardware name (e.g., SAM LS24AG30x)
        ManufacturerName: string                 // Manufacturer (e.g., SAM)
        ProductName: string                      // Product (e.g., LS24AG30x)
        SerialNumber: string                     // Hardware serial
        EDIDIdentifier: string                   // EDID ID (e.g., SAM7179)
        UID: uint32                              // Hardware UID
        IsAttachedToDesktop: bool                // Windows API: attached to desktop
        IsPrimary: bool                          // Windows API: primary device
        IsActive: bool                           // CCD/Monitor API: currently active
        HasMonitorInfo: bool                     // Has physical monitor information
        DeviceStateFlags: uint32                 // Raw Windows device state flags
        MonitorBounds: (int * int * int * int) option  // Monitor bounds (left, top, right, bottom) if active
    }

    // Get all display devices using Windows API
    let private getAllDisplayDevicesWithFlags() =
        let rec enumerateDevices index acc =
            let mutable device = DISPLAY_DEVICE()
            device.cb <- Marshal.SizeOf(typeof<DISPLAY_DEVICE>)

            let result = EnumDisplayDevices(null, index, &device, 0u)
            if result then
                let isMirror = (device.StateFlags &&& Flags.DISPLAY_DEVICE_MIRRORING_DRIVER) <> 0u
                let newAcc = if not isMirror then device :: acc else acc
                enumerateDevices (index + 1u) newAcc
            else
                List.rev acc

        enumerateDevices 0u []


    // CCD path information for three-way correlation
    type CCDPath = {
        SourceID: int
        TargetID: uint32
        APIDeviceName: string
    }

    // Get detailed CCD path information for three-way correlation
    let private getCCDPathInformation() : CCDPath list =
        try
            let mutable pathCount = 0u
            let mutable modeCount = 0u

            let sizeResult = GetDisplayConfigBufferSizes(QDC.QDC_ONLY_ACTIVE_PATHS, &pathCount, &modeCount)
            if sizeResult = 0 && pathCount > 0u then
                let pathArray = Array.zeroCreate<DISPLAYCONFIG_PATH_INFO> (int pathCount)
                let modeArray = Array.zeroCreate<DISPLAYCONFIG_MODE_INFO> (int modeCount)

                let queryResult = QueryDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS, &pathCount, pathArray, &modeCount, modeArray, IntPtr.Zero)
                if queryResult = 0 then
                    Logging.logVerbosef " CCD found %d active paths:" (int pathCount)
                    pathArray
                    |> Array.take (int pathCount)
                    |> Array.map (fun path ->
                        let displayName = sprintf "\\\\.\\DISPLAY%d" (int path.sourceInfo.id + 1)
                        let ccdPath = {
                            SourceID = int path.sourceInfo.id
                            TargetID = uint32 path.targetInfo.id
                            APIDeviceName = displayName
                        }
                        Logging.logVerbosef "   Path %d: Source ID %d -> %s (Target ID: %d)" ccdPath.SourceID ccdPath.SourceID displayName (int ccdPath.TargetID)
                        ccdPath)
                    |> List.ofArray
                else
                    printfn "[WARNING] CCD QueryDisplayConfig failed: %d" queryResult
                    []
            else
                printfn "[WARNING] CCD GetDisplayConfigBufferSizes failed or no active paths: %d" sizeResult
                []
        with
        | ex ->
            printfn "[WARNING] CCD API failed: %s" ex.Message
            []

    // Legacy function for backward compatibility
    let private getActiveDisplaysFromCCD() =
        getCCDPathInformation()
        |> List.map (fun path -> path.APIDeviceName)
        |> Set.ofList

    // Enumerate all displays with complete information
    let enumerateAllDisplays() : EnumeratedDisplay list =
        try
            Logging.logVerbose "Enumerating all Windows displays with complete information..."

            // Get Windows Display numbering algorithm results
            let displayAdapter = createDisplayAdapter()
            let mappings = displayAdapter.Mappings

            if mappings.IsEmpty then
                printfn "[WARNING] No Windows Display numbering mappings available"
                []
            else
                // Get all Windows API display devices
                let allDevices = getAllDisplayDevicesWithFlags()
                Logging.logVerbosef " Found %d Windows API display devices" allDevices.Length

                // Get active monitor bounds
                let activeBounds = MonitorBoundsDetection.getActiveMonitorBounds()
                let monitorBounds = activeBounds |> Map.map (fun _ bounds -> (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom))
                Logging.logVerbosef " Found %d active monitor bounds" (Map.count monitorBounds)

                // Get CCD path information for three-way correlation
                let ccdPaths = getCCDPathInformation()
                Logging.logVerbosef " Found %d CCD paths for correlation" ccdPaths.Length

                // Get active displays from CCD (legacy)
                let activeDisplaysFromCCD = getActiveDisplaysFromCCD()
                Logging.logVerbosef " Found %d active displays from CCD" (Set.count activeDisplaysFromCCD)

                // Three-way correlation: WMI + EDID + CCD paths
                Logging.logVerbosef " Starting three-way correlation (WMI + EDID + CCD)..."

                mappings
                |> List.groupBy (fun mapping -> mapping.UID)
                |> List.choose (fun (uid, mappingsForUID) ->
                    // For each UID, find which mapping (if any) corresponds to an active CCD path
                    let activeMapping =
                        mappingsForUID
                        |> List.tryFind (fun mapping ->
                            // Check if this mapping's API device matches a CCD path with the same UID as target
                            ccdPaths
                            |> List.exists (fun ccdPath ->
                                ccdPath.APIDeviceName = mapping.APIDeviceName && ccdPath.TargetID = mapping.UID))

                    match activeMapping with
                    | Some mapping ->
                        let ccdPath = ccdPaths |> List.find (fun p -> p.APIDeviceName = mapping.APIDeviceName && p.TargetID = mapping.UID)
                        Logging.logVerbosef " ✓ Three-way correlation: UID %u (%s) -> %s -> CCD Target %u (ACTIVE)"
                            mapping.UID mapping.FriendlyName mapping.APIDeviceName ccdPath.TargetID
                        Some (mapping, true)  // (mapping, isActive)
                    | None ->
                        // Include inactive displays but mark them as inactive
                        let inactiveMapping = mappingsForUID |> List.head
                        Logging.logVerbosef " ✗ Three-way correlation: UID %u (%s) -> No active CCD path found (INACTIVE)"
                            uid inactiveMapping.FriendlyName
                        Some (inactiveMapping, false)  // (mapping, isActive)
                )
                |> List.choose (fun (mapping, isActiveByCCD) ->
                    // Find corresponding Windows API device for this mapping
                    let apiDeviceName = mapping.APIDeviceName

                    // Find the device in allDevices list
                    let deviceOption = allDevices |> List.tryFind (fun d -> d.DeviceName = apiDeviceName)

                    match deviceOption with
                    | Some device ->
                        // Check basic Windows API flags
                        let isAttachedToDesktop = (device.StateFlags &&& Flags.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) <> 0u
                        let isPrimary = (device.StateFlags &&& Flags.DISPLAY_DEVICE_PRIMARY_DEVICE) <> 0u

                        // Get monitor bounds if available
                        let bounds = Map.tryFind device.DeviceName monitorBounds
                        let hasMonitorInfo = bounds.IsSome

                        let display = {
                            APIDeviceName = device.DeviceName
                            WindowsDisplayNumber = mapping.WindowsDisplayNumber
                            FriendlyName = mapping.FriendlyName
                            ManufacturerName = mapping.ManufacturerName
                            ProductName = mapping.ProductName
                            SerialNumber = mapping.SerialNumber
                            EDIDIdentifier = mapping.EDIDIdentifier
                            UID = mapping.UID
                            IsAttachedToDesktop = isAttachedToDesktop
                            IsPrimary = isPrimary
                            IsActive = isActiveByCCD && hasMonitorInfo  // Use three-way correlation result
                            HasMonitorInfo = hasMonitorInfo
                            DeviceStateFlags = device.StateFlags
                            MonitorBounds = bounds
                        }

                        Logging.logVerbosef "Display %d: %s (API: %s, Active: %b, Attached: %b, HasMonitor: %b)"
                            mapping.WindowsDisplayNumber mapping.FriendlyName device.DeviceName
                            display.IsActive isAttachedToDesktop hasMonitorInfo

                        Some display
                    | None ->
                        if not isActiveByCCD then
                            // For inactive displays, create entry without API device info
                            let display = {
                                APIDeviceName = mapping.APIDeviceName
                                WindowsDisplayNumber = mapping.WindowsDisplayNumber
                                FriendlyName = sprintf "%s [Inactive]" mapping.FriendlyName
                                ManufacturerName = mapping.ManufacturerName
                                ProductName = mapping.ProductName
                                SerialNumber = mapping.SerialNumber
                                EDIDIdentifier = mapping.EDIDIdentifier
                                UID = mapping.UID
                                IsAttachedToDesktop = false
                                IsPrimary = false
                                IsActive = false
                                HasMonitorInfo = false
                                DeviceStateFlags = 0u
                                MonitorBounds = None
                            }
                            printfn "[INFO] Display %d: %s (API: %s, INACTIVE - not in Windows API)"
                                mapping.WindowsDisplayNumber display.FriendlyName mapping.APIDeviceName
                            Some display
                        else
                            printfn "[WARNING] No API device found for mapping %s (Windows Display %d)" mapping.APIDeviceName mapping.WindowsDisplayNumber
                            None
                )

        with
        | ex ->
            Logging.logErrorf " Display enumeration failed: %s" ex.Message
            []

    // Get only enabled/active displays
    let getEnabledDisplays() : EnumeratedDisplay list =
        enumerateAllDisplays()
        |> List.filter (fun display -> display.IsActive)

    // Get only disabled but connected displays
    let getDisabledDisplays() : EnumeratedDisplay list =
        enumerateAllDisplays()
        |> List.filter (fun display -> not display.IsActive && display.IsAttachedToDesktop)

    // Get all displays with status indication
    let getAllDisplaysWithStatus() : EnumeratedDisplay list =
        enumerateAllDisplays()
        |> List.sortBy (fun d -> d.WindowsDisplayNumber)

    // Helper functions for specific use cases
    let getDisplayByWindowsNumber (windowsDisplayNumber: int) : EnumeratedDisplay option =
        enumerateAllDisplays()
        |> List.tryFind (fun d -> d.WindowsDisplayNumber = windowsDisplayNumber)

    let getAPIDeviceForWindowsDisplay (windowsDisplayNumber: int) : string option =
        getDisplayByWindowsNumber windowsDisplayNumber
        |> Option.map (fun d -> d.APIDeviceName)

    let getWindowsDisplayForAPIDevice (apiDeviceName: string) : int option =
        enumerateAllDisplays()
        |> List.tryFind (fun d -> d.APIDeviceName = apiDeviceName)
        |> Option.map (fun d -> d.WindowsDisplayNumber)