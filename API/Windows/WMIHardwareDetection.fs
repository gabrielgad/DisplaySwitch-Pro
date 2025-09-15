namespace DisplaySwitchPro

open System
open System.Collections.Generic
open System.Management
open System.Text.RegularExpressions

/// WMI-based hardware detection algorithm for display devices
/// Provides hardware identification, serial numbers, and device correlation
module WMIHardwareDetection =

    /// Hardware display information from WMI
    type WMIDisplayInfo = {
        UID: uint32                        // Hardware UID from device path
        ManufacturerName: string          // Manufacturer (e.g., SAM)
        ProductName: string               // Product name (e.g., LS24AG30x)
        FriendlyName: string              // Full friendly name (e.g., SAM LS24AG30x)
        SerialNumber: string              // Hardware serial number
        WMIInstanceName: string           // WMI instance identifier
        TargetId: uint32 option           // CCD target ID if available
    }

    /// Extract UID from device path or instance name
    let extractUID (devicePath: string) =
        try
            let uidMatch = Regex.Match(devicePath, @"UID(\d+)")
            if uidMatch.Success then
                let uidStr = uidMatch.Groups.[1].Value
                match UInt32.TryParse(uidStr) with
                | true, uid -> Some uid
                | false, _ -> None
            else
                None
        with
        | ex ->
            printfn "[ERROR] Failed to extract UID from %s: %s" devicePath ex.Message
            None

    /// Extract target ID from WMI instance name for CCD correlation
    let extractTargetId (instanceName: string) =
        try
            // Example: "DISPLAY\SAM713F\5&12e08716&0&UID176390_0" → 176390
            let uidMatch = Regex.Match(instanceName, @"UID(\d+)")
            if uidMatch.Success then
                let targetIdStr = uidMatch.Groups.[1].Value
                match UInt32.TryParse(targetIdStr) with
                | true, targetId -> Some targetId
                | false, _ -> None
            else
                None
        with
        | ex ->
            printfn "[DEBUG] Error extracting target ID from %s: %s" instanceName ex.Message
            None

    /// Decode WMI string data (handles both uint16[] and byte[] formats)
    let private decodeWMIString (obj: obj) =
        if obj <> null then
            match obj with
            | :? (uint16[]) as uint16Array ->
                let nonZeroBytes = uint16Array |> Array.filter (fun b -> b <> 0us)
                if nonZeroBytes.Length > 0 then
                    String(nonZeroBytes |> Array.map char)
                else ""
            | :? (byte[]) as byteArray ->
                let nonZeroBytes = byteArray |> Array.filter (fun b -> b <> 0uy)
                if nonZeroBytes.Length > 0 then
                    String(nonZeroBytes |> Array.map char)
                else ""
            | _ -> ""
        else ""

    /// Get WMI display data with proper decoding of manufacturer, product, and serial information
    let getWMIDisplayData() =
        try
            let displays = List<WMIDisplayInfo>()

            use searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorID")
            use collection = searcher.Get()

            for obj in collection do
                use managementObj = obj :?> ManagementObject
                try
                    let instanceName = managementObj.["InstanceName"] :?> string

                    // Extract UID from WMI instance name
                    match extractUID instanceName with
                    | Some uid ->
                        // Decode manufacturer name
                        let manufacturerName =
                            try
                                managementObj.["ManufacturerName"] |> decodeWMIString
                            with _ -> "Unknown"

                        // Decode product name
                        let productName =
                            try
                                managementObj.["UserFriendlyName"] |> decodeWMIString
                            with _ -> "Unknown"

                        // Decode serial number
                        let serialNumber =
                            try
                                managementObj.["SerialNumberID"] |> decodeWMIString
                            with _ -> ""

                        // Create friendly display name
                        let friendlyName =
                            if manufacturerName <> "Unknown" && productName <> "Unknown" then
                                sprintf "%s %s" manufacturerName productName
                            elif productName <> "Unknown" then
                                productName
                            else
                                "Unknown Display"

                        // Extract target ID for CCD correlation
                        let targetId = extractTargetId instanceName

                        let displayInfo = {
                            UID = uid
                            ManufacturerName = manufacturerName
                            ProductName = productName
                            FriendlyName = friendlyName
                            SerialNumber = serialNumber
                            WMIInstanceName = instanceName
                            TargetId = targetId
                        }

                        displays.Add(displayInfo)
                        printfn "[DEBUG] WMI Display: UID %u -> %s (Target: %s)" uid friendlyName
                            (match targetId with Some id -> string id | None -> "None")
                    | None ->
                        printfn "[WARNING] No UID found in WMI instance: %s" instanceName

                with ex ->
                    printfn "[ERROR] Failed to process WMI monitor: %s" ex.Message

            displays |> Seq.toList
        with
        | ex ->
            printfn "[ERROR] WMI display enumeration failed: %s" ex.Message
            []

    /// Get WMI display information indexed by UID for correlation
    let getWMIDisplaysByUID() =
        let displays = getWMIDisplayData()
        displays
        |> List.map (fun d -> (d.UID, d))
        |> Map.ofList

    /// Get WMI display information indexed by target ID for CCD correlation
    let getWMIDisplaysByTargetId() =
        let displays = getWMIDisplayData()
        displays
        |> List.choose (fun d ->
            match d.TargetId with
            | Some targetId -> Some (targetId, d)
            | None -> None)
        |> Map.ofSeq

    /// Get monitor friendly names in enumeration order (legacy compatibility)
    let getMonitorFriendlyNames() =
        let wmiInfo = getWMIDisplayData()
        wmiInfo |> List.map (fun info -> info.FriendlyName)