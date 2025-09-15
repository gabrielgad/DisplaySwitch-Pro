namespace DisplaySwitchPro

open System
open WindowsAPI
open CCDTargetMapping
open ResultBuilder

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
                printfn "[DEBUG] GetDisplayConfigBufferSizes failed with error: %d" sizeResult
                Error (sprintf "Failed to get display config buffer sizes: %d" sizeResult)
            else
                printfn "[DEBUG] Buffer sizes - Paths: %d, Modes: %d" pathCount modeCount
                let (queryResult, pathArray, modeArray, actualPathCount, actualModeCount) =
                    queryDisplayConfiguration flags pathCount modeCount

                if queryResult <> ERROR.ERROR_SUCCESS then
                    printfn "[DEBUG] QueryDisplayConfig failed with error: %d" queryResult
                    Error (sprintf "Failed to query display config: %d" queryResult)
                else
                    printfn "[DEBUG] Successfully queried %d paths and %d modes" actualPathCount actualModeCount
                    Ok (pathArray, modeArray, actualPathCount, actualModeCount)
        with
        | ex ->
            printfn "[DEBUG] Exception in getDisplayPaths: %s" ex.Message
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
        printfn "[DEBUG] Filtered %d paths down to %d relevant paths" pathCount filteredCount

        // Log only the first 5 and last 2 filtered paths to reduce verbosity
        if Array.length relevantPaths <= 7 then
            relevantPaths |> Array.iteri (fun i path ->
                printfn "[DEBUG] Path %d: Source %d -> Target %d, Flags: 0x%08X"
                        i path.sourceInfo.id path.targetInfo.id path.flags)
        else
            // Log first 3 paths
            relevantPaths.[0..2] |> Array.iteri (fun i path ->
                printfn "[DEBUG] Path %d: Source %d -> Target %d, Flags: 0x%08X"
                        i path.sourceInfo.id path.targetInfo.id path.flags)
            printfn "[DEBUG] ... (%d paths omitted) ..." (Array.length relevantPaths - 5)
            // Log last 2 paths
            let len = Array.length relevantPaths
            relevantPaths.[(len-2)..(len-1)] |> Array.iteri (fun i path ->
                printfn "[DEBUG] Path %d: Source %d -> Target %d, Flags: 0x%08X"
                        (len-2+i) path.sourceInfo.id path.targetInfo.id path.flags)

        (relevantPaths, uint32 filteredCount)

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
                printfn "[DEBUG] Validated %d display paths successfully" pathCount
                return (pathArray, modeArray, pathCount, modeCount)
        }

    /// Find display path by source ID with multiple strategies
    let findDisplayPathBySourceId displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
        try
            // Extract display number from "\\.\DISPLAY4" -> 4
            let displayNumber =
                if (displayId: string).StartsWith(@"\\.\DISPLAY") then
                    let mutable result = 0
                    match System.Int32.TryParse((displayId: string).Substring(11), &result) with
                    | true -> Some result // Keep 1-based
                    | false -> None
                else None

            match displayNumber with
            | Some displayNum ->
                printfn "[DEBUG] Looking for display %d in %d paths using improved logic" displayNum pathCount

                // Log only key info when we have many paths (reduced verbosity)
                if pathCount > 10u then
                    printfn "[DEBUG] Large path array (%d paths) - searching for Source ID %d" pathCount (displayNum - 1)

                // Strategy 1: Find path with correct Source ID and Target that matches the display
                let targetIdMapping = getDisplayTargetIdMappingAsMap()
                let targetId = Map.tryFind displayId targetIdMapping |> Option.defaultValue 0u

                printfn "[DEBUG] Display %s -> Target ID lookup: %s" displayId
                    (if targetId = 0u then "No mapping found" else sprintf "%u" targetId)

                let matchingPaths =
                    [0 .. int pathCount - 1]
                    |> List.choose (fun i ->
                        let path = paths.[i]
                        let sourceIdMatches = int path.sourceInfo.id = (displayNum - 1)
                        let targetMatches = targetId = 0u || path.targetInfo.id = targetId

                        if sourceIdMatches && targetMatches then
                            printfn "[DEBUG] Found matching path %d: Source %d -> Target %d for Display %d"
                                    i path.sourceInfo.id path.targetInfo.id displayNum
                            Some (path, i)
                        else
                            None)

                match matchingPaths with
                | (path, index) :: _ ->
                    printfn "[DEBUG] Selected path index %d for display %s (source ID %d)" index displayId path.sourceInfo.id
                    Ok (path, index)
                | [] ->
                    printfn "[DEBUG] No source ID match for display %d, trying alternative strategies" displayNum

                    // Strategy 2: Search all paths for matching target ID when source ID fails
                    if targetId <> 0u then
                        printfn "[DEBUG] Source ID failed, searching all paths for target ID %u" targetId
                        let targetMatchingPaths =
                            [0 .. int pathCount - 1]
                            |> List.choose (fun i ->
                                let path = paths.[i]
                                if path.targetInfo.id = targetId then
                                    printfn "[DEBUG] Found target ID match at path %d: Source %d -> Target %d"
                                            i path.sourceInfo.id path.targetInfo.id
                                    Some (path, i)
                                else
                                    None)

                        match targetMatchingPaths with
                        | (path, index) :: _ ->
                            printfn "[DEBUG] Using target ID match at path %d for display %s" index displayId
                            Ok (path, index)
                        | [] ->
                            printfn "[DEBUG] No target ID match found, using fallback strategy"
                            // Strategy 3: Use direct mapping as fallback
                            let pathIndex = displayNum - 1 // Convert to 0-based
                            if pathIndex >= 0 && pathIndex < int pathCount then
                                let path = paths.[pathIndex]
                                printfn "[DEBUG] Using direct index mapping: path %d for display %s" pathIndex displayId
                                Ok (path, pathIndex)
                            else
                                Error (sprintf "No valid path found for display %s (checked source ID and direct index)" displayId)
                    else
                        // Strategy 3: Use direct mapping as fallback when no target ID available
                        let pathIndex = displayNum - 1 // Convert to 0-based
                        if pathIndex >= 0 && pathIndex < int pathCount then
                            let path = paths.[pathIndex]
                            printfn "[DEBUG] Using direct index mapping: path %d for display %s" pathIndex displayId
                            Ok (path, pathIndex)
                        else
                            Error (sprintf "No valid path found for display %s (checked source ID and direct index)" displayId)
            | None ->
                Error (sprintf "Could not parse display number from %s" displayId)
        with
        | ex ->
            printfn "[DEBUG] Exception in improved path finding: %s" ex.Message
            Error (sprintf "Exception finding display path: %s" ex.Message)

    /// Simplified but robust mapping that works for both enabling and disabling displays
    let findDisplayPathByDevice displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
        try
            // Extract display number from "\\.\DISPLAY3" -> 3
            let displayNumber =
                if (displayId: string).StartsWith(@"\\.\DISPLAY") then
                    let mutable result = 0
                    match System.Int32.TryParse((displayId: string).Substring(11), &result) with
                    | true -> Some result // Keep 1-based for easier debugging
                    | false -> None
                else None

            match displayNumber with
            | Some displayNum ->
                printfn "[DEBUG] Looking for display %d in %d paths" displayNum pathCount

                // Strategy 1: Look for paths by source ID (most reliable)
                let foundPathResult =
                    [0 .. int pathCount - 1]
                    |> List.tryFind (fun i ->
                        let path = paths.[i]
                        if int path.sourceInfo.id = (displayNum - 1) then
                            printfn "[DEBUG] Found path with source ID %d matching display %d" path.sourceInfo.id displayNum
                            true
                        else false)
                    |> Option.map (fun i -> (paths.[i], i))

                match foundPathResult with
                | Some (path, foundIndex) ->
                    printfn "[DEBUG] Mapped display %s to path index %d (source ID match)" displayId foundIndex
                    Ok (path, foundIndex)
                | None ->
                    // Strategy 2: Use display index directly as fallback
                    let pathIndex = displayNum - 1 // Convert to 0-based
                    if pathIndex >= 0 && pathIndex < int pathCount then
                        let path = paths.[pathIndex]
                        printfn "[DEBUG] Mapped display %s to path index %d (direct index)" displayId pathIndex
                        Ok (path, pathIndex)
                    else
                        // Strategy 3: Search through all paths for any that could match
                        printfn "[DEBUG] Direct index %d out of range, searching all paths..." pathIndex
                        if int pathCount > 0 then
                            // Just take the path at index (displayNum - 1) mod pathCount to avoid out of bounds
                            let wrappedIndex = (displayNum - 1) % int pathCount
                            let path = paths.[wrappedIndex]
                            printfn "[DEBUG] Using wrapped index %d for display %s" wrappedIndex displayId
                            Ok (path, wrappedIndex)
                        else
                            Error (sprintf "No paths available for display %s" displayId)
            | None ->
                printfn "[DEBUG] Could not parse display number from %s" displayId
                Error (sprintf "Could not parse display number from %s" displayId)
        with
        | ex ->
            printfn "[DEBUG] Exception mapping display path: %s" ex.Message
            Error (sprintf "Exception mapping display path: %s" ex.Message)

    /// Find the display path for a specific display ID (using simplified approach)
    let findDisplayPath displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
        findDisplayPathByDevice displayId paths pathCount

    /// Find inactive display paths specifically - improved version
    let findInactiveDisplayPath displayId (paths: DISPLAYCONFIG_PATH_INFO[]) (pathCount: uint32) =
        try
            printfn "[DEBUG] Finding inactive path for %s in %u total paths" displayId pathCount

            // First try to find using the improved path finding logic
            match findDisplayPathBySourceId displayId paths pathCount with
            | Ok (path, index) ->
                let isInactive = path.flags = 0u || path.targetInfo.targetAvailable = 0
                if isInactive then
                    printfn "[DEBUG] Found inactive path for %s at index %d (flags: 0x%08X, available: %d)"
                            displayId index path.flags path.targetInfo.targetAvailable
                    Ok (path, index)
                else
                    printfn "[DEBUG] Path at index %d for %s is active, will try to activate anyway" index displayId
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
                        printfn "[DEBUG] Fallback: Found inactive path for display %s at index %d" displayId index
                        Ok (path, index)
                    | None ->
                        printfn "[DEBUG] Fallback: No inactive path found for display %s, using generic path finding" displayId
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