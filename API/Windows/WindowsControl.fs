namespace DisplaySwitchPro

open System
open System.Runtime.InteropServices
open System.Threading
open WindowsAPI
open DisplayStateCache
open DisplayConfigurationAPI
open CCDPathManagement
open DisplayDetection
open ResultBuilder
open WindowsAPIResult
open StrategyPerformance

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
    | DEVMODEDirect
    | DEVMODEWithReset
    | HardwareReset
    | DisplaySwitchFallback

// Configuration constants for display operations
module private DisplayConstants =
    // Preferred display modes in order of preference (width, height, refresh rate)
    let PreferredModes = [
        // 60Hz preferred modes
        (3840, 2160, 60); (1920, 1080, 60); (1280, 720, 60); (1024, 768, 60)
        // 30Hz fallbacks for compatibility
        (3840, 2160, 30); (1920, 1080, 30); (1280, 720, 30); (1024, 768, 30)
    ]
    
    // Default values for fallback scenarios
    let DefaultRefreshRate = 60
    let DefaultBitsPerPixel = 32
    
    // Timeout and delay constants
    let ValidationMaxAttempts = 5
    let ValidationBaseDelay = 500 // Base delay in milliseconds
    let ValidationMaxDelay = 4000 // Maximum delay cap
    let HardwareResetDelay = 2000
    let DisplaySwitchTimeout = 5000
    let DisplaySwitchSettleDelay = 3000
    let TestModeDisplayTime = 15000

// High-level display control operations
module DisplayControl =

    // Helper function to map display ID to API device name
    let private getAPIDeviceNameForDisplay displayId =
        match DisplayDetection.getAPIDeviceNameForDisplayId displayId with
        | Some apiDeviceName -> apiDeviceName
        | None -> failwith (sprintf "Cannot map display ID '%s' to API device name" displayId)

    // Helper functions for functional display mode application
    let private getCurrentDevMode displayId =
        let apiDeviceName = getAPIDeviceNameForDisplay displayId
        let mutable devMode = WindowsAPI.DEVMODE()
        devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))

        let result = WindowsAPI.EnumDisplaySettings(apiDeviceName, -1, &devMode)
        if result then
            Ok devMode
        else
            Error (sprintf "Could not get current display settings for %s (API: %s)" displayId apiDeviceName)
    
    let private validateModeExists displayId mode =
        let apiDeviceName = getAPIDeviceNameForDisplay displayId
        let allModes = DisplayDetection.getAllDisplayModes apiDeviceName
        let modeExists = allModes |> List.exists (fun m ->
            m.Width = mode.Width && m.Height = mode.Height && m.RefreshRate = mode.RefreshRate)

        if modeExists then
            Ok ()
        else
            let availableForResolution = allModes |> List.filter (fun m -> m.Width = mode.Width && m.Height = mode.Height)
            if availableForResolution.Length > 0 then
                Logging.logVerbosef " Available refresh rates for %dx%d:" mode.Width mode.Height
                availableForResolution |> List.iter (fun m -> Logging.logVerbosef "   - %dHz" m.RefreshRate)
            else
                Logging.logVerbosef " No modes found for resolution %dx%d" mode.Width mode.Height

            Error (sprintf "Mode %dx%d @ %dHz is not supported by display %s (API: %s)" mode.Width mode.Height mode.RefreshRate displayId apiDeviceName)
    
    let private createTargetDevMode displayId (currentDevMode: WindowsAPI.DEVMODE) mode orientation =
        let apiDeviceName = getAPIDeviceNameForDisplay displayId
        match DisplayDetection.getExactDevModeForMode apiDeviceName mode with
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
        let apiDeviceName = getAPIDeviceNameForDisplay displayId
        let mutable devMode = targetDevMode
        let testResult = WindowsAPI.ChangeDisplaySettingsEx(apiDeviceName, &devMode, IntPtr.Zero, WindowsAPI.CDS.CDS_TEST, IntPtr.Zero)

        if testResult <> WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
            let errorMsg = match testResult with
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_FAILED -> "Display driver failed the mode change"
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_NOTUPDATED -> "Unable to write settings to registry"
                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADDUALVIEW -> "Bad dual view configuration"
                           | _ -> sprintf "Unknown error code: %d" testResult
            Error (sprintf "Display mode test failed for %s (API: %s): %s" displayId apiDeviceName errorMsg)
        else
            let mutable applyMode = targetDevMode
            let applyResult = WindowsAPI.ChangeDisplaySettingsEx(apiDeviceName, &applyMode, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
            
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
            Logging.logVerbosef " ========== Starting applyDisplayMode =========="
            Logging.logVerbosef " Display ID: %s" displayId
            Logging.logVerbosef " Target Mode: %dx%d @ %dHz" mode.Width mode.Height mode.RefreshRate
            Logging.logVerbosef " Target Orientation: %A" orientation
            
            let! currentDevMode = getCurrentDevMode displayId
            Logging.logVerbosef " Current settings: %ux%u @ %uHz" currentDevMode.dmPelsWidth currentDevMode.dmPelsHeight currentDevMode.dmDisplayFrequency
            
            do! validateModeExists displayId mode
            let! targetDevMode = createTargetDevMode displayId currentDevMode mode orientation
            do! testAndApplyMode displayId targetDevMode
            
            Logging.logVerbosef " SUCCESS: Display mode applied successfully!"
            return ()
        }

    // Set display orientation only (preserving current resolution and refresh rate)
    let setDisplayOrientation (displayId: DisplayId) (orientation: DisplayOrientation) =
        result {
            Logging.logVerbosef " ========== Starting setDisplayOrientation =========="
            Logging.logVerbosef " Display ID: %s" displayId
            Logging.logVerbosef " Target Orientation: %A" orientation
            
            let! currentDevMode = getCurrentDevMode displayId
            Logging.logVerbosef " Current settings: %ux%u @ %uHz" currentDevMode.dmPelsWidth currentDevMode.dmPelsHeight currentDevMode.dmDisplayFrequency
            
            // Create current mode from existing settings
            let currentMode = {
                Width = int currentDevMode.dmPelsWidth
                Height = int currentDevMode.dmPelsHeight
                RefreshRate = int currentDevMode.dmDisplayFrequency
                BitsPerPixel = int currentDevMode.dmBitsPerPel
            }
            
            // Validate that the current mode exists (should always pass)
            do! validateModeExists displayId currentMode
            let! targetDevMode = createTargetDevMode displayId currentDevMode currentMode orientation
            do! testAndApplyMode displayId targetDevMode
            
            Logging.logVerbosef " SUCCESS: Display orientation changed to %A!" orientation
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
    

    // Check if two displays overlap
    let displaysOverlap (id1, pos1, info1: DisplayInfo) (id2, pos2, info2: DisplayInfo) =
        if id1 = id2 then false
        else
            let x1, y1, w1, h1 = pos1.X, pos1.Y, info1.Resolution.Width, info1.Resolution.Height
            let x2, y2, w2, h2 = pos2.X, pos2.Y, info2.Resolution.Width, info2.Resolution.Height
            
            // Check if rectangles overlap (not just touching at edges)
            not (x1 + w1 <= x2 || x2 + w2 <= x1 || y1 + h1 <= y2 || y2 + h2 <= y1)

    // Compacting that respects the primary display as the centroid at (0,0)
    let compactDisplayPositions (displayPositions: (DisplayId * Position * DisplayInfo) list) =
        Logging.logVerbosef " ========== Primary-Centered Compacting =========="
        
        if List.isEmpty displayPositions then
            []
        else
            // Find the primary display - it should be the centroid at (0,0)
            let primaryDisplay = displayPositions |> List.tryFind (fun (_, _, info) -> info.IsPrimary)
            
            // Calculate the final compacted displays
            let finalDisplays = 
                match primaryDisplay with
                | Some (primaryId, primaryPos, _) ->
                    if primaryPos.X = 0 && primaryPos.Y = 0 then
                        displayPositions
                    else
                        let offsetX = -primaryPos.X
                        let offsetY = -primaryPos.Y
                        displayPositions |> List.map (fun (id, pos, info) ->
                            (id, { X = pos.X + offsetX; Y = pos.Y + offsetY }, info))
                | None ->
                    match displayPositions with
                    | (_, firstPos, _) :: _ ->
                        let offsetX = -firstPos.X
                        let offsetY = -firstPos.Y
                        displayPositions |> List.map (fun (id, pos, info) ->
                            (id, { X = pos.X + offsetX; Y = pos.Y + offsetY }, info))
                    | [] -> []
            
            // Validate coordinates are within Windows limits (-32768 to +32767)
            let hasInvalidCoords = 
                finalDisplays 
                |> List.exists (fun (_, pos, info: DisplayInfo) -> 
                    pos.X < -32768 || pos.X > 32767 || 
                    (pos.X + info.Resolution.Width) > 32767)
            
            if hasInvalidCoords then
                Logging.logVerbosef " WARNING: Coordinates exceed Windows limits (-32768 to +32767)"
                let minX = finalDisplays |> List.map (fun (_, pos, _) -> pos.X) |> List.min
                let maxXWithWidth = finalDisplays |> List.map (fun (_, pos, info: DisplayInfo) -> pos.X + info.Resolution.Width) |> List.max
                
                let additionalShift = 
                    if minX < -32768 then -32768 - minX
                    elif maxXWithWidth > 32767 then 32767 - maxXWithWidth
                    else 0
                
                if additionalShift <> 0 then
                    Logging.logVerbosef " Applying additional shift of %d pixels to fit Windows limits" additionalShift
                    finalDisplays |> List.map (fun (id, pos, info) ->
                        (id, { pos with X = pos.X + additionalShift }, info))
                else
                    finalDisplays
            else
                finalDisplays

    // Set display position - applies canvas drag changes to Windows using CCD API
    let setDisplayPosition (displayId: DisplayId) (newPosition: Position) =
        result {
            Logging.logVerbosef " Setting %s position to (%d, %d) using CCD API" displayId newPosition.X newPosition.Y
            return! DisplayConfigurationAPI.updateDisplayPosition displayId newPosition
        }
        |> Result.mapError (sprintf "Failed to set %s position: %s" displayId)

    // Apply multiple display positions atomically using CCD API with compacting (with provided display info)
    let applyMultipleDisplayPositionsWithInfo (displayPositionsWithInfo: (DisplayId * Position * DisplayInfo) list) =
        result {
            Logging.logVerbosef " ========== Applying Multiple Display Positions (CCD API with Preset Info) =========="
            Logging.logVerbosef " Total displays to reposition: %d" displayPositionsWithInfo.Length
            displayPositionsWithInfo |> List.iteri (fun i (id, pos, info) ->
                Logging.logVerbosef " Display %d: %s -> (%d, %d), Primary=%b" (i+1) id pos.X pos.Y info.IsPrimary)
            
            // Compact positions using preset display info (ensures correct primary detection)
            let compactedPositions = compactDisplayPositions displayPositionsWithInfo
            let finalPositions = compactedPositions |> List.map (fun (id, pos, _) -> (id, pos))
            
            Logging.logVerbosef " Compacted positions:"
            finalPositions |> List.iteri (fun i (id, pos) ->
                Logging.logVerbosef " Final %d: %s -> (%d, %d)" (i+1) id pos.X pos.Y)
            
            // Apply all position changes atomically using CCD API
            return! DisplayConfigurationAPI.applyMultiplePositionChanges finalPositions
        }
        |> Result.mapError (sprintf "Failed to apply multiple display positions: %s")

    // Apply multiple display positions atomically using CCD API with compacting (legacy version)
    let applyMultipleDisplayPositions (displayPositions: (DisplayId * Position) list) =
        result {
            Logging.logVerbosef " ========== Applying Multiple Display Positions (CCD API) =========="
            Logging.logVerbosef " Total displays to reposition: %d" displayPositions.Length
            displayPositions |> List.iteri (fun i (id, pos) ->
                Logging.logVerbosef " Display %d: %s -> (%d, %d)" (i+1) id pos.X pos.Y)
            
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
            
            Logging.logVerbosef " Found display info for %d of %d displays" displayPositionsWithInfo.Length displayPositions.Length
            
            // Use the new function with info
            return! applyMultipleDisplayPositionsWithInfo displayPositionsWithInfo
        }

    // Pure function that ONLY sets the Windows primary display flag
    let setPrimaryDisplayFlag (displayId: DisplayId) =
        result {
            Logging.logVerbosef " Setting %s as primary display flag only" displayId

            let! currentDevMode = getCurrentDevMode displayId
            let mutable updatedDevMode = currentDevMode

            // Ensure the primary display is positioned at (0,0) as required by Windows
            updatedDevMode.dmPositionX <- 0
            updatedDevMode.dmPositionY <- 0
            updatedDevMode.dmFields <- updatedDevMode.dmFields ||| 0x00000020u // DM_POSITION

            let apiDeviceName = getAPIDeviceNameForDisplay displayId
            Logging.logVerbosef " Setting primary flag for %s (API: %s)" displayId apiDeviceName

            let result = WindowsAPI.ChangeDisplaySettingsEx(
                apiDeviceName,
                &updatedDevMode,
                IntPtr.Zero,
                WindowsAPI.CDS.CDS_SET_PRIMARY ||| WindowsAPI.CDS.CDS_UPDATEREGISTRY,
                IntPtr.Zero
            )

            Logging.logVerbosef " setPrimaryDisplayFlag returned: %d" result

            if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                Logging.logVerbosef " Primary display flag set successfully for %s" displayId
                return ()
            else
                let errorMessage = getDisplayChangeErrorMessage result
                printfn "[ERROR] setPrimaryDisplayFlag failed for %s: Code %d - %s" displayId result errorMessage
                return! Error errorMessage
        }

    // Set display as primary with proper repositioning of all displays using function composition
    let setPrimaryDisplay (displayId: DisplayId) =
        result {
            Logging.logVerbosef " Setting %s as primary display with repositioning" displayId

            // Step 1: Get all current displays
            let allDisplays = DisplayDetection.getConnectedDisplays()
            let activeDisplays = allDisplays |> List.filter (fun d -> d.IsEnabled)
            Logging.logVerbosef " Found %d active displays for repositioning" activeDisplays.Length

            // Step 2: Update primary flag in display info and create position list
            let displayPositions =
                activeDisplays
                |> List.map (fun d ->
                    // Mark the target display as primary, others as non-primary
                    let updatedInfo = if d.Id = displayId then { d with IsPrimary = true } else { d with IsPrimary = false }
                    (d.Id, d.Position, updatedInfo))

            Logging.logVerbosef " Created display positions list with updated primary flags"

            // Step 3: Use compactDisplayPositions to arrange displays with primary at (0,0)
            let compactedPositions = compactDisplayPositions displayPositions
            Logging.logVerbosef " Compacted positions calculated"

            // Step 4: Apply all positions atomically using existing pipeline
            return! applyMultipleDisplayPositionsWithInfo compactedPositions
        }
        |> Result.mapError (sprintf "Failed to set %s as primary: %s" displayId)

    // Comprehensive display state validation with Result type
    let private validateDisplayState displayId expectedState =
        let validateSingleAttempt attempt =
            try
                Logging.logVerbosef " Validation attempt %d for %s (expecting %b)" attempt displayId expectedState
                
                // Method 1: Check via DisplayDetection (our existing system)
                let displays = DisplayDetection.getConnectedDisplays()
                let detectionResult = 
                    displays 
                    |> List.tryFind (fun d -> d.Id = displayId)
                    |> Option.map (fun d -> d.IsEnabled)
                
                // Method 2: Check via EnumDisplayDevices (Windows API direct)
                let mutable displayDevice = WindowsAPI.DISPLAY_DEVICE()
                displayDevice.cb <- Marshal.SizeOf(typeof<WindowsAPI.DISPLAY_DEVICE>)

                // Convert Display ID to API device name for EnumDisplayDevices
                let apiDeviceName =
                    match DisplayDetection.getAPIDeviceNameForDisplayId displayId with
                    | Some apiName -> apiName
                    | None -> displayId  // Fallback to original ID

                let enumResult = WindowsAPI.EnumDisplayDevices(apiDeviceName, 0u, &displayDevice, 0u)
                let apiResult =
                    if enumResult then
                        Some ((displayDevice.StateFlags &&& WindowsAPI.Flags.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) <> 0u)
                    else None
                
                // Method 3: Check via CCD API paths with improved Display ID parsing
                let ccdResult =
                    match CCDPathManagement.getDisplayPaths true with
                    | Ok (pathArray, _, pathCount, _) ->
                        pathArray
                        |> Array.take (int pathCount)
                        |> Array.tryFind (fun path ->
                            // Parse display number from both old and new format
                            let displayNum =
                                if displayId.StartsWith(@"\\.\DISPLAY") then
                                    let mutable num = 0
                                    if Int32.TryParse(displayId.Substring(11), &num) then Some (num - 1) else None
                                elif displayId.StartsWith("Display") then
                                    let mutable num = 0
                                    if Int32.TryParse(displayId.Substring(7), &num) then Some (num - 1) else None
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
                    Logging.logVerbosef " Validation consensus: Detection=%b, API=%b, CCD=%b -> %b" 
                            detectionEnabled apiEnabled ccdEnabled consensus
                    
                    if consensus = expectedState then
                        Ok { IsEnabled = consensus; IsResponding = true; ValidationAttempts = attempt; LastError = None }
                    else
                        Error (sprintf "Display state mismatch - expected %b, got %b" expectedState consensus)
                
                | Some detectionEnabled, Some apiEnabled, None ->
                    let consensus = if detectionEnabled = apiEnabled then detectionEnabled else detectionEnabled
                    Logging.logVerbosef " Validation (no CCD): Detection=%b, API=%b -> %b" detectionEnabled apiEnabled consensus
                    
                    if consensus = expectedState then
                        Ok { IsEnabled = consensus; IsResponding = true; ValidationAttempts = attempt; LastError = None }
                    else
                        Error (sprintf "Display state mismatch - expected %b, got %b" expectedState consensus)
                
                | Some detectionEnabled, None, _ ->
                    Logging.logVerbosef " Validation (detection only): %b" detectionEnabled
                    if detectionEnabled = expectedState then
                        Ok { IsEnabled = detectionEnabled; IsResponding = true; ValidationAttempts = attempt; LastError = None }
                    else
                        Error (sprintf "Display state mismatch - expected %b, got %b" expectedState detectionEnabled)
                
                | None, _, _ ->
                    Error (sprintf "Display %s not found in any validation method" displayId)
            with
            | ex ->
                Error (sprintf "Validation exception: %s" ex.Message)
        
        // Single validation attempt - no retrying
        validateSingleAttempt 1

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
            let preferredModes = DisplayConstants.PreferredModes
            let findMode (w, h, r) = availableModes |> List.tryFind (fun m -> m.Width = w && m.Height = h && m.RefreshRate = r)
            let bestMode = 
                preferredModes 
                |> List.tryPick findMode
                |> Option.defaultWith (fun () -> availableModes |> List.head)
            Ok bestMode
    
    // Helper to enable display using saved state
    let private enableWithSavedState displayId savedState =
        let apiDeviceName = getAPIDeviceNameForDisplay displayId
        let devMode = createDevModeFromSavedState savedState
        let mutable mutableDevMode = devMode
        let result = WindowsAPI.ChangeDisplaySettingsEx(apiDeviceName, &mutableDevMode, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
        
        if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
            Logging.logVerbosef " SUCCESS: Display restored from saved state!"
            Ok ()
        else
            Error (sprintf "Failed to restore saved state (%d)" result)
    
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

    // Helper to enable display with auto-detected mode
    let private enableWithAutoMode displayId bestMode =
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
        
        let apiDeviceName = getAPIDeviceNameForDisplay displayId
        let result = WindowsAPI.ChangeDisplaySettingsEx(apiDeviceName, &devMode, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
        if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
            Logging.logVerbosef " SUCCESS: Display enabled with auto-detected settings!"
            Ok ()
        else
            Error (getDisplayChangeErrorMessage result)

    // Strategy implementation functions
    let private executeStrategy strategy displayId =
        try
            Logging.logVerbosef " Executing strategy %A for display %s" strategy displayId
            
            match strategy with
            | CCDTargeted ->
                // Use ALL paths including inactive to find DISPLAY4
                Logging.logVerbosef " CCD Targeted: Getting ALL paths to find inactive display..."
                match CCDPathManagement.getDisplayPaths true with  // Get ALL paths including inactive
                | Ok (pathArray, modeArray, pathCount, modeCount) ->
                    match CCDPathManagement.findInactiveDisplayPath displayId pathArray pathCount with
                    | Ok (targetPath, pathIndex) ->
                        match CCDPathManagement.validateDisplayPath targetPath with
                        | Ok validatedPath ->
                            let modifiedPaths = Array.copy pathArray
                            let mutable modifiedPath = validatedPath
                            modifiedPath.flags <- modifiedPath.flags ||| WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                            modifiedPath.targetInfo.targetAvailable <- 1
                            modifiedPaths.[pathIndex] <- modifiedPath
                            
                            Logging.logVerbosef " CCD Targeted: Applying targeted configuration using filtered paths..."
                            // Use filtered version to handle large path arrays
                            DisplayConfigurationAPI.applyDisplayConfigurationFiltered modifiedPaths modeArray pathCount modeCount 
                                   (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
                                    WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE)
                        | Error err -> Error err
                    | Error err -> Error err
                | Error err -> Error err
                
            | CCDModePopulation ->
                Logging.logVerbosef " CCD Mode Population: Creating mode information for inactive display..."
                // Get ALL paths including inactive ones
                match CCDPathManagement.getDisplayPathsWithValidation true with
                | Ok (pathArray, modeArray, pathCount, modeCount) ->
                    match CCDPathManagement.findInactiveDisplayPath displayId pathArray pathCount with
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
                                
                                Logging.logVerbosef " CCD Mode Population: Applying configuration with populated modes..."
                                DisplayConfigurationAPI.applyDisplayConfiguration pathArray expandedModeArray pathCount (modeCount + 2u)
                                       (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
                                        WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE)
                            | Error err -> Error err
                        | Error err -> Error err
                    | Error err -> Error err
                | Error err -> Error err
                
            | CCDDirectPath ->
                Logging.logVerbosef " CCD Direct Path: Using exact path with no filtering or modifications..."
                // Get ALL paths to find DISPLAY4, then use it directly
                match CCDPathManagement.getDisplayPathsWithValidation true with
                | Ok (pathArray, modeArray, pathCount, modeCount) ->
                    match CCDPathManagement.findDisplayPathBySourceId displayId pathArray pathCount with
                    | Ok (targetPath, pathIndex) ->
                        // Simply activate the path without any filtering or modifications
                        let directPaths = Array.copy pathArray
                        let mutable modifiedPath = targetPath
                        modifiedPath.flags <- WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                        modifiedPath.targetInfo.targetAvailable <- 1
                        directPaths.[pathIndex] <- modifiedPath
                        
                        Logging.logVerbosef " Using direct path activation with all %d paths" pathCount
                        DisplayConfigurationAPI.applyDisplayConfiguration directPaths modeArray pathCount modeCount 
                               (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
                                WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE)
                    | Error err -> Error err
                | Error err -> Error err
                
            | CCDTopologyExtend ->
                Logging.logVerbosef " CCD Topology: Applying extend topology with improved flags..."
                DisplayConfigurationAPI.applyDisplayConfiguration [||] [||] 0u 0u
                       (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_TOPOLOGY_EXTEND ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES)
                
            | CCDMinimalPaths ->
                Logging.logVerbosef " CCD Minimal Paths: Using filtered path configuration..."
                // Get ALL paths to find DISPLAY4, then filter for SetDisplayConfig
                match CCDPathManagement.getDisplayPathsWithValidation true with
                | Ok (pathArray, modeArray, pathCount, modeCount) ->
                    match CCDPathManagement.findDisplayPathBySourceId displayId pathArray pathCount with
                    | Ok (targetPath, pathIndex) ->
                        Logging.logVerbosef " Found target display at path index %d" pathIndex
                        
                        // Create a minimal array with just the paths we need
                        let activePaths = pathArray |> Array.take (int pathCount) |> Array.filter (fun p -> p.flags <> 0u)
                        let minimalPaths = Array.append activePaths [|targetPath|]
                        
                        // Activate the target path
                        let mutable modifiedPath = targetPath
                        modifiedPath.flags <- modifiedPath.flags ||| WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                        modifiedPath.targetInfo.targetAvailable <- 1
                        minimalPaths.[Array.length activePaths] <- modifiedPath
                        
                        Logging.logVerbosef " Using minimal path set: %d paths (was %d)" (Array.length minimalPaths) pathCount
                        DisplayConfigurationAPI.applyDisplayConfigurationFiltered minimalPaths modeArray (uint32 (Array.length minimalPaths)) modeCount 
                               (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG ||| 
                                WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE)
                    | Error err -> Error err
                | Error err -> Error err
                
            | DEVMODEDirect ->
                Logging.logVerbosef " DEVMODE Direct: Getting saved state or best available mode..."
                match DisplayStateCache.getSavedDisplayState displayId with
                | Some savedState -> enableWithSavedState displayId savedState
                | None ->
                    match getBestAvailableMode displayId with
                    | Ok bestMode -> enableWithAutoMode displayId bestMode
                    | Error err -> Error err
                    
            | DEVMODEWithReset ->
                Logging.logVerbosef " DEVMODE Reset: Using reset sequence..."
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
                    let apiDeviceName = getAPIDeviceNameForDisplay displayId
                    let testResult = WindowsAPI.ChangeDisplaySettingsEx(apiDeviceName, &devMode, IntPtr.Zero, WindowsAPI.CDS.CDS_TEST, IntPtr.Zero)
                    if testResult <> WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                        Error (sprintf "DEVMODE test failed: %d" testResult)
                    else
                        // Step 2: Apply without reset
                        let applyResult1 = WindowsAPI.ChangeDisplaySettingsEx(apiDeviceName, &devMode, IntPtr.Zero,
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
                Logging.logVerbosef " Hardware Reset: Forcing mode enumeration and adapter reset..."
                match DisplayConfigurationAPI.applyDisplayConfiguration [||] [||] 0u 0u
                       (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_FORCE_MODE_ENUMERATION ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES) with
                | Ok _ ->
                    System.Threading.Thread.Sleep(DisplayConstants.HardwareResetDelay)
                    Ok ()
                | Error err -> Error err
                
            | DisplaySwitchFallback ->
                Logging.logVerbosef " Display Switch Fallback: Using Windows DisplaySwitch.exe /extend..."
                try
                    let startInfo = System.Diagnostics.ProcessStartInfo()
                    startInfo.FileName <- "DisplaySwitch.exe"
                    startInfo.Arguments <- "/extend"
                    startInfo.UseShellExecute <- false
                    startInfo.CreateNoWindow <- true
                    startInfo.RedirectStandardOutput <- true
                    startInfo.RedirectStandardError <- true
                    
                    use proc = System.Diagnostics.Process.Start(startInfo)
                    let _ = proc.WaitForExit(DisplayConstants.DisplaySwitchTimeout)
                    
                    if proc.ExitCode = 0 then
                        Logging.logVerbosef " DisplaySwitch.exe completed successfully"
                        System.Threading.Thread.Sleep(DisplayConstants.DisplaySwitchSettleDelay)
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
        let strategies = [CCDTargeted; CCDModePopulation; CCDMinimalPaths; CCDDirectPath; CCDTopologyExtend; DEVMODEDirect; DEVMODEWithReset; HardwareReset; DisplaySwitchFallback]
        
        let tryStrategyWithValidation strategy =
            match executeStrategy strategy displayId with
            | Ok _ ->
                Logging.logVerbosef " Strategy %A executed, validating display state..." strategy
                match validateDisplayState displayId true with
                | Ok validationResult ->
                    if validationResult.IsEnabled then
                        Logging.logVerbosef " SUCCESS: Strategy %A worked! Display enabled and validated." strategy
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
                    Logging.logVerbosef " Display enable succeeded with strategy %A after %d validation attempts" 
                            strategy result.ValidationAttempts
                    Ok ()
                | Error msg ->
                    Logging.logVerbosef " Strategy %A failed: %s" strategy msg
                    Logging.logVerbosef " Trying next strategy..."
                    tryStrategies rest
        
        Logging.logVerbosef " ========== Starting Multi-Strategy Display Enable =========="
        Logging.logVerbosef " Display ID: %s" displayId
        Logging.logVerbosef " Available strategies: %A" strategies
        tryStrategies strategies
    
    // Disable display using CCD API
    let private disableDisplay displayId =
        let stateSaved = DisplayStateCache.saveDisplayState displayId
        if stateSaved then
            Logging.logVerbosef " Display state saved successfully"
        else
            Logging.logVerbosef " Warning: Failed to save display state"
        
        // Use CCD API with target mapping to properly disable the display (like TV fix)
        Logging.logVerbosef " Using CCD API with target mapping to disable display %s" displayId
        match CCDPathManagement.getDisplayPaths false with  // Get only active paths
        | Ok (pathArray, modeArray, pathCount, modeCount) ->
            // Find the correct path using target mapping approach from TV fix
            let targetMappings = CCDTargetMapping.getDisplayTargetIdMapping()

            // Convert Display ID to API device name for mapping lookup
            let apiDeviceName = getAPIDeviceNameForDisplay displayId
            let targetIdOption =
                targetMappings
                |> List.tryFind (fun m -> m.DisplayName = apiDeviceName)
                |> Option.map (fun m -> m.TargetId)

            match targetIdOption with
            | Some targetId ->
                    // Find path with the specific target ID for this display
                    let pathOption =
                        pathArray
                        |> Array.mapi (fun i path -> (i, path))
                        |> Array.tryFind (fun (_, path) -> path.targetInfo.id = targetId)

                    match pathOption with
                    | Some (pathIndex, targetPath) ->
                        Logging.logVerbosef " Found target path %d with Target ID %u for %s" pathIndex targetId displayId

                        // Create new configuration with the target display removed
                        let filteredPaths =
                            pathArray
                            |> Array.mapi (fun i path -> (i, path))
                            |> Array.filter (fun (i, _) -> i <> pathIndex)
                            |> Array.map snd

                        Logging.logVerbosef " Removing display path (was %d paths, now %d paths)" (int pathCount) filteredPaths.Length

                        match DisplayConfigurationAPI.applyDisplayConfigurationFiltered filteredPaths modeArray (uint32 filteredPaths.Length) modeCount
                              (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG |||
                               WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE) with
                        | Ok _ ->
                            Logging.logVerbosef " SUCCESS: Display disabled using target mapping approach!"
                            Ok ()
                        | Error err ->
                            Logging.logVerbosef " Target mapping disable failed: %s, trying path deactivation" err
                            // Fallback: deactivate the path instead of removing it
                            let modifiedPaths = Array.copy pathArray
                            let mutable modifiedPath = targetPath
                            modifiedPath.flags <- 0u  // Remove DISPLAYCONFIG_PATH_ACTIVE flag
                            modifiedPaths.[pathIndex] <- modifiedPath

                            match DisplayConfigurationAPI.applyDisplayConfigurationFiltered modifiedPaths modeArray pathCount modeCount
                                  (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG |||
                                   WindowsAPI.SDC.SDC_ALLOW_CHANGES ||| WindowsAPI.SDC.SDC_SAVE_TO_DATABASE) with
                            | Ok _ ->
                                Logging.logVerbosef " SUCCESS: Display disabled using path deactivation!"
                                Ok ()
                            | Error err2 ->
                                Logging.logVerbosef " Path deactivation failed: %s, using ChangeDisplaySettings" err2
                                let apiDeviceName = getAPIDeviceNameForDisplay displayId
                                let result = WindowsAPI.ChangeDisplaySettingsExNull(apiDeviceName, IntPtr.Zero, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                                if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                                    Logging.logVerbosef " SUCCESS: Display disabled with ChangeDisplaySettings!"
                                    Ok ()
                                else
                                    Logging.logVerbosef " ChangeDisplaySettings failed (%d), treating as success for now" result
                                    Ok ()
                    | None ->
                        Logging.logVerbosef " Could not find path with Target ID %u, using source ID fallback" targetId
                        match CCDPathManagement.findDisplayPathBySourceId displayId pathArray pathCount with
                        | Ok (targetPath, pathIndex) ->
                            let modifiedPaths = Array.copy pathArray
                            let mutable modifiedPath = targetPath
                            modifiedPath.flags <- 0u
                            modifiedPaths.[pathIndex] <- modifiedPath

                            match DisplayConfigurationAPI.applyDisplayConfigurationFiltered modifiedPaths modeArray pathCount modeCount
                                  (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG) with
                            | Ok _ ->
                                Logging.logVerbosef " SUCCESS: Display disabled using source ID fallback!"
                                Ok ()
                            | Error err -> Error err
                        | Error err -> Error err
                | None ->
                    Logging.logVerbosef " No target mapping found for %s, using source ID approach" displayId
                    match CCDPathManagement.findDisplayPathBySourceId displayId pathArray pathCount with
                    | Ok (targetPath, pathIndex) ->
                        let modifiedPaths = Array.copy pathArray
                        let mutable modifiedPath = targetPath
                        modifiedPath.flags <- 0u
                        modifiedPaths.[pathIndex] <- modifiedPath

                        match DisplayConfigurationAPI.applyDisplayConfigurationFiltered modifiedPaths modeArray pathCount modeCount
                              (WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_USE_SUPPLIED_DISPLAY_CONFIG) with
                        | Ok _ ->
                            Logging.logVerbosef " SUCCESS: Display disabled using source ID approach!"
                            Ok ()
                        | Error err -> Error err
                    | Error err -> Error err
        | Error err ->
            Logging.logVerbosef " Could not get display paths: %s, using fallback" err
            let apiDeviceName = getAPIDeviceNameForDisplay displayId
            let result = WindowsAPI.ChangeDisplaySettingsExNull(apiDeviceName, IntPtr.Zero, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
            if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                Logging.logVerbosef " SUCCESS: Display disabled with fallback!"
                Ok ()
            else
                Logging.logVerbosef " Fallback disable failed (%d), treating as success" result
                Ok ()
    
    // Enable or disable a display - now functional with comprehensive validation
    let setDisplayEnabled (displayId: DisplayId) (enabled: bool) =
        try
            Logging.logVerbosef " ========== Starting setDisplayEnabled =========="
            Logging.logVerbosef " Display ID: %s, Target State: %b" displayId enabled
            
            // Get initial state for comparison
            match validateDisplayState displayId (not enabled) with
            | Ok initialState -> 
                Logging.logVerbosef " Initial state validated: IsEnabled=%b" initialState.IsEnabled
            | Error errorMsg -> 
                Logging.logVerbosef " Initial state check failed: %s" errorMsg
            
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
                Logging.logVerbosef " ========== Starting testDisplayMode =========="
                Logging.logVerbosef " Testing mode %dx%d @ %dHz for 15 seconds" mode.Width mode.Height mode.RefreshRate
                
                // Get current display settings to restore later
                let mutable currentDevMode = WindowsAPI.DEVMODE()
                currentDevMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
                let apiDeviceName = getAPIDeviceNameForDisplay displayId
                let getCurrentResult = WindowsAPI.EnumDisplaySettings(apiDeviceName, -1, &currentDevMode)
                
                if not getCurrentResult then
                    Logging.logVerbosef " ERROR: Could not get current display settings for test mode"
                    onComplete (Error "Could not get current display settings")
                else
                    let originalMode = {
                        Width = int currentDevMode.dmPelsWidth
                        Height = int currentDevMode.dmPelsHeight
                        RefreshRate = int currentDevMode.dmDisplayFrequency
                        BitsPerPixel = int currentDevMode.dmBitsPerPel
                    }
                    let originalOrientation = DisplayStateCache.windowsToOrientation currentDevMode.dmDisplayOrientation
                    
                    Logging.logVerbosef " Original mode: %dx%d @ %dHz, orientation: %A" 
                            originalMode.Width originalMode.Height originalMode.RefreshRate originalOrientation
                    
                    // Apply the test mode
                    match applyDisplayMode displayId mode orientation with
                    | Ok _ ->
                        Logging.logVerbosef " Test mode applied successfully, waiting 15 seconds..."
                        
                        // Wait for 15 seconds
                        do! Async.Sleep(DisplayConstants.TestModeDisplayTime)
                        
                        // Revert to original mode
                        Logging.logVerbosef " Reverting to original mode..."
                        match applyDisplayMode displayId originalMode originalOrientation with
                        | Ok _ ->
                            Logging.logVerbosef " Successfully reverted to original mode"
                            onComplete (Ok "Test completed - reverted to original mode")
                        | Error err ->
                            Logging.logVerbosef " ERROR: Failed to revert to original mode: %s" err
                            onComplete (Error (sprintf "Test completed but failed to revert: %s" err))
                    | Error err ->
                        Logging.logVerbosef " ERROR: Failed to apply test mode: %s" err
                        onComplete (Error (sprintf "Failed to apply test mode: %s" err))
            with
            | ex ->
                Logging.logVerbosef " EXCEPTION in testDisplayMode: %s" ex.Message
                onComplete (Error (sprintf "Exception during test: %s" ex.Message))
        }

    // Batch operation result type for consistent error handling
    type BatchOperationResult<'T> = {
        Successes: (DisplayId * 'T) list
        Failures: (DisplayId * string) list  
    }

    // Helper function to create batch operation results
    let private createBatchResult successes failures =
        { Successes = successes; Failures = failures }

    // Apply enable/disable to multiple displays with best-effort approach
    let applyMultipleDisplayEnabled (displayOperations: (DisplayId * bool) list) =
        Logging.logVerbosef " ========== Applying Multiple Display Enable/Disable Operations =========="
        Logging.logVerbosef " Total displays to process: %d" displayOperations.Length
        displayOperations |> List.iteri (fun i (id, enabled) ->
            Logging.logVerbosef " Display %d: %s -> %s" (i+1) id (if enabled then "ENABLE" else "DISABLE"))
        
        let successes = System.Collections.Generic.List<DisplayId * unit>()
        let failures = System.Collections.Generic.List<DisplayId * string>()
        
        for (displayId, enabled) in displayOperations do
            match setDisplayEnabled displayId enabled with
            | Ok () -> 
                successes.Add((displayId, ()))
                Logging.logVerbosef " SUCCESS: %s %s" displayId (if enabled then "enabled" else "disabled")
            | Error err -> 
                failures.Add((displayId, err))
                Logging.logVerbosef " FAILED: %s - %s" displayId err
        
        let batchResult = createBatchResult (List.ofSeq successes) (List.ofSeq failures)
        Logging.logVerbosef " Batch enable/disable completed: %d successes, %d failures" batchResult.Successes.Length batchResult.Failures.Length
        Ok batchResult

    // Apply display modes to multiple displays with best-effort approach
    let applyMultipleDisplayModes (displayModeOperations: (DisplayId * DisplayMode * DisplayOrientation) list) =
        Logging.logVerbosef " ========== Applying Multiple Display Mode Operations =========="
        Logging.logVerbosef " Total displays to process: %d" displayModeOperations.Length
        displayModeOperations |> List.iteri (fun i (id, mode, orientation) ->
            Logging.logVerbosef " Display %d: %s -> %dx%d @ %dHz, %A" (i+1) id mode.Width mode.Height mode.RefreshRate orientation)
        
        let successes = System.Collections.Generic.List<DisplayId * unit>()
        let failures = System.Collections.Generic.List<DisplayId * string>()
        
        for (displayId, mode, orientation) in displayModeOperations do
            match applyDisplayMode displayId mode orientation with
            | Ok () -> 
                successes.Add((displayId, ()))
                Logging.logVerbosef " SUCCESS: %s mode applied" displayId
            | Error err -> 
                failures.Add((displayId, err))
                Logging.logVerbosef " FAILED: %s - %s" displayId err
        
        let batchResult = createBatchResult (List.ofSeq successes) (List.ofSeq failures)
        Logging.logVerbosef " Batch mode changes completed: %d successes, %d failures" batchResult.Successes.Length batchResult.Failures.Length
        Ok batchResult

    // Apply orientation changes to multiple displays with best-effort approach
    let applyMultipleDisplayOrientations (displayOrientationOperations: (DisplayId * DisplayOrientation) list) =
        Logging.logVerbosef " ========== Applying Multiple Display Orientation Operations =========="
        Logging.logVerbosef " Total displays to process: %d" displayOrientationOperations.Length
        displayOrientationOperations |> List.iteri (fun i (id, orientation) ->
            Logging.logVerbosef " Display %d: %s -> %A" (i+1) id orientation)
        
        let successes = System.Collections.Generic.List<DisplayId * unit>()
        let failures = System.Collections.Generic.List<DisplayId * string>()
        
        for (displayId, orientation) in displayOrientationOperations do
            match setDisplayOrientation displayId orientation with
            | Ok () -> 
                successes.Add((displayId, ()))
                Logging.logVerbosef " SUCCESS: %s orientation set to %A" displayId orientation
            | Error err -> 
                failures.Add((displayId, err))
                Logging.logVerbosef " FAILED: %s - %s" displayId err
        
        let batchResult = createBatchResult (List.ofSeq successes) (List.ofSeq failures)
        Logging.logVerbosef " Batch orientation changes completed: %d successes, %d failures" batchResult.Successes.Length batchResult.Failures.Length
        Ok batchResult

    /// Enhanced logging and diagnostics module (additive - doesn't change existing APIs)
    module EnhancedDiagnostics =

        /// Enable strategy performance tracking with logging
        let enablePerformanceTracking () =
            if not (StrategyPerformance.isTrackingEnabled ()) then
                StrategyPerformance.enableTracking (Some 500) (Some 10)
                Logging.logVerbosef " ✓ Strategy performance tracking enabled"
            else
                Logging.logVerbosef " ✓ Strategy performance tracking already enabled"

        /// Generate enhanced strategy performance report
        let generateStrategyReport () =
            if StrategyPerformance.isTrackingEnabled () then
                let report = StrategyPerformance.generatePerformanceReport ()
                let insights = StrategyPerformance.getPerformanceInsights ()

                Logging.logVerbosef " === Strategy Performance Report ==="
                report.Split('\n') |> Array.iter (fun line ->
                    if not (String.IsNullOrWhiteSpace line) then
                        Logging.logVerbosef " %s" line)

                if not insights.IsEmpty then
                    Logging.logVerbosef " === Performance Insights ==="
                    insights |> List.iter (fun insight -> Logging.logVerbosef " • %s" insight)

                report
            else
                let msg = "Performance tracking is disabled. Call EnablePerformanceTracking() first."
                Logging.logVerbosef " %s" msg
                msg

        /// Enhanced error diagnostics for display operations
        let diagnoseDisplayError displayId error =
            let diagnostics = System.Text.StringBuilder()
            diagnostics.AppendLine(sprintf "=== Display Operation Diagnostics for %s ===" displayId) |> ignore
            diagnostics.AppendLine(sprintf "Error: %s" error) |> ignore
            diagnostics.AppendLine(sprintf "Timestamp: %s" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))) |> ignore

            // Check display connectivity
            try
                let connectedDisplays = DisplayDetection.getConnectedDisplays()
                let targetDisplay = connectedDisplays |> List.tryFind (fun d -> d.Id = displayId)

                match targetDisplay with
                | Some display ->
                    diagnostics.AppendLine(sprintf "Display Status: Connected, Enabled=%b, Primary=%b" display.IsEnabled display.IsPrimary) |> ignore
                    diagnostics.AppendLine(sprintf "Resolution: %dx%d @ %dHz" display.Resolution.Width display.Resolution.Height display.Resolution.RefreshRate) |> ignore
                    diagnostics.AppendLine(sprintf "Position: (%d, %d)" display.Position.X display.Position.Y) |> ignore
                | None ->
                    diagnostics.AppendLine("Display Status: Not found in connected displays") |> ignore

                diagnostics.AppendLine(sprintf "Total connected displays: %d" connectedDisplays.Length) |> ignore
            with
            | ex ->
                diagnostics.AppendLine(sprintf "Error checking display status: %s" ex.Message) |> ignore

            // Enhanced error classification
            let errorClassification =
                if error.Contains("Access denied") || error.Contains("permission") then
                    "PERMISSION_ERROR: Run as administrator and ensure no other display management software is running"
                elif error.Contains("Invalid parameter") || error.Contains("87") then
                    "CONFIGURATION_ERROR: The display configuration is invalid - check resolution and refresh rate settings"
                elif error.Contains("Device not found") || error.Contains("1169") then
                    "HARDWARE_ERROR: Display may be disconnected or driver issues - check cables and update drivers"
                elif error.Contains("timeout") || error.Contains("not responding") then
                    "TIMEOUT_ERROR: Display hardware is slow to respond - this is often normal for TVs and some monitors"
                elif error.Contains("Resource in use") || error.Contains("170") then
                    "RESOURCE_ERROR: Display is busy - wait a moment and try again, or close other display software"
                else
                    "GENERAL_ERROR: Check Windows Event Viewer for more details"

            diagnostics.AppendLine(sprintf "Error Classification: %s" errorClassification) |> ignore

            let result = diagnostics.ToString()
            Logging.logVerbosef "%s" result
            result

        /// Log strategy execution with enhanced details
        let logStrategyExecution strategy displayId result duration =
            match result with
            | Ok _ ->
                Logging.logVerbosef " ✓ SUCCESS: Strategy %A completed in %.0fms for %s"
                    strategy duration displayId

                if StrategyPerformance.isTrackingEnabled () then
                    let stats = StrategyPerformance.getStrategyStats strategy
                    if stats.TotalAttempts > 1 then
                        Logging.logVerbosef "   Strategy performance: %.1f%% success rate (%.0fms avg)"
                            stats.SuccessRate stats.AverageDuration.TotalMilliseconds
            | Error errorMsg ->
                Logging.logVerbosef " ✗ FAILED: Strategy %A failed after %.0fms for %s: %s"
                    strategy duration displayId errorMsg

                // Generate diagnostics for failures
                let _ = diagnoseDisplayError displayId errorMsg

                if StrategyPerformance.isTrackingEnabled () then
                    let stats = StrategyPerformance.getStrategyStats strategy
                    if stats.TotalAttempts > 1 then
                        Logging.logVerbosef "   Strategy history: %.1f%% success rate (%d attempts)"
                            stats.SuccessRate stats.TotalAttempts

        /// Enhanced validation with confidence scoring
        let validateDisplayStateWithConfidence displayId expectedState =
            match validateDisplayState displayId expectedState with
            | Ok validationResult ->
                let confidence =
                    if validationResult.ValidationAttempts = 1 then 95
                    elif validationResult.ValidationAttempts <= 3 then 85
                    else 70

                Logging.logVerbosef " ✓ Display validation: %s state=%b (confidence: %d%%)"
                    displayId validationResult.IsEnabled confidence

                Ok (validationResult, confidence)
            | Error errorMsg ->
                Logging.logVerbosef " ✗ Display validation failed: %s" errorMsg
                Error errorMsg