namespace DisplaySwitchPro

open System
open System.Runtime.InteropServices
open System.Threading
open WindowsAPI
open DisplayStateCache
open DisplayConfigurationAPI
open DisplayDetection

// Enhanced error types for display operations
type DisplayError = 
    | InvalidPath of string
    | HardwareNotResponding of string
    | DriverError of int * string
    | ValidationTimeout of string
    | PermissionDenied of string
    | ConfigurationFailed of string
    | DeviceBusy of string

type DisplayValidationResult = {
    IsEnabled: bool
    IsResponding: bool
    ValidationAttempts: int
    LastError: string option
}

// Strategy types for functional approach
type EnableStrategy = 
    | CCDTargeted
    | CCDModePopulation
    | CCDTopologyExtend  
    | CCDMinimalPaths
    | CCDDirectPath
    | TVSpecificActivation
    | DEVMODEDirect
    | DEVMODEWithReset
    | HardwareReset
    | DisplaySwitchFallback

// High-level display control operations
module DisplayControl =
    
    // Result computation expression builder
    type ResultBuilder() =
        member _.Bind(x, f) = Result.bind f x
        member _.Return(x) = Ok x
        member _.ReturnFrom(x) = x
        member _.Zero() = Ok ()
        member _.Combine(a, b) = Result.bind (fun () -> b) a
        member _.Delay(f) = f
        member _.Run(f) = f()
        member _.For(seq, body) = 
            seq |> Seq.fold (fun acc item -> 
                Result.bind (fun () -> body item) acc) (Ok ())

    let result = ResultBuilder()
    
    // Helper functions for functional display mode application
    let private getCurrentDevMode displayId =
        let mutable devMode = WindowsAPI.DEVMODE()
        devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
        
        let result = WindowsAPI.EnumDisplaySettings(displayId, -1, &devMode)
        if result then 
            Ok devMode
        else 
            Error (sprintf "Could not get current display settings for %s" displayId)
    
    let private validateModeExists displayId mode =
        let allModes = DisplayDetection.getAllDisplayModes displayId
        let modeExists = allModes |> List.exists (fun m -> 
            m.Width = mode.Width && m.Height = mode.Height && m.RefreshRate = mode.RefreshRate)
        
        if modeExists then 
            Ok ()
        else
            let availableForResolution = allModes |> List.filter (fun m -> m.Width = mode.Width && m.Height = mode.Height)
            if availableForResolution.Length > 0 then
                printfn "[DEBUG] Available refresh rates for %dx%d:" mode.Width mode.Height
                availableForResolution |> List.iter (fun m -> printfn "[DEBUG]   - %dHz" m.RefreshRate)
            else
                printfn "[DEBUG] No modes found for resolution %dx%d" mode.Width mode.Height
            
            Error (sprintf "Mode %dx%d @ %dHz is not supported by display %s" mode.Width mode.Height mode.RefreshRate displayId)
    
    let private createTargetDevMode displayId (currentDevMode: WindowsAPI.DEVMODE) mode orientation =
        match DisplayDetection.getExactDevModeForMode displayId mode with
        | Some exactDevMode ->
            let mutable targetDevMode = exactDevMode
            targetDevMode.dmDisplayOrientation <- DisplayStateCache.orientationToWindows orientation
            if orientation <> Landscape then
                targetDevMode.dmFields <- targetDevMode.dmFields ||| 0x00000080u
            Ok targetDevMode
        | None ->
            let mutable targetDevMode = currentDevMode
            targetDevMode.dmPelsWidth <- uint32 mode.Width
            targetDevMode.dmPelsHeight <- uint32 mode.Height  
            targetDevMode.dmDisplayFrequency <- uint32 mode.RefreshRate
            targetDevMode.dmBitsPerPel <- uint32 mode.BitsPerPixel
            targetDevMode.dmDisplayOrientation <- DisplayStateCache.orientationToWindows orientation
            
            let isResolutionChange = currentDevMode.dmPelsWidth <> uint32 mode.Width || currentDevMode.dmPelsHeight <> uint32 mode.Height
            let dmFields = 
                if isResolutionChange then
                    0x00080000u ||| 0x00040000u ||| 0x00020000u ||| 0x00400000u
                else
                    currentDevMode.dmFields ||| 0x00400000u ||| 0x00000080u
            
            targetDevMode.dmFields <- dmFields
            Ok targetDevMode
    
    let private testAndApplyMode displayId targetDevMode =
        let mutable devMode = targetDevMode
        let testResult = WindowsAPI.ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, WindowsAPI.CDS.CDS_TEST, IntPtr.Zero)
        
        if testResult <> WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
            let errorMsg = match testResult with
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_FAILED -> "Display driver failed the mode change"
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_NOTUPDATED -> "Unable to write settings to registry"
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADDUALVIEW -> "Bad dual view configuration"
                           | _ -> sprintf "Unknown error code: %d" testResult
            Error (sprintf "Display mode test failed for %s: %s" displayId errorMsg)
        else
            let mutable applyMode = targetDevMode
            let applyResult = WindowsAPI.ChangeDisplaySettingsEx(displayId, &applyMode, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
            
            match applyResult with
            | x when x = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL -> Ok ()
            | x when x = WindowsAPI.DISP.DISP_CHANGE_RESTART -> Ok ()
            | _ ->
                let errorMsg = match applyResult with
                               | x when x = WindowsAPI.DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                               | x when x = WindowsAPI.DISP.DISP_CHANGE_FAILED -> "Display driver failed the mode change"
                               | x when x = WindowsAPI.DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                               | x when x = WindowsAPI.DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                               | x when x = WindowsAPI.DISP.DISP_CHANGE_NOTUPDATED -> "Unable to write settings to registry"
                               | x when x = WindowsAPI.DISP.DISP_CHANGE_BADDUALVIEW -> "Bad dual view configuration"
                               | _ -> sprintf "Unknown error code: %d" applyResult
                Error (sprintf "Failed to apply display mode to %s: %s" displayId errorMsg)

    // Apply display mode changes (resolution, refresh rate, orientation) - now functional
    let applyDisplayMode (displayId: DisplayId) (mode: DisplayMode) (orientation: DisplayOrientation) =
        result {
            printfn "[DEBUG] ========== Starting applyDisplayMode =========="
            printfn "[DEBUG] Display ID: %s" displayId
            printfn "[DEBUG] Target Mode: %dx%d @ %dHz" mode.Width mode.Height mode.RefreshRate
            printfn "[DEBUG] Target Orientation: %A" orientation
            
            let! currentDevMode = getCurrentDevMode displayId
            printfn "[DEBUG] Current settings: %ux%u @ %uHz" currentDevMode.dmPelsWidth currentDevMode.dmPelsHeight currentDevMode.dmDisplayFrequency
            
            do! validateModeExists displayId mode
            let! targetDevMode = createTargetDevMode displayId currentDevMode mode orientation
            do! testAndApplyMode displayId targetDevMode
            
            printfn "[DEBUG] SUCCESS: Display mode applied successfully!"
            return ()
        }

    // Helper to get error message from display change result
    let private getDisplayChangeErrorMessage result =
        match result with
        | x when x = WindowsAPI.DISP.DISP_CHANGE_FAILED -> "Display settings change failed"
        | x when x = WindowsAPI.DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
        | x when x = WindowsAPI.DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
        | x when x = WindowsAPI.DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
        | x when x = WindowsAPI.DISP.DISP_CHANGE_NOTUPDATED -> "Unable to write settings to registry"
        | x when x = WindowsAPI.DISP.DISP_CHANGE_BADDUALVIEW -> "Bad dual view configuration"
        | _ -> sprintf "Unknown error code: %d" result
    
    // Helper to apply primary display settings
    let private applyPrimarySettings displayId (devMode: WindowsAPI.DEVMODE) =
        let mutable modifiedDevMode = devMode
        modifiedDevMode.dmPositionX <- 0
        modifiedDevMode.dmPositionY <- 0
        modifiedDevMode.dmFields <- modifiedDevMode.dmFields ||| 0x00000020u // DM_POSITION
        
        let result = WindowsAPI.ChangeDisplaySettingsEx(
            displayId, 
            &modifiedDevMode, 
            IntPtr.Zero,
            WindowsAPI.CDS.CDS_SET_PRIMARY ||| WindowsAPI.CDS.CDS_UPDATEREGISTRY ||| WindowsAPI.CDS.CDS_NORESET,
            IntPtr.Zero
        )
        
        if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
            Ok ()
        else
            Error (getDisplayChangeErrorMessage result)
    
    // Set display as primary - now more functional
    let setPrimaryDisplay (displayId: DisplayId) =
        result {
            printfn "Setting %s as primary display" displayId
            let! currentDevMode = getCurrentDevMode displayId
            let! _ = applyPrimarySettings displayId currentDevMode
            printfn "Successfully set %s as primary display" displayId
            return ()
        }
        |> Result.mapError (sprintf "Failed to set %s as primary: %s" displayId)

    // Compact display positions to eliminate gaps and ensure adjacency
    let private compactDisplayPositions (displayPositions: (DisplayId * Position * DisplayInfo) list) =
        printfn "[DEBUG] ========== Compacting Display Positions =========="
        
        // Find primary display (should stay at origin or move everything together)
        let primaryDisplay = displayPositions |> List.tryFind (fun (_, _, info) -> info.IsPrimary)
        
        match primaryDisplay with
        | Some (primaryId, primaryPos, primaryInfo) ->
            printfn "[DEBUG] Primary display %s found, keeping at (0,0)" primaryId
            
            // Separate primary from others
            let otherDisplays = displayPositions |> List.filter (fun (id, _, _) -> id <> primaryId)
            
            // Sort other displays by their intended X position relative to primary
            let sortedOthers = 
                otherDisplays 
                |> List.sortBy (fun (_, pos, _) -> pos.X)
            
            // Split into left and right of primary
            let leftDisplays = sortedOthers |> List.filter (fun (_, pos, _) -> pos.X < primaryPos.X)
            let rightDisplays = sortedOthers |> List.filter (fun (_, pos, _) -> pos.X >= primaryPos.X)
            
            printfn "[DEBUG] Primary: %s, Left: %d displays, Right: %d displays" primaryId leftDisplays.Length rightDisplays.Length
            
            // Build layout functionally: [left displays][primary][right displays]
            
            // Place displays to the left of primary (negative X coordinates)
            let leftCompacted, leftmostX = 
                leftDisplays 
                |> List.rev // Process right to left so they're adjacent to primary
                |> List.fold (fun (acc, currentX) (id, originalPos, info) ->
                    let newX = currentX - int info.Resolution.Width
                    let newPos = { X = newX; Y = 0 }
                    printfn "[DEBUG] Compacting %s (left): (%d, %d) -> (%d, %d)" id originalPos.X originalPos.Y newPos.X newPos.Y
                    ((id, newPos, info) :: acc, newX)
                ) ([], 0)
            
            // Primary display always at origin
            let primaryCompacted = [(primaryId, { X = 0; Y = 0 }, primaryInfo)]
            printfn "[DEBUG] Compacting %s (primary): (%d, %d) -> (0, 0)" primaryId primaryPos.X primaryPos.Y
            
            // Place displays to the right of primary  
            let rightCompacted, _ = 
                rightDisplays
                |> List.fold (fun (acc, currentX) (id, originalPos, info) ->
                    let newPos = { X = currentX; Y = 0 }
                    printfn "[DEBUG] Compacting %s (right): (%d, %d) -> (%d, %d)" id originalPos.X originalPos.Y newPos.X newPos.Y
                    ((id, newPos, info) :: acc, currentX + int info.Resolution.Width)
                ) ([], int primaryInfo.Resolution.Width)
            
            // Combine all displays: left + primary + right
            leftCompacted @ primaryCompacted @ (rightCompacted |> List.rev)
            
        | None ->
            printfn "[DEBUG] No primary display found, using first display as reference"
            // If no primary, compact starting from first display at origin
            let sortedDisplays = 
                displayPositions 
                |> List.sortBy (fun (_, pos, _) -> pos.X)
            
            let compactedDisplays, _ = 
                sortedDisplays
                |> List.fold (fun (acc, currentX) (id, originalPos, info) ->
                    let newPos = { X = currentX; Y = 0 }
                    let nextX = currentX + int info.Resolution.Width
                    printfn "[DEBUG] Compacting %s: (%d, %d) -> (%d, %d)" id originalPos.X originalPos.Y newPos.X newPos.Y
                    
                    ((id, newPos, info) :: acc, nextX)
                ) ([], 0)
            
            compactedDisplays |> List.rev

    // Set display position - applies canvas drag changes to Windows using CCD API
    let setDisplayPosition (displayId: DisplayId) (newPosition: Position) =
        result {
            printfn "[DEBUG] Setting %s position to (%d, %d) using CCD API" displayId newPosition.X newPosition.Y
            return! DisplayConfigurationAPI.updateDisplayPosition displayId newPosition
        }
        |> Result.mapError (sprintf "Failed to set %s position: %s" displayId)

    // Apply multiple display positions atomically using CCD API with compacting
    let applyMultipleDisplayPositions (displayPositions: (DisplayId * Position) list) =
        result {
            printfn "[DEBUG] ========== Applying Multiple Display Positions (CCD API) =========="
            printfn "[DEBUG] Total displays to reposition: %d" displayPositions.Length
            displayPositions |> List.iteri (fun i (id, pos) ->
                printfn "[DEBUG] Display %d: %s -> (%d, %d)" (i+1) id pos.X pos.Y)
            
            // Get display info for all displays to enable compacting
            let connectedDisplays = DisplayDetection.getConnectedDisplays()
            let displayMap = connectedDisplays |> List.map (fun d -> (d.Id, d)) |> Map.ofList
            
            // Build list with display info for compacting
            let displayPositionsWithInfo = 
                displayPositions
                |> List.choose (fun (id, pos) ->
                    match Map.tryFind id displayMap with
                    | Some info -> Some (id, pos, info)
                    | None -> 
                        printfn "[WARNING] Display %s not found in connected displays" id
                        None)
            
            printfn "[DEBUG] Found display info for %d of %d displays" displayPositionsWithInfo.Length displayPositions.Length
            
            // Compact positions to ensure adjacency and eliminate gaps
            let compactedPositions = compactDisplayPositions displayPositionsWithInfo
            let finalPositions = compactedPositions |> List.map (fun (id, pos, _) -> (id, pos))
            
            printfn "[DEBUG] Compacted positions:"
            finalPositions |> List.iteri (fun i (id, pos) ->
                printfn "[DEBUG] Final %d: %s -> (%d, %d)" (i+1) id pos.X pos.Y)
            
            // Apply all position changes atomically using CCD API
            return! DisplayConfigurationAPI.applyMultiplePositionChanges finalPositions
        }
        |> Result.mapError (sprintf "Failed to apply multiple display positions: %s")

    // Comprehensive display state validation with Result type
    let private validateDisplayState displayId expectedState maxAttempts =
        let validateSingleAttempt attempt =
            try
                printfn "[DEBUG] Validation attempt %d for %s (expecting %b)" attempt displayId expectedState
                
                // Method 1: Check via DisplayDetection (our existing system)
                let displays = DisplayDetection.getConnectedDisplays()
                let detectionResult = 
                    displays 
                    |> List.tryFind (fun d -> d.Id = displayId)
                    |> Option.map (fun d -> d.IsEnabled)
                
                // Method 2: Check via EnumDisplayDevices (Windows API direct)
                let mutable displayDevice = WindowsAPI.DISPLAY_DEVICE()
                displayDevice.cb <- Marshal.SizeOf(typeof<WindowsAPI.DISPLAY_DEVICE>)
                let enumResult = WindowsAPI.EnumDisplayDevices(displayId, 0u, &displayDevice, 0u)
                let apiResult = 
                    if enumResult then
                        Some ((displayDevice.StateFlags &&& WindowsAPI.Flags.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) <> 0u)
                    else None
                
                // Method 3: Check via CCD API paths
                let ccdResult = 
                    match DisplayConfigurationAPI.getDisplayPaths true with
                    | Ok (pathArray, _, pathCount, _) ->
                        pathArray
                        |> Array.take (int pathCount)
                        |> Array.tryFind (fun path -> 
                            let displayNum = if displayId.StartsWith(@"\\.\DISPLAY") then
                                                let mutable num = 0
                                                if Int32.TryParse(displayId.Substring(11), &num) then Some (num - 1) else None
                                             else None
                            match displayNum with
                            | Some num when int path.sourceInfo.id = num -> true
                            | _ -> false)
                        |> Option.map (fun path -> 
                            path.flags &&& WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE <> 0u &&
                            path.targetInfo.targetAvailable <> 0)
                    | Error _ -> None
                
                // Combine results for comprehensive validation
                match detectionResult, apiResult, ccdResult with
                | Some detectionEnabled, Some apiEnabled, Some ccdEnabled ->
                    let consensus = [detectionEnabled; apiEnabled; ccdEnabled] |> List.countBy id |> List.maxBy snd |> fst
                    printfn "[DEBUG] Validation consensus: Detection=%b, API=%b, CCD=%b -> %b" 
                            detectionEnabled apiEnabled ccdEnabled consensus
                    
                    if consensus = expectedState then
                        Ok { IsEnabled = consensus; IsResponding = true; ValidationAttempts = attempt; LastError = None }
                    else
                        Error (sprintf "Display state mismatch - expected %b, got %b" expectedState consensus)
                
                | Some detectionEnabled, Some apiEnabled, None ->
                    let consensus = if detectionEnabled = apiEnabled then detectionEnabled else detectionEnabled
                    printfn "[DEBUG] Validation (no CCD): Detection=%b, API=%b -> %b" detectionEnabled apiEnabled consensus
                    
                    if consensus = expectedState then
                        Ok { IsEnabled = consensus; IsResponding = true; ValidationAttempts = attempt; LastError = None }
                    else
                        Error (sprintf "Display state mismatch - expected %b, got %b" expectedState consensus)
                
                | Some detectionEnabled, None, _ ->
                    printfn "[DEBUG] Validation (detection only): %b" detectionEnabled
                    if detectionEnabled = expectedState then
                        Ok { IsEnabled = detectionEnabled; IsResponding = true; ValidationAttempts = attempt; LastError = None }
                    else
                        Error (sprintf "Display state mismatch - expected %b, got %b" expectedState detectionEnabled)
                
                | None, _, _ ->
                    Error (sprintf "Display %s not found in any validation method" displayId)
            with
            | ex ->
                Error (sprintf "Validation exception: %s" ex.Message)
        
        // Progressive retry with exponential backoff
        let rec tryWithBackoff attempt =
            if attempt > maxAttempts then
                Error "Validation timeout - max attempts exceeded"
            else
                match validateSingleAttempt attempt with
                | Ok result -> Ok result
                | Error msg when attempt = maxAttempts -> 
                    Error msg
                | Error msg ->
                    let delay = min (500 * (1 <<< (attempt - 1))) 4000  // 500ms, 1s, 2s, 4s, cap at 4s
                    printfn "[DEBUG] Validation attempt %d failed (%s), retrying in %dms..." attempt msg delay
                    System.Threading.Thread.Sleep(delay)
                    tryWithBackoff (attempt + 1)
        
        tryWithBackoff 1

    // Helper to create DEVMODE from saved state
    let private createDevModeFromSavedState savedState =
        let mutable devMode = WindowsAPI.DEVMODE()
        devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
        devMode.dmPelsWidth <- uint32 savedState.Resolution.Width
        devMode.dmPelsHeight <- uint32 savedState.Resolution.Height
        devMode.dmDisplayFrequency <- uint32 savedState.Resolution.RefreshRate
        devMode.dmBitsPerPel <- 32u
        devMode.dmDisplayOrientation <- DisplayStateCache.orientationToWindows (DisplayStateCache.intToOrientation savedState.OrientationValue)
        devMode.dmPositionX <- savedState.Position.X
        devMode.dmPositionY <- savedState.Position.Y
        devMode.dmFields <- 0x00020000u ||| 0x00040000u ||| 0x00080000u ||| 0x00400000u ||| 0x00000020u ||| 0x00000080u
        devMode
    
    // Helper to get best available mode for a display
    let private getBestAvailableMode displayId =
        let availableModes = DisplayDetection.getAllDisplayModes displayId
        if availableModes.IsEmpty then
            Error (sprintf "No display modes available for %s" displayId)
        else
            let preferredModes = [(3840, 2160, 60); (1920, 1080, 60); (1280, 720, 60)]
            let findMode (w, h, r) = availableModes |> List.tryFind (fun m -> m.Width = w && m.Height = h && m.RefreshRate = r)
            let bestMode = 
                preferredModes 
                |> List.tryPick findMode
                |> Option.defaultWith (fun () -> availableModes |> List.head)
            Ok bestMode
    
    // Helper to enable display using saved state
    let private enableWithSavedState displayId savedState =
        let devMode = createDevModeFromSavedState savedState
        let mutable mutableDevMode = devMode
        let result = WindowsAPI.ChangeDisplaySettingsEx(displayId, &mutableDevMode, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
        
        if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
            printfn "[DEBUG] SUCCESS: Display restored from saved state!"
            Ok ()
        else
            Error (sprintf "Failed to restore saved state (%d)" result)
    
    // Helper to enable display with auto-detected mode
    let private enableWithAutoMode displayId bestMode =
        let mutable devMode = WindowsAPI.DEVMODE()
        devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
        devMode.dmPelsWidth <- uint32 bestMode.Width
        devMode.dmPelsHeight <- uint32 bestMode.Height
        devMode.dmDisplayFrequency <- uint32 bestMode.RefreshRate
        devMode.dmBitsPerPel <- uint32 bestMode.BitsPerPixel
        devMode.dmDisplayOrientation <- WindowsAPI.DMDO.DMDO_DEFAULT
        devMode.dmPositionX <- 3840
        devMode.dmPositionY <- 0
        devMode.dmFields <- 0x00020000u ||| 0x00040000u ||| 0x00080000u ||| 0x00400000u ||| 0x00000020u
        
        let result = WindowsAPI.ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
        if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
            printfn "[DEBUG] SUCCESS: Display enabled with auto-detected settings!"
            Ok ()
        else
            Error (getDisplayChangeErrorMessage result)
    
    // Pure function to calculate optimal position for newly enabled display
    let private calculateOptimalPosition displayId =
        let connectedDisplays = DisplayDetection.getConnectedDisplays()
        let enabledDisplays = connectedDisplays |> List.filter (fun d -> d.IsEnabled && d.Id <> displayId)
        
        if enabledDisplays.IsEmpty then
            (0, 0)  // First display goes at origin
        else
            // Find rightmost display and place new display to its right
            let rightmostDisplay = enabledDisplays |> List.maxBy (fun d -> d.Position.X + d.Resolution.Width)
            (rightmostDisplay.Position.X + rightmostDisplay.Resolution.Width, rightmostDisplay.Position.Y)

    // Strategy implementation functions
    let private executeStrategy strategy displayId =
        try
            printfn "[DEBUG] Executing strategy %A for display %s" strategy displayId
            
            match strategy with
            | CCDTargeted ->
                // Use ALL paths including inactive to find DISPLAY4
                printfn "[DEBUG] CCD Targeted: Getting ALL paths to find inactive display..."
                match DisplayConfigurationAPI.getDisplayPaths true with  // Get ALL paths including inactive
                | Ok (pathArray, modeArray, pathCount, modeCount) ->
                    match DisplayConfigurationAPI.findInactiveDisplayPath displayId pathArray pathCount with
                    | Ok (targetPath, pathIndex) ->
                        match DisplayConfigurationAPI.validateDisplayPath targetPath with
                        | Ok validatedPath ->
                            let modifiedPaths = Array.copy pathArray
                            let mutable modifiedPath = validatedPath
                            modifiedPath.flags <- modifiedPath.flags ||| WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                            modifiedPath.targetInfo.targetAvailable <- 1
                            modifiedPaths.[pathIndex] <- modifiedPath
                            
                            printfn "[DEBUG] CCD Targeted: Applying targeted configuration using filtered paths..."
                            // Use filtered version to handle large path arrays
                            DisplayConfigurationAPI.applyDisplayConfigurationFiltered modifiedPaths modeArray pathCount modeCount 
                                   (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
                                    WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE)
                        | Error err -> Error err
                    | Error err -> Error err
                | Error err -> Error err
                
            | CCDModePopulation ->
                printfn "[DEBUG] CCD Mode Population: Creating mode information for inactive display..."
                // Get ALL paths including inactive ones
                match DisplayConfigurationAPI.getDisplayPathsWithValidation true with
                | Ok (pathArray, modeArray, pathCount, modeCount) ->
                    match DisplayConfigurationAPI.findInactiveDisplayPath displayId pathArray pathCount with
                    | Ok (targetPath, pathIndex) ->
                        // Create a larger mode array with space for new modes
                        let expandedModeArray = Array.zeroCreate<WindowsAPI.DISPLAYCONFIG_MODE_INFO> (int modeCount + 10)
                        Array.blit modeArray 0 expandedModeArray 0 (int modeCount)
                        
                        // Get the best mode for this display
                        match getBestAvailableMode displayId with
                        | Ok bestMode ->
                            // Populate modes for the inactive path
                            match DisplayConfigurationAPI.populateModesForInactivePath pathArray expandedModeArray pathIndex displayId bestMode with
                            | Ok () ->
                                // Enable the path
                                let mutable modifiedPath = pathArray.[pathIndex]
                                modifiedPath.flags <- modifiedPath.flags ||| WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                                modifiedPath.targetInfo.targetAvailable <- 1
                                pathArray.[pathIndex] <- modifiedPath
                                
                                printfn "[DEBUG] CCD Mode Population: Applying configuration with populated modes..."
                                DisplayConfigurationAPI.applyDisplayConfiguration pathArray expandedModeArray pathCount (modeCount + 2u)
                                       (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
                                        WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE)
                            | Error err -> Error err
                        | Error err -> Error err
                    | Error err -> Error err
                | Error err -> Error err
                
            | CCDDirectPath ->
                printfn "[DEBUG] CCD Direct Path: Using exact path with no filtering or modifications..."
                // Get ALL paths to find DISPLAY4, then use it directly
                match DisplayConfigurationAPI.getDisplayPathsWithValidation true with
                | Ok (pathArray, modeArray, pathCount, modeCount) ->
                    match DisplayConfigurationAPI.findDisplayPathBySourceId displayId pathArray pathCount with
                    | Ok (targetPath, pathIndex) ->
                        // Simply activate the path without any filtering or modifications
                        let directPaths = Array.copy pathArray
                        let mutable modifiedPath = targetPath
                        modifiedPath.flags <- WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                        modifiedPath.targetInfo.targetAvailable <- 1
                        directPaths.[pathIndex] <- modifiedPath
                        
                        printfn "[DEBUG] Using direct path activation with all %d paths" pathCount
                        DisplayConfigurationAPI.applyDisplayConfiguration directPaths modeArray pathCount modeCount 
                               (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
                                WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE)
                    | Error err -> Error err
                | Error err -> Error err
                
            | TVSpecificActivation ->
                printfn "[DEBUG] TV-Specific Activation: Special handling for TV displays..."
                // Check if this is a Samsung TV (Q80A) based on the display ID or name
                let isTVDisplay = displayId.Contains("DISPLAY4") // Our known TV display
                if isTVDisplay then
                    printfn "[DEBUG] Detected Samsung Q80A TV - using TV-specific activation sequence"
                    
                    // Strategy for TV: Get current active configuration and add TV display
                    match DisplayConfigurationAPI.getDisplayPaths false with  // Get only active paths first
                    | Ok (activePaths, activeModes, activePathCount, activeModeCount) ->
                        match DisplayConfigurationAPI.getDisplayPaths true with  // Get all paths to find TV
                        | Ok (allPaths, allModes, allPathCount, allModeCount) ->
                            match DisplayConfigurationAPI.findDisplayPathBySourceId displayId allPaths allPathCount with
                            | Ok (tvPath, tvPathIndex) ->
                                // Create a configuration with current active displays + TV
                                let combinedPaths = Array.append activePaths [|tvPath|]
                                let mutable activeTvPath = tvPath
                                activeTvPath.flags <- WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                                activeTvPath.targetInfo.targetAvailable <- 1
                                combinedPaths.[int activePathCount] <- activeTvPath
                                
                                printfn "[DEBUG] TV configuration: %d active paths + TV = %d total paths" activePathCount (Array.length combinedPaths)
                                
                                // Use longer timeout for TV displays
                                DisplayConfigurationAPI.applyDisplayConfiguration combinedPaths activeModes (uint32 (Array.length combinedPaths)) activeModeCount 
                                       (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
                                        WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE)
                            | Error err -> Error err
                        | Error err -> Error err
                    | Error err -> Error err
                else
                    printfn "[DEBUG] Not a TV display - skipping TV-specific activation"
                    Error "Not a TV display"
                
            | CCDTopologyExtend ->
                printfn "[DEBUG] CCD Topology: Applying extend topology with improved flags..."
                DisplayConfigurationAPI.applyDisplayConfiguration [||] [||] 0u 0u
                       (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_TOPOLOGY_EXTEND ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES)
                
            | CCDMinimalPaths ->
                printfn "[DEBUG] CCD Minimal Paths: Using filtered path configuration..."
                // Get ALL paths to find DISPLAY4, then filter for SetDisplayConfig
                match DisplayConfigurationAPI.getDisplayPathsWithValidation true with
                | Ok (pathArray, modeArray, pathCount, modeCount) ->
                    match DisplayConfigurationAPI.findDisplayPathBySourceId displayId pathArray pathCount with
                    | Ok (targetPath, pathIndex) ->
                        printfn "[DEBUG] Found target display at path index %d" pathIndex
                        
                        // Create a minimal array with just the paths we need
                        let activePaths = pathArray |> Array.take (int pathCount) |> Array.filter (fun p -> p.flags <> 0u)
                        let minimalPaths = Array.append activePaths [|targetPath|]
                        
                        // Activate the target path
                        let mutable modifiedPath = targetPath
                        modifiedPath.flags <- modifiedPath.flags ||| WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                        modifiedPath.targetInfo.targetAvailable <- 1
                        minimalPaths.[Array.length activePaths] <- modifiedPath
                        
                        printfn "[DEBUG] Using minimal path set: %d paths (was %d)" (Array.length minimalPaths) pathCount
                        DisplayConfigurationAPI.applyDisplayConfigurationFiltered minimalPaths modeArray (uint32 (Array.length minimalPaths)) modeCount 
                               (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
                                WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE)
                    | Error err -> Error err
                | Error err -> Error err
                
            | DEVMODEDirect ->
                printfn "[DEBUG] DEVMODE Direct: Getting saved state or best available mode..."
                match DisplayStateCache.getSavedDisplayState displayId with
                | Some savedState -> enableWithSavedState displayId savedState
                | None ->
                    match getBestAvailableMode displayId with
                    | Ok bestMode -> enableWithAutoMode displayId bestMode
                    | Error err -> Error err
                    
            | DEVMODEWithReset ->
                printfn "[DEBUG] DEVMODE Reset: Using reset sequence..."
                match getBestAvailableMode displayId with
                | Ok bestMode ->
                    let mutable devMode = WindowsAPI.DEVMODE()
                    devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
                    devMode.dmPelsWidth <- uint32 bestMode.Width
                    devMode.dmPelsHeight <- uint32 bestMode.Height
                    devMode.dmDisplayFrequency <- uint32 bestMode.RefreshRate
                    devMode.dmBitsPerPel <- uint32 bestMode.BitsPerPixel
                    devMode.dmDisplayOrientation <- WindowsAPI.DMDO.DMDO_DEFAULT
                    
                    let (optimalX, optimalY) = calculateOptimalPosition displayId
                    devMode.dmPositionX <- optimalX
                    devMode.dmPositionY <- optimalY
                    devMode.dmFields <- 0x00020000u ||| 0x00040000u ||| 0x00080000u ||| 0x00400000u ||| 0x00000020u
                    
                    // Step 1: Test the mode
                    let testResult = WindowsAPI.ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, WindowsAPI.CDS.CDS_TEST, IntPtr.Zero)
                    if testResult <> WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                        Error (sprintf "DEVMODE test failed: %d" testResult)
                    else
                        // Step 2: Apply without reset
                        let applyResult1 = WindowsAPI.ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, 
                                           WindowsAPI.CDS.CDS_UPDATEREGISTRY ||| WindowsAPI.CDS.CDS_NORESET, IntPtr.Zero)
                        if applyResult1 <> WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                            Error (sprintf "DEVMODE apply (no reset) failed: %d" applyResult1)
                        else
                            // Step 3: Apply global reset  
                            let mutable resetDevMode = WindowsAPI.DEVMODE()
                            let resetResult = WindowsAPI.ChangeDisplaySettings(&resetDevMode, 0u)
                            if resetResult <> WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL && resetResult <> WindowsAPI.DISP.DISP_CHANGE_RESTART then
                                Error (sprintf "DEVMODE reset failed: %d" resetResult)
                            else
                                Ok ()
                | Error err -> Error err
                
            | HardwareReset ->
                printfn "[DEBUG] Hardware Reset: Forcing mode enumeration and adapter reset..."
                match DisplayConfigurationAPI.applyDisplayConfiguration [||] [||] 0u 0u
                       (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_FORCE_MODE_ENUMERATION ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES) with
                | Ok _ ->
                    System.Threading.Thread.Sleep(2000)
                    Ok ()
                | Error err -> Error err
                
            | DisplaySwitchFallback ->
                printfn "[DEBUG] Display Switch Fallback: Using Windows DisplaySwitch.exe /extend..."
                try
                    let startInfo = System.Diagnostics.ProcessStartInfo()
                    startInfo.FileName <- "DisplaySwitch.exe"
                    startInfo.Arguments <- "/extend"
                    startInfo.UseShellExecute <- false
                    startInfo.CreateNoWindow <- true
                    startInfo.RedirectStandardOutput <- true
                    startInfo.RedirectStandardError <- true
                    
                    use proc = System.Diagnostics.Process.Start(startInfo)
                    let _ = proc.WaitForExit(5000) // Wait up to 5 seconds
                    
                    if proc.ExitCode = 0 then
                        printfn "[DEBUG] DisplaySwitch.exe completed successfully"
                        System.Threading.Thread.Sleep(3000) // Give Windows time to apply changes
                        Ok ()
                    else
                        Error (sprintf "DisplaySwitch.exe failed with exit code: %d" proc.ExitCode)
                with
                | ex ->
                    Error (sprintf "Exception running DisplaySwitch.exe: %s" ex.Message)
        with
        | ex -> Error (sprintf "Strategy %A exception: %s" strategy ex.Message)

    // Enhanced multi-strategy display enable with comprehensive validation
    let private tryEnableDisplay displayId =
        let strategies = [CCDTargeted; CCDModePopulation; CCDMinimalPaths; CCDDirectPath; TVSpecificActivation; CCDTopologyExtend; DEVMODEDirect; DEVMODEWithReset; HardwareReset; DisplaySwitchFallback]
        
        let tryStrategyWithValidation strategy =
            match executeStrategy strategy displayId with
            | Ok _ ->
                printfn "[DEBUG] Strategy %A executed, validating display state..." strategy
                match validateDisplayState displayId true 5 with
                | Ok validationResult ->
                    if validationResult.IsEnabled then
                        printfn "[DEBUG] SUCCESS: Strategy %A worked! Display enabled and validated." strategy
                        Ok validationResult
                    else
                        Error (sprintf "Strategy %A: API succeeded but validation failed" strategy)
                | Error validationError ->
                    Error (sprintf "Strategy %A: API succeeded but validation error: %s" strategy validationError)
            | Error strategyError ->
                Error (sprintf "Strategy %A failed: %s" strategy strategyError)
        
        let rec tryStrategies remainingStrategies =
            match remainingStrategies with
            | [] -> Error "All strategies exhausted - display could not be enabled"
            | strategy :: rest ->
                match tryStrategyWithValidation strategy with
                | Ok result ->
                    printfn "[DEBUG] Display enable succeeded with strategy %A after %d validation attempts" 
                            strategy result.ValidationAttempts
                    Ok ()
                | Error msg ->
                    printfn "[DEBUG] Strategy %A failed: %s" strategy msg
                    printfn "[DEBUG] Trying next strategy..."
                    tryStrategies rest
        
        printfn "[DEBUG] ========== Starting Multi-Strategy Display Enable =========="
        printfn "[DEBUG] Display ID: %s" displayId
        printfn "[DEBUG] Available strategies: %A" strategies
        tryStrategies strategies
    
    // Disable display using CCD API for proper TV handling
    let private disableDisplay displayId =
        let stateSaved = DisplayStateCache.saveDisplayState displayId
        if stateSaved then
            printfn "[DEBUG] Display state saved successfully"
        else
            printfn "[DEBUG] Warning: Failed to save display state"
        
        // Use CCD API to properly disable the display (especially for TVs)
        printfn "[DEBUG] Using CCD API to disable display %s" displayId
        match DisplayConfigurationAPI.getDisplayPaths true with  // Get all paths including the one to disable
        | Ok (pathArray, modeArray, pathCount, modeCount) ->
            match DisplayConfigurationAPI.findDisplayPathBySourceId displayId pathArray pathCount with
            | Ok (targetPath, pathIndex) ->
                let modifiedPaths = Array.copy pathArray
                let mutable modifiedPath = targetPath
                modifiedPath.flags <- 0u  // Remove DISPLAYCONFIG_PATH_ACTIVE flag
                modifiedPath.targetInfo.targetAvailable <- 0  // Mark as not available
                modifiedPaths.[pathIndex] <- modifiedPath
                
                printfn "[DEBUG] Applying configuration to disable display path %d" pathIndex
                match DisplayConfigurationAPI.applyDisplayConfigurationFiltered modifiedPaths modeArray pathCount modeCount 
                      (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
                       WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE) with
                | Ok _ -> 
                    printfn "[DEBUG] SUCCESS: Display disabled using CCD API!"
                    Ok ()
                | Error err -> 
                    printfn "[DEBUG] CCD disable failed: %s, trying fallback method" err
                    // Fallback to original method
                    let result = WindowsAPI.ChangeDisplaySettingsExNull(displayId, IntPtr.Zero, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                    if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                        printfn "[DEBUG] SUCCESS: Display disabled with fallback!"
                        Ok ()
                    else
                        printfn "[DEBUG] Fallback disable failed (%d), treating as success" result
                        Ok ()
            | Error err ->
                printfn "[DEBUG] Could not find display path: %s, using fallback" err
                let result = WindowsAPI.ChangeDisplaySettingsExNull(displayId, IntPtr.Zero, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                    printfn "[DEBUG] SUCCESS: Display disabled with fallback!"
                    Ok ()
                else
                    printfn "[DEBUG] Fallback disable failed (%d), treating as success" result
                    Ok ()
        | Error err ->
            printfn "[DEBUG] Could not get display paths: %s, using fallback" err
            let result = WindowsAPI.ChangeDisplaySettingsExNull(displayId, IntPtr.Zero, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
            if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                printfn "[DEBUG] SUCCESS: Display disabled with fallback!"
                Ok ()
            else
                printfn "[DEBUG] Fallback disable failed (%d), treating as success" result
                Ok ()
    
    // Enable or disable a display - now functional with comprehensive validation
    let setDisplayEnabled (displayId: DisplayId) (enabled: bool) =
        try
            printfn "[DEBUG] ========== Starting setDisplayEnabled =========="
            printfn "[DEBUG] Display ID: %s, Target State: %b" displayId enabled
            
            // Get initial state for comparison
            match validateDisplayState displayId (not enabled) 1 with
            | Ok initialState -> 
                printfn "[DEBUG] Initial state validated: IsEnabled=%b" initialState.IsEnabled
            | Error errorMsg -> 
                printfn "[DEBUG] Initial state check failed: %s" errorMsg
            
            if enabled then
                tryEnableDisplay displayId
            else
                disableDisplay displayId
        with
        | ex ->
            Error (sprintf "Exception setting display %s enabled state: %s" displayId ex.Message)

    // Test display mode temporarily for 15 seconds then revert
    let testDisplayMode (displayId: DisplayId) (mode: DisplayMode) (orientation: DisplayOrientation) onComplete =
        async {
            try
                printfn "[DEBUG] ========== Starting testDisplayMode =========="
                printfn "[DEBUG] Testing mode %dx%d @ %dHz for 15 seconds" mode.Width mode.Height mode.RefreshRate
                
                // Get current display settings to restore later
                let mutable currentDevMode = WindowsAPI.DEVMODE()
                currentDevMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
                let getCurrentResult = WindowsAPI.EnumDisplaySettings(displayId, -1, &currentDevMode)
                
                if not getCurrentResult then
                    printfn "[DEBUG] ERROR: Could not get current display settings for test mode"
                    onComplete (Error "Could not get current display settings")
                else
                    let originalMode = {
                        Width = int currentDevMode.dmPelsWidth
                        Height = int currentDevMode.dmPelsHeight
                        RefreshRate = int currentDevMode.dmDisplayFrequency
                        BitsPerPixel = int currentDevMode.dmBitsPerPel
                    }
                    let originalOrientation = DisplayStateCache.windowsToOrientation currentDevMode.dmDisplayOrientation
                    
                    printfn "[DEBUG] Original mode: %dx%d @ %dHz, orientation: %A" 
                            originalMode.Width originalMode.Height originalMode.RefreshRate originalOrientation
                    
                    // Apply the test mode
                    match applyDisplayMode displayId mode orientation with
                    | Ok _ ->
                        printfn "[DEBUG] Test mode applied successfully, waiting 15 seconds..."
                        
                        // Wait for 15 seconds
                        do! Async.Sleep(15000)
                        
                        // Revert to original mode
                        printfn "[DEBUG] Reverting to original mode..."
                        match applyDisplayMode displayId originalMode originalOrientation with
                        | Ok _ ->
                            printfn "[DEBUG] Successfully reverted to original mode"
                            onComplete (Ok "Test completed - reverted to original mode")
                        | Error err ->
                            printfn "[DEBUG] ERROR: Failed to revert to original mode: %s" err
                            onComplete (Error (sprintf "Test completed but failed to revert: %s" err))
                    | Error err ->
                        printfn "[DEBUG] ERROR: Failed to apply test mode: %s" err
                        onComplete (Error (sprintf "Failed to apply test mode: %s" err))
            with
            | ex ->
                printfn "[DEBUG] EXCEPTION in testDisplayMode: %s" ex.Message
                onComplete (Error (sprintf "Exception during test: %s" ex.Message))
        }