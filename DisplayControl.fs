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
    
    // Apply display mode changes (resolution, refresh rate, orientation)
    let applyDisplayMode (displayId: DisplayId) (mode: DisplayMode) (orientation: DisplayOrientation) =
        try
            printfn "[DEBUG] ========== Starting applyDisplayMode =========="
            printfn "[DEBUG] Display ID: %s" displayId
            printfn "[DEBUG] Target Mode: %dx%d @ %dHz (BitsPerPixel: %d)" mode.Width mode.Height mode.RefreshRate mode.BitsPerPixel
            printfn "[DEBUG] Target Orientation: %A (Windows value: %u)" orientation (DisplayStateCache.orientationToWindows orientation)

            // Get current display settings to use as base
            let mutable devMode = WindowsAPI.DEVMODE()
            devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
            printfn "[DEBUG] DEVMODE structure size: %d bytes" devMode.dmSize
            
            let getCurrentResult = WindowsAPI.EnumDisplaySettings(displayId, -1, &devMode)
            if not getCurrentResult then
                printfn "[DEBUG] ERROR: EnumDisplaySettings failed for %s" displayId
                Error (sprintf "Could not get current display settings for %s" displayId)
            else
                printfn "[DEBUG] Current display settings retrieved successfully:"
                printfn "[DEBUG]   Current Resolution: %ux%u @ %uHz" devMode.dmPelsWidth devMode.dmPelsHeight devMode.dmDisplayFrequency
                printfn "[DEBUG]   Current BitsPerPixel: %u" devMode.dmBitsPerPel
                printfn "[DEBUG]   Current Orientation: %u" devMode.dmDisplayOrientation
                printfn "[DEBUG]   Current Position: (%d, %d)" devMode.dmPositionX devMode.dmPositionY
                printfn "[DEBUG]   Current dmFields: 0x%08X" devMode.dmFields
                
                // Store original values for comparison
                let originalWidth = devMode.dmPelsWidth
                let originalHeight = devMode.dmPelsHeight
                let originalFreq = devMode.dmDisplayFrequency
                let originalOrientation = devMode.dmDisplayOrientation
                
                // Update the settings we want to change
                devMode.dmPelsWidth <- uint32 mode.Width
                devMode.dmPelsHeight <- uint32 mode.Height  
                devMode.dmDisplayFrequency <- uint32 mode.RefreshRate
                devMode.dmBitsPerPel <- uint32 mode.BitsPerPixel
                devMode.dmDisplayOrientation <- DisplayStateCache.orientationToWindows orientation
                
                // Try two approaches based on whether we're changing resolution
                let isResolutionChange = originalWidth <> uint32 mode.Width || originalHeight <> uint32 mode.Height
                
                let dmFields = 
                    if isResolutionChange then
                        // For resolution changes, use only the essential fields
                        printfn "[DEBUG] Resolution change detected - using minimal fields"
                        0x00080000u ||| 0x00040000u ||| 0x00020000u ||| 0x00400000u
                        // DM_BITSPERPEL | DM_PELSHEIGHT | DM_PELSWIDTH | DM_DISPLAYFREQUENCY
                    else
                        // For refresh rate only changes, preserve original fields
                        printfn "[DEBUG] Refresh rate only change - preserving original fields"
                        devMode.dmFields ||| 0x00400000u ||| 0x00000080u
                        // Original fields | DM_DISPLAYFREQUENCY | DM_DISPLAYORIENTATION
                
                devMode.dmFields <- dmFields
                
                printfn "[DEBUG] Updated DEVMODE values:"
                printfn "[DEBUG]   New Resolution: %ux%u @ %uHz" devMode.dmPelsWidth devMode.dmPelsHeight devMode.dmDisplayFrequency
                printfn "[DEBUG]   New BitsPerPixel: %u" devMode.dmBitsPerPel
                printfn "[DEBUG]   New Orientation: %u" devMode.dmDisplayOrientation
                printfn "[DEBUG]   New dmFields: 0x%08X" devMode.dmFields
                printfn "[DEBUG]   Fields breakdown:"
                printfn "[DEBUG]     DM_BITSPERPEL (0x00080000): %b" (dmFields &&& 0x00080000u <> 0u)
                printfn "[DEBUG]     DM_PELSHEIGHT (0x00040000): %b" (dmFields &&& 0x00040000u <> 0u)
                printfn "[DEBUG]     DM_PELSWIDTH (0x00020000): %b" (dmFields &&& 0x00020000u <> 0u)
                printfn "[DEBUG]     DM_DISPLAYFREQUENCY (0x00400000): %b" (dmFields &&& 0x00400000u <> 0u)
                printfn "[DEBUG]     DM_DISPLAYORIENTATION (0x00000080): %b" (dmFields &&& 0x00000080u <> 0u)

                // Before testing, verify this mode exists in our enumerated modes
                printfn "[DEBUG] Verifying mode exists in enumerated modes..."
                let allModes = DisplayDetection.getAllDisplayModes displayId
                let modeExists = allModes |> List.exists (fun m -> 
                    m.Width = mode.Width && m.Height = mode.Height && m.RefreshRate = mode.RefreshRate)
                printfn "[DEBUG] Mode %dx%d @ %dHz exists in enumerated modes: %b" mode.Width mode.Height mode.RefreshRate modeExists
                
                if not modeExists then
                    // Log available modes for this resolution
                    let availableForResolution = allModes |> List.filter (fun m -> m.Width = mode.Width && m.Height = mode.Height)
                    if availableForResolution.Length > 0 then
                        printfn "[DEBUG] Available refresh rates for %dx%d:" mode.Width mode.Height
                        availableForResolution |> List.iter (fun m -> printfn "[DEBUG]   - %dHz" m.RefreshRate)
                    else
                        printfn "[DEBUG] No modes found for resolution %dx%d" mode.Width mode.Height
                    
                    Error (sprintf "Mode %dx%d @ %dHz is not supported by display %s" mode.Width mode.Height mode.RefreshRate displayId)
                else
                    // Try to get the exact DEVMODE structure for the target mode
                    printfn "[DEBUG] Getting exact DEVMODE structure for target mode..."
                    let mutable finalDevMode = 
                        match DisplayDetection.getExactDevModeForMode displayId mode with
                        | Some exactDevMode ->
                            printfn "[DEBUG] Found exact DEVMODE - using it instead of modified current mode"
                            
                            // Use the exact mode but preserve orientation
                            let mutable targetDevMode = exactDevMode
                            targetDevMode.dmDisplayOrientation <- DisplayStateCache.orientationToWindows orientation
                            
                            // Only add orientation field if it's different
                            if orientation <> Landscape then
                                targetDevMode.dmFields <- targetDevMode.dmFields ||| 0x00000080u // DM_DISPLAYORIENTATION
                            
                            printfn "[DEBUG] Using exact DEVMODE with fields: 0x%08X" targetDevMode.dmFields
                            targetDevMode
                        | None ->
                            printfn "[DEBUG] Could not find exact DEVMODE - using modified current mode"
                            devMode
                    
                    // Test the change first
                    printfn "[DEBUG] Testing display mode change with CDS_TEST flag..."
                    let testResult = WindowsAPI.ChangeDisplaySettingsEx(displayId, &finalDevMode, IntPtr.Zero, WindowsAPI.CDS.CDS_TEST, IntPtr.Zero)
                    printfn "[DEBUG] Test result: %d" testResult
                    
                    if testResult <> WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                        let errorMsg = match testResult with
                                       | x when x = WindowsAPI.DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                                       | x when x = WindowsAPI.DISP.DISP_CHANGE_FAILED -> "Display driver failed the mode change"
                                       | x when x = WindowsAPI.DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                                       | x when x = WindowsAPI.DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                                       | x when x = WindowsAPI.DISP.DISP_CHANGE_NOTUPDATED -> "Unable to write settings to registry"
                                       | x when x = WindowsAPI.DISP.DISP_CHANGE_BADDUALVIEW -> "Bad dual view configuration"
                                       | _ -> sprintf "Unknown error code: %d" testResult
                        printfn "[DEBUG] ERROR: Display mode test failed: %s" errorMsg
                        Error (sprintf "Display mode test failed for %s: %s" displayId errorMsg)
                    else
                        // Apply the change
                        printfn "[DEBUG] Test successful! Applying display mode change..."
                        printfn "[DEBUG] Using flags: CDS_UPDATEREGISTRY (0x%08X)" WindowsAPI.CDS.CDS_UPDATEREGISTRY
                        
                        let applyResult = WindowsAPI.ChangeDisplaySettingsEx(displayId, &finalDevMode, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                        printfn "[DEBUG] Apply result: %d" applyResult
                        
                        if applyResult = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                            printfn "[DEBUG] SUCCESS: Display mode applied successfully!"
                            
                            // Verify the change took effect
                            let mutable verifyMode = WindowsAPI.DEVMODE()
                            verifyMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
                            let verifyResult = WindowsAPI.EnumDisplaySettings(displayId, -1, &verifyMode)
                            if verifyResult then
                                printfn "[DEBUG] Verification - New settings:"
                                printfn "[DEBUG]   Resolution: %ux%u @ %uHz" verifyMode.dmPelsWidth verifyMode.dmPelsHeight verifyMode.dmDisplayFrequency
                                printfn "[DEBUG]   Orientation: %u" verifyMode.dmDisplayOrientation
                                
                                if verifyMode.dmPelsWidth <> uint32 mode.Width || 
                                   verifyMode.dmPelsHeight <> uint32 mode.Height ||
                                   verifyMode.dmDisplayFrequency <> uint32 mode.RefreshRate then
                                    printfn "[DEBUG] WARNING: Verified settings don't match requested settings!"
                            
                            Ok ()
                        elif applyResult = WindowsAPI.DISP.DISP_CHANGE_RESTART then
                            printfn "[DEBUG] Display mode applied, but system restart required"
                            Ok () // Still consider success, just inform user
                        else
                            let errorMsg = match applyResult with
                                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                                           | x when x = WindowsAPI.DISP.DISP_CHANGE_FAILED -> "Display driver failed the mode change"
                                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                                           | x when x = WindowsAPI.DISP.DISP_CHANGE_NOTUPDATED -> "Unable to write settings to registry"
                                           | x when x = WindowsAPI.DISP.DISP_CHANGE_BADDUALVIEW -> "Bad dual view configuration"
                                           | _ -> sprintf "Unknown error code: %d" applyResult
                            printfn "[DEBUG] ERROR: Failed to apply display mode: %s" errorMsg
                            Error (sprintf "Failed to apply display mode to %s: %s" displayId errorMsg)
        with
        | ex ->
            printfn "[DEBUG] EXCEPTION in applyDisplayMode: %s" ex.Message
            printfn "[DEBUG] Stack trace: %s" ex.StackTrace
            Error (sprintf "Exception applying display mode to %s: %s" displayId ex.Message)

    // Set display as primary
    let setPrimaryDisplay (displayId: DisplayId) =
        try
            printfn "Setting %s as primary display" displayId

            // Get current display settings
            let mutable devMode = WindowsAPI.DEVMODE()
            devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
            
            let getCurrentResult = WindowsAPI.EnumDisplaySettings(displayId, -1, &devMode)
            if not getCurrentResult then
                Error (sprintf "Could not get current display settings for %s" displayId)
            else
                // Set position to 0,0 and use SET_PRIMARY flag
                devMode.dmPositionX <- 0
                devMode.dmPositionY <- 0
                devMode.dmFields <- devMode.dmFields ||| 0x00000020u // DM_POSITION

                let result = WindowsAPI.ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, 
                                                     WindowsAPI.CDS.CDS_SET_PRIMARY ||| WindowsAPI.CDS.CDS_UPDATEREGISTRY ||| WindowsAPI.CDS.CDS_NORESET, 
                                                     IntPtr.Zero)
                
                if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                    printfn "Successfully set %s as primary display" displayId
                    Ok ()
                else
                    let errorMsg = match result with
                                   | x when x = WindowsAPI.DISP.DISP_CHANGE_FAILED -> "Failed to set as primary"
                                   | x when x = WindowsAPI.DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                                   | x when x = WindowsAPI.DISP.DISP_CHANGE_BADFLAGS -> "Invalid flags"
                                   | _ -> sprintf "Unknown error code: %d" result
                    Error (sprintf "Failed to set %s as primary: %s" displayId errorMsg)
        with
        | ex ->
            Error (sprintf "Exception setting %s as primary: %s" displayId ex.Message)

    // Diagnostic function to check display state before and after API calls
    let private checkDisplayState displayId description =
        printfn "[DEBUG] === %s ===" description
        printfn "[DEBUG] Checking state with simple approach..."
        
        // Just use the existing display detection to check if display is enabled
        let displays = DisplayDetection.getConnectedDisplays()
        let targetDisplay = displays |> List.tryFind (fun d -> d.Id = displayId)
        match targetDisplay with
        | Some display ->
            printfn "[DEBUG] %s - IsEnabled: %b" displayId display.IsEnabled
            printfn "[DEBUG] %s - Name: %s" displayId display.Name
        | None ->
            printfn "[DEBUG] %s - Display not found in detection results" displayId

    // Enable or disable a display using the proper modern Windows API approach
    let setDisplayEnabled (displayId: DisplayId) (enabled: bool) =
        try
            printfn "[DEBUG] ========== Starting setDisplayEnabled =========="
            printfn "[DEBUG] Display ID: %s" displayId
            printfn "[DEBUG] Target Enabled State: %b" enabled

            // Check initial state
            checkDisplayState displayId "BEFORE API CALLS"

            if enabled then
                // Use the correct approach: force Windows to auto-detect and enable all displays
                printfn "[DEBUG] Attempting to enable display using Windows auto-detection..."
                
                // Strategy 1: Use targeted approach with current display paths
                printfn "[DEBUG] Step 1: Getting current display configuration..."
                match DisplayConfigurationAPI.getDisplayPaths true with // Include inactive displays
                | Ok (pathArray, modeArray, pathCount, modeCount) ->
                    printfn "[DEBUG] Found %d paths, %d modes - looking for %s" pathCount modeCount displayId
                    
                    // Find the target display path
                    match DisplayConfigurationAPI.findDisplayPath displayId pathArray (int pathCount) with
                    | Ok (targetPath, pathIndex) ->
                        printfn "[DEBUG] Found target path at index %d" pathIndex
                        
                        // Create a modified path array with the target display enabled
                        let modifiedPaths = Array.copy pathArray
                        let mutable modifiedPath = targetPath
                        modifiedPath.flags <- modifiedPath.flags ||| WindowsAPI.DISPLAYCONFIG_PATH.DISPLAYCONFIG_PATH_ACTIVE
                        modifiedPath.targetInfo.targetAvailable <- 1
                        modifiedPaths.[pathIndex] <- modifiedPath
                        
                        printfn "[DEBUG] Applying modified display configuration..."
                        let applyResult = WindowsAPI.SetDisplayConfig(pathCount, modifiedPaths, modeCount, modeArray, WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES)
                        if applyResult = WindowsAPI.ERROR.ERROR_SUCCESS then
                            printfn "[DEBUG] SUCCESS: Modified display configuration applied!"
                            checkDisplayState displayId "AFTER TARGETED CONFIGURATION"
                            
                            // Give Windows time to apply changes
                            System.Threading.Thread.Sleep(1000)
                            checkDisplayState displayId "AFTER 1000MS DELAY"
                            
                            Ok ()
                        else
                            printfn "[DEBUG] Targeted configuration failed (%d), falling back to topology extend..." applyResult
                            
                            // Fallback: Use empty configuration with extend topology
                            let extendResult = WindowsAPI.SetDisplayConfig(0u, null, 0u, null, WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_TOPOLOGY_EXTEND)
                            if extendResult = WindowsAPI.ERROR.ERROR_SUCCESS then
                                printfn "[DEBUG] SUCCESS: SetDisplayConfig topology extend succeeded!"
                                checkDisplayState displayId "AFTER TOPOLOGY EXTEND"
                                
                                // Give Windows time to apply changes
                                System.Threading.Thread.Sleep(500)
                                checkDisplayState displayId "AFTER 500MS DELAY"
                                
                                Ok ()
                            else
                                printfn "[DEBUG] Topology extend also failed (%d)" extendResult
                                Error (sprintf "Both targeted and topology extend failed: %d, %d" applyResult extendResult)
                    | Error err ->
                        printfn "[DEBUG] Could not find display path for %s: %s" displayId err
                        
                        // Fallback to topology extend
                        let extendResult = WindowsAPI.SetDisplayConfig(0u, null, 0u, null, WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_TOPOLOGY_EXTEND)
                        if extendResult = WindowsAPI.ERROR.ERROR_SUCCESS then
                            printfn "[DEBUG] SUCCESS: SetDisplayConfig topology extend succeeded!"
                            Ok ()
                        else
                            Error (sprintf "Failed to find path and topology extend failed: %d" extendResult)
                | Error err ->
                    printfn "[DEBUG] Failed to get display paths: %s" err
                    
                    // Fallback to topology extend
                    let extendResult = WindowsAPI.SetDisplayConfig(0u, null, 0u, null, WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_TOPOLOGY_EXTEND)
                    if extendResult = WindowsAPI.ERROR.ERROR_SUCCESS then
                        printfn "[DEBUG] SUCCESS: SetDisplayConfig topology extend succeeded!"
                        Ok ()
                    else
                        Error (sprintf "Failed to get paths and topology extend failed: %d" extendResult)
                        
                // If all strategies above failed, try additional fallback approaches
                |> function
                | Ok result -> Ok result
                | Error _ ->
                    // Strategy 2: Force Windows to re-enumerate all displays
                    printfn "[DEBUG] Trying force mode enumeration as fallback..."
                    let forceEnumResult = WindowsAPI.SetDisplayConfig(0u, null, 0u, null, 
                                                          WindowsAPI.SDC.SDC_APPLY ||| WindowsAPI.SDC.SDC_FORCE_MODE_ENUMERATION ||| WindowsAPI.SDC.SDC_ALLOW_CHANGES)
                    if forceEnumResult = WindowsAPI.ERROR.ERROR_SUCCESS then
                        printfn "[DEBUG] SUCCESS: Display enabled via forced mode enumeration!"
                        Ok ()
                    else
                        // Strategy 3: Try manual ChangeDisplaySettingsEx approach
                        printfn "[DEBUG] Final fallback: using ChangeDisplaySettingsEx..."
                        
                        // Check if we have saved state for this display
                        match DisplayStateCache.getSavedDisplayState displayId with
                        | Some savedState ->
                            printfn "[DEBUG] Found saved state, restoring to: %dx%d @ %dHz at (%d, %d)" 
                                    savedState.Resolution.Width savedState.Resolution.Height 
                                    savedState.Resolution.RefreshRate savedState.Position.X savedState.Position.Y
                            
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
                            
                            let result = WindowsAPI.ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                            if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                                printfn "[DEBUG] SUCCESS: Display restored from saved state!"
                                Ok ()
                            else
                                printfn "[DEBUG] Failed to restore saved state (%d)" result
                                Error (sprintf "All strategies failed to enable display %s" displayId)
                        | None ->
                            // Auto-detect best available mode
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
                                
                                let mutable devMode = WindowsAPI.DEVMODE()
                                devMode.dmSize <- uint16 (Marshal.SizeOf(typeof<WindowsAPI.DEVMODE>))
                                devMode.dmPelsWidth <- uint32 bestMode.Width
                                devMode.dmPelsHeight <- uint32 bestMode.Height
                                devMode.dmDisplayFrequency <- uint32 bestMode.RefreshRate
                                devMode.dmBitsPerPel <- uint32 bestMode.BitsPerPixel
                                devMode.dmDisplayOrientation <- WindowsAPI.DMDO.DMDO_DEFAULT
                                devMode.dmPositionX <- 3840 // Position to right of other displays
                                devMode.dmPositionY <- 0
                                devMode.dmFields <- 0x00020000u ||| 0x00040000u ||| 0x00080000u ||| 0x00400000u ||| 0x00000020u
                                
                                printfn "[DEBUG] Using auto-detected mode: %dx%d @ %dHz" bestMode.Width bestMode.Height bestMode.RefreshRate
                                
                                let result = WindowsAPI.ChangeDisplaySettingsEx(displayId, &devMode, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                                if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                                    printfn "[DEBUG] SUCCESS: Display enabled with auto-detected settings!"
                                    Ok ()
                                else
                                    let errorMsg = match result with
                                                   | x when x = WindowsAPI.DISP.DISP_CHANGE_BADMODE -> "Invalid display mode"
                                                   | x when x = WindowsAPI.DISP.DISP_CHANGE_FAILED -> "Display driver failed"
                                                   | _ -> sprintf "Error code: %d" result
                                    Error (sprintf "Failed to enable display %s: %s" displayId errorMsg)
            else
                // Disabling display: save current state first, then disable
                printfn "[DEBUG] Disabling display - saving current state first..."
                let stateSaved = DisplayStateCache.saveDisplayState displayId
                if stateSaved then
                    printfn "[DEBUG] Display state saved successfully"
                else
                    printfn "[DEBUG] Warning: Failed to save display state"
                
                // Disable using ChangeDisplaySettingsEx with NULL DEVMODE
                printfn "[DEBUG] Disabling display using ChangeDisplaySettingsEx with NULL mode..."
                let result = WindowsAPI.ChangeDisplaySettingsExNull(displayId, IntPtr.Zero, IntPtr.Zero, WindowsAPI.CDS.CDS_UPDATEREGISTRY, IntPtr.Zero)
                if result = WindowsAPI.DISP.DISP_CHANGE_SUCCESSFUL then
                    printfn "[DEBUG] SUCCESS: Display disabled!"
                    Ok ()
                else
                    let errorMsg = match result with
                                   | x when x = WindowsAPI.DISP.DISP_CHANGE_FAILED -> "Display driver failed"
                                   | x when x = WindowsAPI.DISP.DISP_CHANGE_BADPARAM -> "Invalid parameter"
                                   | _ -> sprintf "Unknown error code: %d" result
                    printfn "[DEBUG] Failed to disable display (%d), this is expected behavior for some displays" result
                    // Don't treat disable failure as fatal - some displays can't be disabled this way
                    Ok ()
        with
        | ex ->
            printfn "[DEBUG] EXCEPTION in setDisplayEnabled: %s" ex.Message
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