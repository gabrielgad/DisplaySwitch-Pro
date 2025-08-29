namespace DisplaySwitchPro

open System
open System.Runtime.InteropServices
open System.Threading
open WindowsAPI
open DisplayStateCache
open DisplayConfigurationAPI
open DisplayDetection

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
        | x when x = WindowsAPI.DISP.DISP_CHANGE_FAILED -> "Failed to set as primary"
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

    // Diagnostic function to check display state - now functional
    let private checkDisplayState displayId description =
        let displays = DisplayDetection.getConnectedDisplays()
        let targetDisplay = displays |> List.tryFind (fun d -> d.Id = displayId)
        
        match targetDisplay with
        | Some display ->
            printfn "[DEBUG] === %s ===" description
            printfn "[DEBUG] %s - IsEnabled: %b, Name: %s" displayId display.IsEnabled display.Name
        | None ->
            printfn "[DEBUG] === %s ===" description
            printfn "[DEBUG] %s - Display not found in detection results" displayId

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
    
    // Enable display using multiple strategies
    let private tryEnableDisplay displayId =
        // Strategy 1: Targeted configuration
        let targetedResult = 
            match DisplayConfigurationAPI.getDisplayPaths true with
            | Ok (pathArray, modeArray, pathCount, modeCount) ->
                match DisplayConfigurationAPI.findDisplayPath displayId pathArray (int pathCount) with
                | Ok (targetPath, pathIndex) ->
                    let modifiedPaths = Array.copy pathArray
                    let mutable modifiedPath = targetPath
                    modifiedPath.flags <- modifiedPath.flags ||| WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                    modifiedPath.targetInfo.targetAvailable <- 1
                    modifiedPaths.[pathIndex] <- modifiedPath
                    
                    let applyResult = WindowsAPI.SetDisplayConfig(pathCount, modifiedPaths, modeCount, modeArray, 
                                                                WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES)
                    if applyResult = WindowsAPI.ERROR.ERROR_SUCCESS then
                        checkDisplayState displayId "AFTER TARGETED CONFIGURATION"
                        System.Threading.Thread.Sleep(1000)
                        Ok ()
                    else
                        Error (sprintf "Targeted configuration failed: %d" applyResult)
                | Error err -> Error err
            | Error err -> Error err
        
        match targetedResult with
        | Ok () -> Ok ()
        | Error _ ->
            // Strategy 2: Topology extend
            let topologyResult = WindowsAPI.SetDisplayConfig(0u, null, 0u, null, WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_TOPOLOGY_EXTEND)
            if topologyResult = WindowsAPI.ERROR.ERROR_SUCCESS then
                printfn "[DEBUG] SUCCESS: Topology extend succeeded!"
                checkDisplayState displayId "AFTER TOPOLOGY EXTEND"
                Ok ()
            else
                // Strategy 3: Force enumeration
                let forceEnumResult = WindowsAPI.SetDisplayConfig(0u, null, 0u, null, 
                                                      WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_FORCE_MODE_ENUMERATION ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES)
                if forceEnumResult = WindowsAPI.ERROR.ERROR_SUCCESS then
                    printfn "[DEBUG] SUCCESS: Force enumeration succeeded!"
                    Ok ()
                else
                    // Strategy 4: Manual enable
                    match DisplayStateCache.getSavedDisplayState displayId with
                    | Some savedState -> enableWithSavedState displayId savedState
                    | None ->
                        match getBestAvailableMode displayId with
                        | Ok bestMode -> enableWithAutoMode displayId bestMode
                        | Error err -> Error err
    
    // Disable display
    let private disableDisplay displayId =
        let stateSaved = DisplayStateCache.saveDisplayState displayId
        if stateSaved then
            printfn "[DEBUG] Display state saved successfully"
        else
            printfn "[DEBUG] Warning: Failed to save display state"
        
        let result = WindowsAPI.ChangeDisplaySettingsExNull(displayId, IntPtr.Zero, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
        if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
            printfn "[DEBUG] SUCCESS: Display disabled!"
            Ok ()
        else
            printfn "[DEBUG] Failed to disable (%d), treating as success" result
            Ok ()
    
    // Enable or disable a display - now functional
    let setDisplayEnabled (displayId: DisplayId) (enabled: bool) =
        try
            printfn "[DEBUG] ========== Starting setDisplayEnabled =========="
            printfn "[DEBUG] Display ID: %s, Target State: %b" displayId enabled
            
            checkDisplayState displayId "BEFORE API CALLS"
            
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