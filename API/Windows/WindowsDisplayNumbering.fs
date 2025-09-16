namespace DisplaySwitchPro

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open WindowsAPI
open WMIHardwareDetection

// Windows Display Settings numbering algorithm based on hardware introduction order
module WindowsDisplayNumbering =

    // Hardware display mapping information
    type HardwareDisplayMapping = {
        UID: uint32                        // Hardware UID from device path
        EDIDIdentifier: string            // EDID manufacturer+product (e.g., SAM7179)
        ManufacturerName: string          // Manufacturer (e.g., SAM)
        ProductName: string               // Product name (e.g., LS24AG30x)
        FriendlyName: string              // Full friendly name (e.g., SAM LS24AG30x)
        SerialNumber: string              // Hardware serial number
        WindowsDisplayNumber: int         // Predicted Windows Display Settings number
        APIDeviceName: string             // Windows API device name (e.g., \\.\DISPLAY1)
        WMIInstanceName: string           // WMI instance identifier
        MonitorIndex: uint32              // Monitor index on the API adapter (0, 1, 2...)
    }


    // Extract EDID identifier from device path
    let private extractEDIDIdentifier (devicePath: string) =
        try
            let edidMatch = Regex.Match(devicePath, @"DISPLAY#([^#]+)#")
            if edidMatch.Success then
                edidMatch.Groups.[1].Value
            else
                "Unknown"
        with
        | ex ->
            Logging.logErrorf " Failed to extract EDID from %s: %s" devicePath ex.Message
            "Unknown"

    // Get WMI display data directly
    let private getWMIDisplayData() =
        let wmiDisplays = WMIHardwareDetection.getWMIDisplayData()
        wmiDisplays
        |> List.map (fun wmiInfo -> {
            UID = wmiInfo.UID
            EDIDIdentifier = ""  // Will be filled by API correlation
            ManufacturerName = wmiInfo.ManufacturerName
            ProductName = wmiInfo.ProductName
            FriendlyName = wmiInfo.FriendlyName
            SerialNumber = wmiInfo.SerialNumber
            WindowsDisplayNumber = 0  // Will be calculated
            APIDeviceName = ""  // Will be filled by API correlation
            WMIInstanceName = wmiInfo.WMIInstanceName
            MonitorIndex = 0u  // Will be filled by API correlation
        })

    // Correlate WMI data with Windows API enumeration
    let private correlateWithAPI (wmiDisplays: HardwareDisplayMapping list) =
        try
            let mutable correlatedDisplays = []

            // Create initial WMI display list (will be matched by UID)
            let wmiDisplaysList = wmiDisplays

            // Enumerate API displays and correlate
            let mutable adapterIndex = 0u
            let mutable continueEnum = true

            while continueEnum do
                let mutable adapter = DISPLAY_DEVICE()
                adapter.cb <- System.Runtime.InteropServices.Marshal.SizeOf(typeof<DISPLAY_DEVICE>)

                let result = EnumDisplayDevices(null, adapterIndex, &adapter, 0u)
                if result then
                    let isAttached = (adapter.StateFlags &&& Flags.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) <> 0u
                    let isMirror = (adapter.StateFlags &&& Flags.DISPLAY_DEVICE_MIRRORING_DRIVER) <> 0u

                    if isAttached && not isMirror then
                        // Get monitors for this adapter
                        let mutable monitorIndex = 0u
                        let mutable continueMonitorEnum = true

                        while continueMonitorEnum do
                            let mutable monitor = DISPLAY_DEVICE()
                            monitor.cb <- System.Runtime.InteropServices.Marshal.SizeOf(typeof<DISPLAY_DEVICE>)

                            let monitorResult = EnumDisplayDevices(adapter.DeviceName, monitorIndex, &monitor, 1u)
                            if monitorResult then
                                // Extract UID and EDID from device path
                                match WMIHardwareDetection.extractUID monitor.DeviceID with
                                | Some uid ->
                                    let edidId = extractEDIDIdentifier monitor.DeviceID

                                    // Find WMI display with this UID and create correlated display
                                    match wmiDisplaysList |> List.tryFind (fun wmi -> wmi.UID = uid) with
                                    | Some wmiDisplay ->
                                        let correlatedDisplay = {
                                            wmiDisplay with
                                                EDIDIdentifier = edidId
                                                APIDeviceName = adapter.DeviceName
                                                MonitorIndex = monitorIndex
                                        }
                                        correlatedDisplays <- correlatedDisplay :: correlatedDisplays
                                        Logging.logVerbosef " Correlated UID %u (%s) -> %s (EDID: %s, Monitor: %u)" uid wmiDisplay.FriendlyName adapter.DeviceName edidId monitorIndex
                                    | None ->
                                        printfn "[WARNING] Found UID %u in API but not in WMI data" uid

                                | None ->
                                    printfn "[WARNING] No UID found in API device path: %s" monitor.DeviceID

                                monitorIndex <- monitorIndex + 1u
                            else
                                continueMonitorEnum <- false

                    adapterIndex <- adapterIndex + 1u
                else
                    continueEnum <- false

            // Return only displays that have both WMI and API data
            correlatedDisplays
            |> List.filter (fun d -> d.APIDeviceName <> "" && d.EDIDIdentifier <> "")
            |> List.rev  // Reverse to maintain original order since we prepended

        with
        | ex ->
            Logging.logErrorf " API correlation failed: %s" ex.Message
            []

    // Apply hardware introduction order algorithm (sort by UID and assign Windows Display numbers)
    let private applyHardwareIntroductionOrderAlgorithm (displays: HardwareDisplayMapping list) =
        // Group by UID and assign the same Windows Display number to all mappings of the same UID
        let uniqueUIDs =
            displays
            |> List.map (fun d -> d.UID)
            |> List.distinct
            |> List.sort  // Sort by UID (hardware introduction order)

        // Create UID to Windows Display number mapping
        let uidToDisplayNumber =
            uniqueUIDs
            |> List.mapi (fun index uid -> (uid, index + 1))  // Windows Display numbers start at 1
            |> Map.ofList

        // Apply the Windows Display numbers to all displays
        displays
        |> List.map (fun display ->
            let windowsDisplayNumber = Map.find display.UID uidToDisplayNumber
            { display with WindowsDisplayNumber = windowsDisplayNumber }
        )

    // Main function to get Windows Display Settings numbering
    let getWindowsDisplayNumbering() : HardwareDisplayMapping list =
        try
            Logging.logVerbose "Building Windows Display Settings numbering using hardware introduction order algorithm..."

            // Step 1: Get WMI display data
            let wmiDisplays = getWMIDisplayData()
            Logging.logVerbosef " Found %d WMI displays" wmiDisplays.Length

            if wmiDisplays.IsEmpty then
                Logging.logErrorf " No WMI display data available"
                []
            else
                // Step 2: Correlate with Windows API enumeration
                let correlatedDisplays = correlateWithAPI wmiDisplays
                Logging.logVerbosef " Successfully correlated %d displays" correlatedDisplays.Length

                if correlatedDisplays.IsEmpty then
                    Logging.logErrorf " No displays successfully correlated"
                    []
                else
                    // Step 3: Apply hardware introduction order algorithm
                    let numberedDisplays = applyHardwareIntroductionOrderAlgorithm correlatedDisplays

                    Logging.logVerbose "Windows Display numbering results:"
                    for display in numberedDisplays do
                        Logging.logVerbosef "  Windows Display %d: %s (UID: %u, API: %s, EDID: %s)"
                            display.WindowsDisplayNumber
                            display.FriendlyName
                            display.UID
                            display.APIDeviceName
                            display.EDIDIdentifier

                    numberedDisplays

        with
        | ex ->
            Logging.logErrorf " Windows display numbering failed: %s" ex.Message
            []

    // Helper function to find API device name for a specific Windows Display number
    let getAPIDeviceNameForWindowsDisplay (windowsDisplayNumber: int) (mappings: HardwareDisplayMapping list) =
        mappings
        |> List.tryFind (fun m -> m.WindowsDisplayNumber = windowsDisplayNumber)
        |> Option.map (fun m -> m.APIDeviceName)

    // Helper function to get Windows Display number for an API device name
    let getWindowsDisplayNumberForAPIDevice (apiDeviceName: string) (mappings: HardwareDisplayMapping list) =
        mappings
        |> List.tryFind (fun m -> m.APIDeviceName = apiDeviceName)
        |> Option.map (fun m -> m.WindowsDisplayNumber)

    // Helper function to get friendly name for Windows Display number
    let getFriendlyNameForWindowsDisplay (windowsDisplayNumber: int) (mappings: HardwareDisplayMapping list) =
        mappings
        |> List.tryFind (fun m -> m.WindowsDisplayNumber = windowsDisplayNumber)
        |> Option.map (fun m -> m.FriendlyName)

    // Create display adapter that maps between Windows Display numbers and API device names
    type DisplayAdapter(mappings: HardwareDisplayMapping list) =
        member _.Mappings = mappings

        member _.GetAPIDeviceForWindowsDisplay(windowsDisplayNumber: int) =
            getAPIDeviceNameForWindowsDisplay windowsDisplayNumber mappings

        member _.GetWindowsDisplayForAPIDevice(apiDeviceName: string) =
            getWindowsDisplayNumberForAPIDevice apiDeviceName mappings

        member _.GetFriendlyNameForWindowsDisplay(windowsDisplayNumber: int) =
            getFriendlyNameForWindowsDisplay windowsDisplayNumber mappings

        member _.GetDisplayMapping(windowsDisplayNumber: int) =
            mappings |> List.tryFind (fun m -> m.WindowsDisplayNumber = windowsDisplayNumber)

        member _.ListAllMappings() =
            mappings
            |> List.map (fun m ->
                sprintf "Windows Display %d: %s (API: %s, UID: %u)"
                    m.WindowsDisplayNumber m.FriendlyName m.APIDeviceName m.UID)

    // Factory function to create display adapter
    let createDisplayAdapter() =
        let mappings = getWindowsDisplayNumbering()
        if mappings.IsEmpty then
            printfn "[WARNING] No display mappings available - adapter may not work correctly"
        DisplayAdapter(mappings)