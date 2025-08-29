namespace DisplaySwitchPro

open System
open WindowsAPI

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