namespace DisplaySwitchPro

open System
open WindowsAPI
open ResultBuilder
open CCDPathManagement

// CCD (Connecting and Configuring Displays) API helper functions
module DisplayConfigurationAPI =

    // Import path management functions directly
    let getDisplayPaths = CCDPathManagement.getDisplayPaths
    let filterRelevantPaths = CCDPathManagement.filterRelevantPaths
    let getDisplayPathsWithValidation = CCDPathManagement.getDisplayPathsWithValidation

    // Apply display configuration with proper validation
    let applyDisplayConfiguration (paths: WindowsAPI.DISPLAYCONFIG_PATH_INFO[]) (modes: WindowsAPI.DISPLAYCONFIG_MODE_INFO[]) (pathCount: uint32) (modeCount: uint32) flags =
        try
            printfn "[DEBUG] Applying display configuration with %u paths, %u modes" pathCount modeCount
            printfn "[DEBUG] Flags: 0x%08X" flags

            // Log only essential configuration details (reduced verbosity)
            if int pathCount <= 5 then
                for i in 0 .. int pathCount - 1 do
                    let path = paths.[i]
                    printfn "[DEBUG] Path %d: Source %d -> Target %d, Flags: 0x%08X"
                            i path.sourceInfo.id path.targetInfo.id path.flags
            else
                printfn "[DEBUG] Configuring %u paths (details omitted for brevity)" pathCount

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
    let applyDisplayConfigurationFiltered (paths: WindowsAPI.DISPLAYCONFIG_PATH_INFO[]) (modes: WindowsAPI.DISPLAYCONFIG_MODE_INFO[]) (pathCount: uint32) (modeCount: uint32) flags =
        try
            printfn "[DEBUG] Applying filtered display configuration (original: %u paths)" pathCount

            // Filter paths if we have a large array that might cause issues
            let (finalPaths, finalPathCount) =
                if pathCount > 10u then
                    printfn "[DEBUG] Large path array detected, filtering to relevant paths only"
                    filterRelevantPaths paths pathCount
                else
                    (paths, pathCount)

            printfn "[DEBUG] Using %u paths for SetDisplayConfig" finalPathCount
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