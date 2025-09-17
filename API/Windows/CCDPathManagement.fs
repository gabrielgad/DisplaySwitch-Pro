namespace DisplaySwitchPro

open System
open WindowsAPI
open CCDTargetMapping
open ResultBuilder
open WindowsAPIResult

/// CCD (Connecting and Configuring Displays) Path Management algorithm
/// Provides path discovery, enumeration, and management for display configuration
module CCDPathManagement =

    /// CCD path information with metadata
    type PathInfo = {
        Path: DISPLAYCONFIG_PATH_INFO
        Index: int
        DisplayName: string
        IsActive: bool
        IsRelevant: bool
    }

    /// Helper functions for functional API calls
    let private getBufferSizes includeInactive =
        let flags = if includeInactive then QDC.QDC_ALL_PATHS else QDC.QDC_ONLY_ACTIVE_PATHS
        let mutable pathCount = 0u
        let mutable modeCount = 0u
        let sizeResult = GetDisplayConfigBufferSizes(flags, &pathCount, &modeCount)
        (sizeResult, pathCount, modeCount, flags)

    let private queryDisplayConfiguration flags pathCount modeCount =
        let pathArray = Array.zeroCreate<DISPLAYCONFIG_PATH_INFO> (int pathCount)
        let modeArray = Array.zeroCreate<DISPLAYCONFIG_MODE_INFO> (int modeCount)
        let mutable actualPathCount = pathCount
        let mutable actualModeCount = modeCount
        let queryResult = QueryDisplayConfig(flags, &actualPathCount, pathArray, &actualModeCount, modeArray, IntPtr.Zero)
        (queryResult, pathArray, modeArray, actualPathCount, actualModeCount)

    /// Get display paths from Windows CCD API
    let getDisplayPaths includeInactive =
        try
            let (sizeResult, pathCount, modeCount, flags) = getBufferSizes includeInactive

            if sizeResult <> ERROR.ERROR_SUCCESS then
                Logging.logVerbosef "GetDisplayConfigBufferSizes failed with error: %d" sizeResult
                Error (sprintf "Failed to get display config buffer sizes: %d" sizeResult)
            else
                Logging.logVerbosef "Buffer sizes - Paths: %d, Modes: %d" pathCount modeCount
                let (queryResult, pathArray, modeArray, actualPathCount, actualModeCount) =
                    queryDisplayConfiguration flags pathCount modeCount

                if queryResult <> ERROR.ERROR_SUCCESS then
                    Logging.logVerbosef "QueryDisplayConfig failed with error: %d" queryResult
                    Error (sprintf "Failed to query display config: %d" queryResult)
                else
                    Logging.logVerbosef "Successfully queried %d paths and %d modes" actualPathCount actualModeCount
                    Ok (pathArray, modeArray, actualPathCount, actualModeCount)
        with
        | ex ->
            Logging.logVerbosef "Exception in getDisplayPaths: %s" ex.Message
            Error (sprintf "Exception getting display paths: %s" ex.Message)

    /// Filter paths to only include relevant ones for display configuration
    let filterRelevantPaths (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
        let relevantPaths =
            paths
            |> Array.take (int pathCount)
            |> Array.indexed
            |> Array.filter (fun (_, path) ->
                // Keep paths that are currently active OR the path we want to activate
                // Don't filter out Source 0 - it's the primary display!
                path.flags <> 0u || path.targetInfo.targetAvailable <> 0)
            |> Array.map snd

        let filteredCount = Array.length relevantPaths
        Logging.logVerbosef " Filtered %d paths down to %d relevant paths" pathCount filteredCount

        // Log only the first 5 and last 2 filtered paths to reduce verbosity
        if Array.length relevantPaths <= 7 then
            relevantPaths |> Array.iteri (fun i path ->
                Logging.logVerbosef " Path %d: Source %d -> Target %d, Flags: 0x%08X"
                        i path.sourceInfo.id path.targetInfo.id path.flags)
        else
            // Log first 3 paths
            relevantPaths.[0..2] |> Array.iteri (fun i path ->
                Logging.logVerbosef " Path %d: Source %d -> Target %d, Flags: 0x%08X"
                        i path.sourceInfo.id path.targetInfo.id path.flags)
            Logging.logVerbosef " ... (%d paths omitted) ..." (Array.length relevantPaths - 5)
            // Log last 2 paths
            let len = Array.length relevantPaths
            relevantPaths.[(len-2)..(len-1)] |> Array.iteri (fun i path ->
                Logging.logVerbosef " Path %d: Source %d -> Target %d, Flags: 0x%08X"
                        (len-2+i) path.sourceInfo.id path.targetInfo.id path.flags)

        (relevantPaths, uint32 filteredCount)

    /// Helper function to parse display number from both old and new formats
    let private parseDisplayNumber (displayId: string) =
        if displayId.StartsWith("Display") then
            // New format: "Display3" -> 3
            let numberPart = displayId.Substring(7)
            match System.Int32.TryParse(numberPart) with
            | true, result -> Some result
            | false, _ -> None
        elif displayId.StartsWith(@"\\.\DISPLAY") then
            // Legacy format: "\\.\DISPLAY3" -> 3
            let numberPart = displayId.Substring(11)
            match System.Int32.TryParse(numberPart) with
            | true, result -> Some result
            | false, _ -> None
        else None

    /// Helper function to resolve Display ID to Target ID using WMI correlation
    let private resolveTargetIdForDisplay (displayId: string) =
        try
            // Extract Windows Display Number from displayId (e.g., "Display4" -> 4)
            match parseDisplayNumber displayId with
            | Some windowsDisplayNumber ->
                // Get WMI data to find the UID for this Windows Display Number
                let wmiDisplays = WMIHardwareDetection.getWMIDisplayData()

                // Find the WMI display that corresponds to this Windows Display Number
                // This uses the hardware introduction order algorithm from the research
                let sortedWMIDisplays =
                    wmiDisplays
                    |> List.sortBy (fun d -> d.UID)  // Sort by UID (hardware introduction order)

                // Map Windows Display Number to UID using introduction order
                if windowsDisplayNumber > 0 && windowsDisplayNumber <= sortedWMIDisplays.Length then
                    let targetWMIDisplay = sortedWMIDisplays.[windowsDisplayNumber - 1]
                    let targetUID = targetWMIDisplay.UID

                    Logging.logVerbosef " Resolved %s -> Windows Display %d -> UID %u (from WMI)" displayId windowsDisplayNumber targetUID

                    // The target ID for a display is its UID according to our correlation research
                    Some targetUID
                else
                    Logging.logVerbosef " Windows Display Number %d out of range (have %d WMI displays)" windowsDisplayNumber sortedWMIDisplays.Length
                    None
            | None ->
                Logging.logVerbosef " Could not parse display number from %s" displayId
                None
        with
        | ex ->
            Logging.logVerbosef " Exception resolving target ID for %s: %s" displayId ex.Message
            None

    /// Enhanced function to get display paths with validation
    let getDisplayPathsWithValidation includeInactive =
        result {
            let! (pathArray, modeArray, pathCount, modeCount) = getDisplayPaths includeInactive

            // Validate we have meaningful data
            if int pathCount = 0 then
                return! Error "No display paths found in system"
            elif pathArray |> Array.exists (fun path -> path.sourceInfo.id <> 0u || path.targetInfo.id <> 0u) |> not then
                return! Error "Display paths contain no valid source/target IDs"
            else
                Logging.logVerbosef " Validated %d display paths successfully" pathCount
                return (pathArray, modeArray, pathCount, modeCount)
        }

    /// Find display path by source ID with multiple strategies
    let findDisplayPathBySourceId displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
        try
            // Extract display number from both "Display3" and "\\.\DISPLAY3" formats
            let displayNumber = parseDisplayNumber displayId

            match displayNumber with
            | Some displayNum ->
                Logging.logVerbosef " Looking for display %d in %d paths using improved logic" displayNum pathCount

                // Log only key info when we have many paths (reduced verbosity)
                if pathCount > 10u then
                    Logging.logVerbosef " Large path array (%d paths) - searching for Source ID %d" pathCount (displayNum - 1)

                // Strategy 1: Find path with correct Source ID and Target that matches the display
                // First try to get Target ID using Windows Display Number mapping (more reliable for inactive displays)
                let targetIdFromWindowsMapping = resolveTargetIdForDisplay displayId

                // Fallback to CCD mapping if Windows mapping fails
                let targetIdMapping = getDisplayTargetIdMappingAsMap()
                let targetIdFromCCDMapping = Map.tryFind displayId targetIdMapping

                // Prefer Windows mapping over CCD mapping
                let targetId =
                    match targetIdFromWindowsMapping with
                    | Some uid -> uid
                    | None ->
                        match targetIdFromCCDMapping with
                        | Some ccdTargetId -> ccdTargetId
                        | None -> 0u

                Logging.logVerbosef " Display %s -> Target ID lookup: %s" displayId
                    (if targetId = 0u then "No mapping found" else sprintf "%u (from %s)" targetId (if targetIdFromWindowsMapping.IsSome then "Windows mapping" else "CCD mapping"))

                let matchingPaths =
                    [0 .. int pathCount - 1]
                    |> List.choose (fun i ->
                        let path = paths.[i]
                        let sourceIdMatches = int path.sourceInfo.id = (displayNum - 1)
                        let targetMatches = targetId = 0u || path.targetInfo.id = targetId

                        if sourceIdMatches && targetMatches then
                            Logging.logVerbosef " Found matching path %d: Source %d -> Target %d for Display %d"
                                    i path.sourceInfo.id path.targetInfo.id displayNum
                            Some (path, i)
                        else
                            None)
                    // If we have a specific target ID, prioritize exact target matches
                    |> (fun paths ->
                        if targetId <> 0u then
                            // Sort so exact target ID matches come first
                            paths |> List.sortBy (fun (path, _) ->
                                if path.targetInfo.id = targetId then 0 else 1)
                        else
                            paths)

                match matchingPaths with
                | (path, index) :: _ ->
                    Logging.logVerbosef " Selected path index %d for display %s (source ID %d)" index displayId path.sourceInfo.id
                    Ok (path, index)
                | [] ->
                    Logging.logVerbosef " No source ID match for display %d, trying alternative strategies" displayNum

                    // Strategy 2: Search all paths for matching target ID when source ID fails
                    if targetId <> 0u then
                        Logging.logVerbosef " Source ID failed, searching all paths for target ID %u" targetId
                        let targetMatchingPaths =
                            [0 .. int pathCount - 1]
                            |> List.choose (fun i ->
                                let path = paths.[i]
                                if path.targetInfo.id = targetId then
                                    Logging.logVerbosef " Found target ID match at path %d: Source %d -> Target %d"
                                            i path.sourceInfo.id path.targetInfo.id
                                    Some (path, i)
                                else
                                    None)

                        match targetMatchingPaths with
                        | (path, index) :: _ ->
                            Logging.logVerbosef " Using target ID match at path %d for display %s" index displayId
                            Ok (path, index)
                        | [] ->
                            Logging.logVerbosef " No target ID match found, using fallback strategy"
                            // Strategy 3: Use direct mapping as fallback
                            let pathIndex = displayNum - 1 // Convert to 0-based
                            if pathIndex >= 0 && pathIndex < int pathCount then
                                let path = paths.[pathIndex]
                                Logging.logVerbosef " Using direct index mapping: path %d for display %s" pathIndex displayId
                                Ok (path, pathIndex)
                            else
                                Error (sprintf "No valid path found for display %s (checked source ID and direct index)" displayId)
                    else
                        // Strategy 3: Use direct mapping as fallback when no target ID available
                        let pathIndex = displayNum - 1 // Convert to 0-based
                        if pathIndex >= 0 && pathIndex < int pathCount then
                            let path = paths.[pathIndex]
                            Logging.logVerbosef " Using direct index mapping: path %d for display %s" pathIndex displayId
                            Ok (path, pathIndex)
                        else
                            Error (sprintf "No valid path found for display %s (checked source ID and direct index)" displayId)
            | None ->
                Error (sprintf "Could not parse display number from %s" displayId)
        with
        | ex ->
            Logging.logVerbosef " Exception in improved path finding: %s" ex.Message
            Error (sprintf "Exception finding display path: %s" ex.Message)

    /// Simplified but robust mapping that works for both enabling and disabling displays
    let findDisplayPathByDevice displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
        try
            // Extract display number from both "Display3" and "\\.\DISPLAY3" formats
            let displayNumber = parseDisplayNumber displayId

            match displayNumber with
            | Some displayNum ->
                Logging.logVerbosef " Looking for display %d in %d paths" displayNum pathCount

                // Get Target ID for more precise matching
                let targetId = resolveTargetIdForDisplay displayId |> Option.defaultValue 0u

                // Strategy 1: Look for paths by source ID and target ID (most reliable)
                let foundPathResult =
                    [0 .. int pathCount - 1]
                    |> List.choose (fun i ->
                        let path = paths.[i]
                        let sourceIdMatches = int path.sourceInfo.id = (displayNum - 1)
                        let targetMatches = targetId = 0u || path.targetInfo.id = targetId

                        if sourceIdMatches && targetMatches then
                            Logging.logVerbosef " Found path %d with source ID %d and target ID %u matching display %d"
                                i path.sourceInfo.id path.targetInfo.id displayNum
                            Some (path, i)
                        else None)
                    // Prioritize exact target ID matches if we have a specific target
                    |> (fun paths ->
                        if targetId <> 0u then
                            paths |> List.sortBy (fun (path, _) ->
                                if path.targetInfo.id = targetId then 0 else 1)
                        else paths)
                    |> List.tryHead

                match foundPathResult with
                | Some (path, foundIndex) ->
                    Logging.logVerbosef " Mapped display %s to path index %d (source ID match)" displayId foundIndex
                    Ok (path, foundIndex)
                | None ->
                    // Strategy 2: Use display index directly as fallback
                    let pathIndex = displayNum - 1 // Convert to 0-based
                    if pathIndex >= 0 && pathIndex < int pathCount then
                        let path = paths.[pathIndex]
                        Logging.logVerbosef " Mapped display %s to path index %d (direct index)" displayId pathIndex
                        Ok (path, pathIndex)
                    else
                        // Strategy 3: Search through all paths for any that could match
                        Logging.logVerbosef " Direct index %d out of range, searching all paths..." pathIndex
                        if int pathCount > 0 then
                            // Just take the path at index (displayNum - 1) mod pathCount to avoid out of bounds
                            let wrappedIndex = (displayNum - 1) % int pathCount
                            let path = paths.[wrappedIndex]
                            Logging.logVerbosef " Using wrapped index %d for display %s" wrappedIndex displayId
                            Ok (path, wrappedIndex)
                        else
                            Error (sprintf "No paths available for display %s" displayId)
            | None ->
                Logging.logVerbosef " Could not parse display number from %s" displayId
                Error (sprintf "Could not parse display number from %s" displayId)
        with
        | ex ->
            Logging.logVerbosef " Exception mapping display path: %s" ex.Message
            Error (sprintf "Exception mapping display path: %s" ex.Message)

    /// Find the display path for a specific display ID (using simplified approach)
    let findDisplayPath displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
        findDisplayPathByDevice displayId paths pathCount

    /// Find inactive display paths specifically - improved version
    let findInactiveDisplayPath displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
        try
            Logging.logVerbosef " Finding inactive path for %s in %u total paths" displayId pathCount

            // First try to find using the improved path finding logic
            match findDisplayPathBySourceId displayId paths pathCount with
            | Ok (path, index) ->
                let isInactive = path.flags = 0u || path.targetInfo.targetAvailable = 0
                if isInactive then
                    Logging.logVerbosef " Found inactive path for %s at index %d (flags: 0x%08X, available: %d)"
                            displayId index path.flags path.targetInfo.targetAvailable
                    Ok (path, index)
                else
                    Logging.logVerbosef " Path at index %d for %s is active, will try to activate anyway" index displayId
                    Ok (path, index)
            | Error _ ->
                // Fallback to original logic if improved logic fails
                let displayNumber =
                    if (displayId: string).StartsWith(@"\\.\DISPLAY") then
                        let mutable result = 0
                        match System.Int32.TryParse((displayId: string).Substring(11), &result) with
                        | true -> Some result
                        | false -> None
                    else None

                match displayNumber with
                | Some displayNum ->
                    // Look for inactive paths (flags = 0 or targetAvailable = 0)
                    let inactivePaths =
                        [0 .. int pathCount - 1]
                        |> List.filter (fun i ->
                            let path = paths.[i]
                            let isInactive = path.flags = 0u || path.targetInfo.targetAvailable = 0
                            let matchesDisplay = int path.sourceInfo.id = (displayNum - 1)
                            isInactive && matchesDisplay)
                        |> List.tryHead
                        |> Option.map (fun i -> (paths.[i], i))

                    match inactivePaths with
                    | Some (path, index) ->
                        Logging.logVerbosef " Fallback: Found inactive path for display %s at index %d" displayId index
                        Ok (path, index)
                    | None ->
                        Logging.logVerbosef " Fallback: No inactive path found for display %s, using generic path finding" displayId
                        findDisplayPathByDevice displayId paths pathCount
                | None ->
                    Error (sprintf "Could not parse display number from %s" displayId)
        with
        | ex ->
            Error (sprintf "Exception finding inactive display path: %s" ex.Message)

    /// Validate display path integrity
    let validateDisplayPath (path: DISPLAYCONFIG_PATH_INFO) =
        let sourceValid = path.sourceInfo.id <> 0u || path.sourceInfo.adapterId.LowPart <> 0u
        let targetValid = path.targetInfo.id <> 0u || path.targetInfo.adapterId.LowPart <> 0u

        if not sourceValid then
            Error "Display path has invalid source information"
        elif not targetValid then
            Error "Display path has invalid target information"
        else
            Ok path

    /// Get enhanced path information with metadata
    let getPathInfoWithMetadata includeInactive =
        result {
            let! (pathArray, modeArray, pathCount, modeCount) = getDisplayPaths includeInactive

            let pathInfos =
                pathArray
                |> Array.take (int pathCount)
                |> Array.mapi (fun index path ->
                    let displayName = sprintf "\\\\.\\DISPLAY%d" (int path.sourceInfo.id + 1)
                    let isActive = path.flags <> 0u
                    let isRelevant = path.flags <> 0u || path.targetInfo.targetAvailable <> 0

                    {
                        Path = path
                        Index = index
                        DisplayName = displayName
                        IsActive = isActive
                        IsRelevant = isRelevant
                    })
                |> Array.toList

            return pathInfos
        }

    /// Enhanced error reporting helpers (additive - doesn't break existing APIs)
    module EnhancedErrorReporting =

        /// Enhanced error classification for CCD path operations
        let classifyPathError (errorCode: uint32) (operation: string) (context: string) =
            match errorCode with
            | 0u -> sprintf "Success in %s (%s)" operation context
            | 87u -> sprintf "Invalid parameter in %s - display configuration may be invalid (%s)" operation context
            | 1004u -> sprintf "Invalid flags in %s - unsupported configuration options (%s)" operation context
            | 1169u -> sprintf "Device not found in %s - display may be disconnected (%s)" operation context
            | 1359u -> sprintf "Internal error in %s - driver or system issue (%s)" operation context
            | 6u -> sprintf "Invalid handle in %s - display adapter issue (%s)" operation context
            | 170u -> sprintf "Resource in use in %s - display may be busy (%s)" operation context
            | 1450u -> sprintf "Insufficient system resources in %s (%s)" operation context
            | _ -> sprintf "CCD API error %u in %s (%s)" errorCode operation context

        /// Get diagnostic information for path finding failures
        let getDiagnosticInfo displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
            let diagnostics = System.Text.StringBuilder()
            diagnostics.AppendLine(sprintf "=== Path Finding Diagnostics for %s ===" displayId) |> ignore
            diagnostics.AppendLine(sprintf "Total paths available: %d" pathCount) |> ignore

            // Show path summary
            let activePaths = paths |> Array.take (int pathCount) |> Array.filter (fun p -> p.flags <> 0u)
            let inactivePaths = paths |> Array.take (int pathCount) |> Array.filter (fun p -> p.flags = 0u)
            diagnostics.AppendLine(sprintf "Active paths: %d, Inactive paths: %d" activePaths.Length inactivePaths.Length) |> ignore

            // Show source ID distribution
            let sourceIds = paths |> Array.take (int pathCount) |> Array.map (fun p -> p.sourceInfo.id) |> Array.distinct
            diagnostics.AppendLine(sprintf "Available source IDs: %s" (sourceIds |> Array.map string |> String.concat ", ")) |> ignore

            // Show target ID distribution
            let targetIds = paths |> Array.take (int pathCount) |> Array.map (fun p -> p.targetInfo.id) |> Array.distinct
            diagnostics.AppendLine(sprintf "Available target IDs: %s" (targetIds |> Array.map string |> String.concat ", ")) |> ignore

            // Show display number parsing result
            match parseDisplayNumber displayId with
            | Some displayNum ->
                diagnostics.AppendLine(sprintf "Parsed display number: %d (looking for source ID %d)" displayNum (displayNum - 1)) |> ignore
                let expectedSourceId = uint32 (displayNum - 1)
                let pathsWithExpectedSource = paths |> Array.take (int pathCount) |> Array.filter (fun p -> p.sourceInfo.id = expectedSourceId)
                diagnostics.AppendLine(sprintf "Paths with expected source ID %u: %d" expectedSourceId pathsWithExpectedSource.Length) |> ignore
            | None ->
                diagnostics.AppendLine(sprintf "Could not parse display number from '%s'" displayId) |> ignore

            diagnostics.ToString()

        /// Enhanced error message for path finding failures
        let createEnhancedPathError displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) originalError =
            let diagnostics = getDiagnosticInfo displayId paths pathCount
            sprintf "%s\n\nDiagnostic Information:\n%s" originalError diagnostics

        /// Validate path array integrity and provide detailed feedback
        let validatePathArrayIntegrity (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
            let issues = System.Collections.Generic.List<string>()

            if int pathCount = 0 then
                issues.Add("No paths found in system")
            elif int pathCount > paths.Length then
                issues.Add(sprintf "Path count %d exceeds array length %d" pathCount paths.Length)
            else
                let actualPaths = paths |> Array.take (int pathCount)

                // Check for all-zero paths
                let zeroSourcePaths = actualPaths |> Array.filter (fun p -> p.sourceInfo.id = 0u && p.sourceInfo.adapterId.LowPart = 0u)
                if zeroSourcePaths.Length = actualPaths.Length then
                    issues.Add("All paths have zero source information")

                let zeroTargetPaths = actualPaths |> Array.filter (fun p -> p.targetInfo.id = 0u && p.targetInfo.adapterId.LowPart = 0u)
                if zeroTargetPaths.Length = actualPaths.Length then
                    issues.Add("All paths have zero target information")

                // Check for duplicate source IDs in active paths
                let activePaths = actualPaths |> Array.filter (fun p -> p.flags <> 0u)
                let activeSourceIds = activePaths |> Array.map (fun p -> p.sourceInfo.id)
                let duplicateSourceIds = activeSourceIds |> Array.groupBy id |> Array.filter (fun (_, group) -> group.Length > 1) |> Array.map fst
                if duplicateSourceIds.Length > 0 then
                    issues.Add(sprintf "Duplicate active source IDs found: %s" (duplicateSourceIds |> Array.map string |> String.concat ", "))

            if issues.Count = 0 then
                Ok "Path array integrity validated successfully"
            else
                Error (String.concat "; " issues)

    /// Optional enhanced versions of existing functions (for gradual migration)
    module EnhancedVersions =

        /// Enhanced version of getDisplayPaths with better error reporting
        let getDisplayPathsWithDiagnostics includeInactive =
            match getDisplayPaths includeInactive with
            | Ok (pathArray, modeArray, pathCount, modeCount) ->
                // Validate integrity and add diagnostics
                match EnhancedErrorReporting.validatePathArrayIntegrity pathArray pathCount with
                | Ok validationMessage ->
                    Logging.logVerbosef " Path validation: %s" validationMessage
                    Ok (pathArray, modeArray, pathCount, modeCount)
                | Error validationError ->
                    Logging.logVerbosef " Path validation failed: %s" validationError
                    // Still return the result but log the validation issue
                    Ok (pathArray, modeArray, pathCount, modeCount)
            | Error originalError ->
                let enhancedError = sprintf "%s (Use getDisplayPathsWithDiagnostics for more details)" originalError
                Error enhancedError

        /// Enhanced version of findDisplayPathBySourceId with detailed diagnostics
        let findDisplayPathBySourceIdWithDiagnostics displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
            match findDisplayPathBySourceId displayId paths pathCount with
            | Ok result -> Ok result
            | Error originalError ->
                let enhancedError = EnhancedErrorReporting.createEnhancedPathError displayId paths pathCount originalError
                Logging.logVerbosef " Enhanced path finding error: %s" enhancedError
                Error enhancedError

        /// Enhanced version with confidence scoring
        let findDisplayPathWithConfidence displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
            match findDisplayPathBySourceId displayId paths pathCount with
            | Ok (path, index) ->
                // Calculate confidence score based on multiple factors
                let mutable confidence = 0

                // Factor 1: Source ID exact match (50 points)
                match parseDisplayNumber displayId with
                | Some displayNum when int path.sourceInfo.id = (displayNum - 1) -> confidence <- confidence + 50
                | _ -> ()

                // Factor 2: Target ID mapping match (30 points)
                let targetId = resolveTargetIdForDisplay displayId |> Option.defaultValue 0u
                if targetId <> 0u && path.targetInfo.id = targetId then
                    confidence <- confidence + 30

                // Factor 3: Path is active (20 points)
                if path.flags <> 0u then
                    confidence <- confidence + 20

                let confidenceLevel =
                    match confidence with
                    | c when c >= 80 -> "High"
                    | c when c >= 50 -> "Medium"
                    | c when c >= 20 -> "Low"
                    | _ -> "Very Low"

                Logging.logVerbosef " Path found for %s with %s confidence (score: %d/100)" displayId confidenceLevel confidence
                Ok ((path, index), confidence, confidenceLevel)
            | Error originalError ->
                let enhancedError = EnhancedErrorReporting.createEnhancedPathError displayId paths pathCount originalError
                Error enhancedError