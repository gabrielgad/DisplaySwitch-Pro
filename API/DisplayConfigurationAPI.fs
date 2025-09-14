namespace DisplaySwitchPro

open System
open WindowsAPI
open ResultBuilder

// CCD (Connecting and Configuring Displays) API helper functions
module DisplayConfigurationAPI =
    
    // Helper functions for functional API calls
    let private getBufferSizes includeInactive =
        let flags = if includeInactive then WindowsAPI.QDC.QDC_ALL_PATHS else WindowsAPI.QDC.QDC_ONLY_ACTIVE_PATHS
        let mutable pathCount = 0u
        let mutable modeCount = 0u
        let sizeResult = WindowsAPI.GetDisplayConfigBufferSizes(flags, &pathCount, &modeCount)
        (sizeResult, pathCount, modeCount, flags)
    
    let private queryDisplayConfiguration flags pathCount modeCount =
        let pathArray = Array.zeroCreate<WindowsAPI.DISPLAYCONFIG_PATH_INFO> (int pathCount)
        let modeArray = Array.zeroCreate<WindowsAPI.DISPLAYCONFIG_MODE_INFO> (int modeCount)
        let mutable actualPathCount = pathCount
        let mutable actualModeCount = modeCount
        let queryResult = WindowsAPI.QueryDisplayConfig(flags, &actualPathCount, pathArray, &actualModeCount, modeArray, IntPtr.Zero)
        (queryResult, pathArray, modeArray, actualPathCount, actualModeCount)

    // Get display paths from Windows CCD API
    let getDisplayPaths includeInactive =
        try
            let (sizeResult, pathCount, modeCount, flags) = getBufferSizes includeInactive
            
            if sizeResult <> WindowsAPI.ERROR.ERROR_SUCCESS then
                printfn "[DEBUG] GetDisplayConfigBufferSizes failed with error: %d" sizeResult
                Error (sprintf "Failed to get display config buffer sizes: %d" sizeResult)
            else
                printfn "[DEBUG] Buffer sizes - Paths: %d, Modes: %d" pathCount modeCount
                let (queryResult, pathArray, modeArray, actualPathCount, actualModeCount) = 
                    queryDisplayConfiguration flags pathCount modeCount
                
                if queryResult <> WindowsAPI.ERROR.ERROR_SUCCESS then
                    printfn "[DEBUG] QueryDisplayConfig failed with error: %d" queryResult
                    Error (sprintf "Failed to query display config: %d" queryResult)
                else
                    printfn "[DEBUG] Successfully queried %d paths and %d modes" actualPathCount actualModeCount
                    Ok (pathArray, modeArray, actualPathCount, actualModeCount)
        with
        | ex ->
            printfn "[DEBUG] Exception in getDisplayPaths: %s" ex.Message
            Error (sprintf "Exception getting display paths: %s" ex.Message)

    // Filter paths to only include relevant ones for display configuration
    let filterRelevantPaths (paths: WindowsAPI.DISPLAYCONFIG_PATH_INFO[]) pathCount =
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

    // Improved path finding that handles large path arrays correctly
    let findDisplayPathBySourceId displayId (paths: WindowsAPI.DISPLAYCONFIG_PATH_INFO[]) pathCount =
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
                // Get target ID mapping from WMI data
                let targetIdMapping = DisplayDetection.getDisplayTargetIdMapping()
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

    // Simplified but robust mapping that works for both enabling and disabling displays
    let findDisplayPathByDevice displayId (paths: WindowsAPI.DISPLAYCONFIG_PATH_INFO[]) pathCount =
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

    // Find the display path for a specific display ID (using simplified approach)
    let findDisplayPath displayId (paths: WindowsAPI.DISPLAYCONFIG_PATH_INFO[]) pathCount =
        findDisplayPathByDevice displayId paths pathCount

    // Enhanced function to get display paths with validation
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

    // Find inactive display paths specifically - improved version
    let findInactiveDisplayPath displayId (paths: WindowsAPI.DISPLAYCONFIG_PATH_INFO[]) pathCount =
        try
            printfn "[DEBUG] Finding inactive path for %s in %d total paths" displayId pathCount
            
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

    // Apply display configuration with proper validation
    let applyDisplayConfiguration (paths: WindowsAPI.DISPLAYCONFIG_PATH_INFO[]) (modes: WindowsAPI.DISPLAYCONFIG_MODE_INFO[]) pathCount modeCount flags =
        try
            printfn "[DEBUG] Applying display configuration with %d paths, %d modes" pathCount modeCount
            printfn "[DEBUG] Flags: 0x%08X" flags
            
            // Log only essential configuration details (reduced verbosity)
            if int pathCount <= 5 then
                for i in 0 .. int pathCount - 1 do
                    let path = paths.[i]
                    printfn "[DEBUG] Path %d: Source %d -> Target %d, Flags: 0x%08X" 
                            i path.sourceInfo.id path.targetInfo.id path.flags
            else
                printfn "[DEBUG] Configuring %d paths (details omitted for brevity)" pathCount
            
            let result = WindowsAPI.SetDisplayConfig(pathCount, paths, modeCount, modes, flags)
            
            if result = WindowsAPI.ERROR.ERROR_SUCCESS then
                printfn "[DEBUG] SetDisplayConfig succeeded"
                Ok ()
            else
                let errorMsg = match result with
                               | x when x = WindowsAPI.ERROR.ERROR_INVALID_PARAMETER -> "Invalid parameter - check path/mode configuration"
                               | x when x = WindowsAPI.ERROR.ERROR_NOT_SUPPORTED -> "Operation not supported by driver"
                               | x when x = WindowsAPI.ERROR.ERROR_ACCESS_DENIED -> "Access denied - insufficient privileges"
                               | x when x = WindowsAPI.ERROR.ERROR_GEN_FAILURE -> "General failure in display driver"
                               | _ -> sprintf "Unknown error code: %d" result
                printfn "[DEBUG] SetDisplayConfig failed: %s" errorMsg
                Error errorMsg
        with
        | ex ->
            printfn "[DEBUG] Exception in applyDisplayConfiguration: %s" ex.Message
            Error (sprintf "Exception applying configuration: %s" ex.Message)

    // Apply display configuration with filtered paths to avoid "Invalid parameter" with large arrays
    let applyDisplayConfigurationFiltered (paths: WindowsAPI.DISPLAYCONFIG_PATH_INFO[]) (modes: WindowsAPI.DISPLAYCONFIG_MODE_INFO[]) pathCount modeCount flags =
        try
            printfn "[DEBUG] Applying filtered display configuration (original: %d paths)" pathCount
            
            // Filter paths if we have a large array that might cause issues
            let (finalPaths, finalPathCount) = 
                if pathCount > 10u then
                    printfn "[DEBUG] Large path array detected, filtering to relevant paths only"
                    filterRelevantPaths paths pathCount
                else
                    (paths, pathCount)
            
            printfn "[DEBUG] Using %d paths for SetDisplayConfig" finalPathCount
            applyDisplayConfiguration finalPaths modes finalPathCount modeCount flags
        with
        | ex ->
            printfn "[DEBUG] Exception in filtered apply: %s" ex.Message
            Error (sprintf "Exception in filtered apply: %s" ex.Message)

    // Create a source mode info entry from display mode
    let createSourceModeInfo adapterId sourceId width height =
        let mutable sourceMode = WindowsAPI.DISPLAYCONFIG_MODE_INFO()
        sourceMode.infoType <- WindowsAPI.DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE
        sourceMode.id <- sourceId
        sourceMode.adapterId <- adapterId
        sourceMode.modeInfo.sourceMode.width <- uint32 width
        sourceMode.modeInfo.sourceMode.height <- uint32 height
        sourceMode.modeInfo.sourceMode.pixelFormat <- 0x00000003u  // DISPLAYCONFIG_PIXELFORMAT_32BPP
        sourceMode.modeInfo.sourceMode.position.x <- 0
        sourceMode.modeInfo.sourceMode.position.y <- 0
        sourceMode

    // Create a target mode info entry from display mode
    let createTargetModeInfo adapterId targetId width height refreshRate =
        let mutable targetMode = WindowsAPI.DISPLAYCONFIG_MODE_INFO()
        targetMode.infoType <- WindowsAPI.DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET
        targetMode.id <- targetId
        targetMode.adapterId <- adapterId
        
        // Set up video signal info
        let mutable videoInfo = targetMode.modeInfo.targetMode.targetVideoSignalInfo
        videoInfo.activeSize.cx <- uint32 width
        videoInfo.activeSize.cy <- uint32 height
        videoInfo.totalSize.cx <- uint32 width
        videoInfo.totalSize.cy <- uint32 height
        videoInfo.vSyncFreq.Numerator <- uint32 refreshRate
        videoInfo.vSyncFreq.Denominator <- 1u
        videoInfo.hSyncFreq.Numerator <- uint32 (refreshRate * height / 1000) // Approximate
        videoInfo.hSyncFreq.Denominator <- 1u
        videoInfo.videoStandard <- 0u
        videoInfo.scanLineOrdering <- 0u
        
        targetMode.modeInfo.targetMode.targetVideoSignalInfo <- videoInfo
        targetMode

    // Create mode information for inactive display path
    let createModeInfoForPath (path: WindowsAPI.DISPLAYCONFIG_PATH_INFO) displayId (bestMode: DisplayMode) =
        try
            let sourceMode = createSourceModeInfo path.sourceInfo.adapterId path.sourceInfo.id bestMode.Width bestMode.Height
            let targetMode = createTargetModeInfo path.targetInfo.adapterId path.targetInfo.id bestMode.Width bestMode.Height bestMode.RefreshRate
            
            printfn "[DEBUG] Created mode info for %s: %dx%d @ %dHz" displayId bestMode.Width bestMode.Height bestMode.RefreshRate
            Ok (sourceMode, targetMode)
        with
        | ex ->
            Error (sprintf "Exception creating mode info: %s" ex.Message)

    // Populate modes for inactive path - adds modes to arrays and updates path indices
    let populateModesForInactivePath (pathArray: WindowsAPI.DISPLAYCONFIG_PATH_INFO[]) (modeArray: WindowsAPI.DISPLAYCONFIG_MODE_INFO[]) pathIndex displayId (bestMode: DisplayMode) =
        try
            let path = pathArray.[pathIndex]
            if path.sourceInfo.modeInfoIdx = WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_MODE_IDX_INVALID ||
               path.targetInfo.modeInfoIdx = WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_MODE_IDX_INVALID then
                
                match createModeInfoForPath path displayId bestMode with
                | Ok (sourceMode, targetMode) ->
                    // Find next available slots in mode array
                    let sourceIndex = modeArray |> Array.findIndex (fun m -> m.infoType = 0u)
                    let targetIndex = modeArray |> Array.findIndexBack (fun m -> m.infoType = 0u)
                    
                    if sourceIndex >= 0 && targetIndex >= 0 && sourceIndex <> targetIndex then
                        // Add modes to array
                        modeArray.[sourceIndex] <- sourceMode
                        modeArray.[targetIndex] <- targetMode
                        
                        // Update path to point to these modes
                        let mutable updatedPath = path
                        updatedPath.sourceInfo.modeInfoIdx <- uint32 sourceIndex
                        updatedPath.targetInfo.modeInfoIdx <- uint32 targetIndex
                        pathArray.[pathIndex] <- updatedPath
                        
                        printfn "[DEBUG] Populated modes for path %d: source index %d, target index %d" pathIndex sourceIndex targetIndex
                        Ok ()
                    else
                        Error "No available slots in mode array"
                | Error err -> Error err
            else
                printfn "[DEBUG] Path already has mode information - no population needed"
                Ok ()
        with
        | ex ->
            Error (sprintf "Exception populating modes: %s" ex.Message)

    // Validate display path integrity (updated to allow missing mode info that we'll populate)
    let validateDisplayPath (path: WindowsAPI.DISPLAYCONFIG_PATH_INFO) =
        let sourceValid = path.sourceInfo.id <> 0u || path.sourceInfo.adapterId.LowPart <> 0u
        let targetValid = path.targetInfo.id <> 0u || path.targetInfo.adapterId.LowPart <> 0u
        
        if not sourceValid then
            Error "Display path has invalid source information"
        elif not targetValid then
            Error "Display path has invalid target information"  
        else
            Ok path

    // Update display position using CCD API
    let updateDisplayPosition displayId newPosition =
        result {
            printfn "[DEBUG] Updating %s position to (%d, %d) using CCD API" displayId newPosition.X newPosition.Y
            
            let! (pathArray, modeArray, pathCount, modeCount) = getDisplayPaths false
            let! (path, pathIndex) = findDisplayPath displayId pathArray pathCount
            
            // Find the source mode for this display
            let sourceIndex = int path.sourceInfo.modeInfoIdx
            if sourceIndex = int WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_MODE_IDX_INVALID then
                return! Error (sprintf "Source mode index is invalid for display %s" displayId)
            elif sourceIndex >= 0 && sourceIndex < int modeCount then
                let mutable sourceMode = modeArray.[sourceIndex]
                
                // Update position in source mode
                sourceMode.modeInfo.sourceMode.position.x <- int32 newPosition.X
                sourceMode.modeInfo.sourceMode.position.y <- int32 newPosition.Y
                modeArray.[sourceIndex] <- sourceMode
                
                printfn "[DEBUG] Updated source mode position for %s at index %d" displayId sourceIndex
                
                // Apply configuration with updated position
                let flags = WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE
                return! applyDisplayConfiguration pathArray modeArray pathCount modeCount flags
            else
                return! Error (sprintf "Invalid source mode index %d for display %s" sourceIndex displayId)
        }

    // Apply multiple position changes atomically using CCD API with validation
    let applyMultiplePositionChanges (positionChanges: (DisplayId * Position) list) =
        result {
            printfn "[DEBUG] Applying %d position changes using CCD API" (List.length positionChanges)
            
            // Get fresh configuration to avoid stale state issues
            let! (pathArray, modeArray, pathCount, modeCount) = getDisplayPaths false
            
            // Update all positions in the mode array
            let updateResults = 
                positionChanges
                |> List.map (fun (displayId, newPosition) ->
                    match findDisplayPath displayId pathArray pathCount with
                    | Ok (path, _) ->
                        let sourceIndex = int path.sourceInfo.modeInfoIdx
                        if sourceIndex = int WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_MODE_IDX_INVALID then
                            printfn "[ERROR] Source mode index is invalid for %s" displayId
                            Error (sprintf "Source mode index is invalid for %s" displayId)
                        elif sourceIndex >= 0 && sourceIndex < int modeCount then
                            let mutable sourceMode = modeArray.[sourceIndex]
                            sourceMode.modeInfo.sourceMode.position.x <- int32 newPosition.X
                            sourceMode.modeInfo.sourceMode.position.y <- int32 newPosition.Y
                            modeArray.[sourceIndex] <- sourceMode
                            printfn "[DEBUG] Updated %s position to (%d, %d)" displayId newPosition.X newPosition.Y
                            Ok ()
                        else
                            printfn "[ERROR] Invalid source mode index %d for %s" sourceIndex displayId
                            Error (sprintf "Invalid source mode index %d for %s" sourceIndex displayId)
                    | Error err ->
                        printfn "[ERROR] Failed to find path for %s: %s" displayId err
                        Error (sprintf "Failed to find path for %s: %s" displayId err))
            
            // Check if all updates succeeded
            let errors = updateResults |> List.choose (function | Error e -> Some e | Ok _ -> None)
            if not (List.isEmpty errors) then
                return! Error (sprintf "Failed to update displays: %s" (String.concat "; " errors))
            else
                // First validate the configuration
                printfn "[DEBUG] Validating configuration before applying..."
                let validationFlags = WindowsAPI.SDC.SDC_VALIDATE ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG
                let validationResult = WindowsAPI.SetDisplayConfig(pathCount, pathArray, modeCount, modeArray, validationFlags)
                
                if validationResult <> WindowsAPI.ERROR.ERROR_SUCCESS then
                    printfn "[DEBUG] Configuration validation failed with code: %d" validationResult
                    return! Error (sprintf "Configuration validation failed before application (code: %d)" validationResult)
                else
                    printfn "[DEBUG] Configuration validation passed"
                    // Apply all changes atomically
                    let flags = WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE
                    return! applyDisplayConfiguration pathArray modeArray pathCount modeCount flags
        }