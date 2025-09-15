namespace DisplaySwitchPro

open System
open WindowsAPI
open WMIHardwareDetection

/// CCD (Connecting and Configuring Displays) Target ID mapping algorithm
/// Provides correlation between Windows display device names and CCD target IDs
module CCDTargetMapping =

    /// Display to target ID mapping
    type DisplayTargetMapping = {
        DisplayName: string           // Windows display device name (e.g., \\.\DISPLAY1)
        TargetId: uint32             // CCD target ID
        SourceId: uint32             // CCD source ID
        FriendlyName: string option  // Hardware friendly name if available
        IsActive: bool               // Whether the path is currently active
    }

    /// Get display target ID mapping using CCD API with WMI correlation
    let getDisplayTargetIdMapping() =
        try
            printfn "[DEBUG] Building display-to-target ID mapping using CCD API..."

            // Use raw Windows API calls to get display paths
            let mutable pathCount = 0u
            let mutable modeCount = 0u

            // Get buffer sizes - use ALL_PATHS to get complete enumeration order
            let sizeResult = GetDisplayConfigBufferSizes(QDC.QDC_ALL_PATHS, &pathCount, &modeCount)
            if sizeResult = 0 then
                let pathArray = Array.zeroCreate<DISPLAYCONFIG_PATH_INFO> (int pathCount)
                let modeArray = Array.zeroCreate<DISPLAYCONFIG_MODE_INFO> (int modeCount)

                // Query display configuration - use ALL_PATHS to get complete enumeration order
                let queryResult = QueryDisplayConfig(QDC.QDC_ALL_PATHS, &pathCount, pathArray, &modeCount, modeArray, IntPtr.Zero)
                if queryResult = 0 then
                    printfn "[DEBUG] Found %d CCD paths for target mapping" pathCount

                    // Get WMI monitor information with target IDs
                    let wmiByTargetId = getWMIDisplaysByTargetId()

                    // Build mapping by matching CCD source IDs to Windows display device names
                    let mapping =
                        pathArray
                        |> Array.take (int pathCount)
                        |> Array.choose (fun path ->
                            // Convert source ID to Windows display device name (sourceId 0 = DISPLAY1, etc.)
                            let displayName = sprintf "\\\\.\\DISPLAY%d" (int path.sourceInfo.id + 1)
                            let targetId = path.targetInfo.id
                            let sourceId = path.sourceInfo.id
                            let isActive = path.flags <> 0u

                            // Get friendly name from WMI if available
                            let friendlyName =
                                match Map.tryFind targetId wmiByTargetId with
                                | Some wmiInfo -> Some wmiInfo.FriendlyName
                                | None -> None

                            let mapping = {
                                DisplayName = displayName
                                TargetId = targetId
                                SourceId = sourceId
                                FriendlyName = friendlyName
                                IsActive = isActive
                            }

                            match friendlyName with
                            | Some name ->
                                printfn "[DEBUG] CCD Mapping: %s (Source %d) -> Target %u (%s) [%s]"
                                    displayName sourceId targetId name (if isActive then "Active" else "Inactive")
                            | None ->
                                printfn "[DEBUG] CCD Mapping: %s (Source %d) -> Target %u (No WMI data) [%s]"
                                    displayName sourceId targetId (if isActive then "Active" else "Inactive")

                            Some mapping)
                        |> Array.toList

                    printfn "[DEBUG] Created CCD target mapping for %d displays" mapping.Length
                    mapping
                else
                    printfn "[ERROR] QueryDisplayConfig failed with code: %d" queryResult
                    []
            else
                printfn "[ERROR] GetDisplayConfigBufferSizes failed with code: %d" sizeResult
                []

        with
        | ex ->
            printfn "[ERROR] Failed to build display-target ID mapping: %s" ex.Message
            []

    /// Get target ID for a specific display device name
    let getTargetIdForDisplay (displayName: string) =
        let mappings = getDisplayTargetIdMapping()
        mappings
        |> List.tryFind (fun m -> m.DisplayName = displayName)
        |> Option.map (fun m -> m.TargetId)

    /// Get display name for a specific target ID
    let getDisplayForTargetId (targetId: uint32) =
        let mappings = getDisplayTargetIdMapping()
        mappings
        |> List.tryFind (fun m -> m.TargetId = targetId)
        |> Option.map (fun m -> m.DisplayName)

    /// Get display target ID mapping as simple Map (legacy compatibility)
    let getDisplayTargetIdMappingAsMap() =
        let mappings = getDisplayTargetIdMapping()
        mappings
        |> List.map (fun m -> (m.DisplayName, m.TargetId))
        |> Map.ofList

    /// Get only active display target mappings
    let getActiveDisplayTargetMappings() =
        let mappings = getDisplayTargetIdMapping()
        mappings
        |> List.filter (fun m -> m.IsActive)

    /// Get only inactive display target mappings
    let getInactiveDisplayTargetMappings() =
        let mappings = getDisplayTargetIdMapping()
        mappings
        |> List.filter (fun m -> not m.IsActive)

    /// Find best target mapping for a display using multiple strategies
    let findBestTargetMappingForDisplay (displayName: string) =
        let mappings = getDisplayTargetIdMapping()

        // Strategy 1: Direct name match
        match mappings |> List.tryFind (fun m -> m.DisplayName = displayName) with
        | Some mapping -> Some mapping
        | None ->
            // Strategy 2: Extract display number and try alternative matching
            let displayNumberResult =
                if displayName.StartsWith(@"\\.\DISPLAY") then
                    let numberPart = displayName.Substring(11)
                    match System.Int32.TryParse(numberPart) with
                    | true, num -> Some num
                    | false, _ -> None
                else None

            match displayNumberResult with
            | Some displayNum ->
                // Try to find mapping by source ID (display number - 1 = source ID)
                mappings
                |> List.tryFind (fun m -> int m.SourceId = (displayNum - 1))
            | None -> None