namespace DisplaySwitchPro

open System
open System.Runtime.InteropServices
open WindowsAPI

/// Monitor bounds detection algorithm for active display positioning
/// Provides monitor bounds detection for display positioning and layout operations
module MonitorBoundsDetection =

    /// Monitor bounds information
    type MonitorBounds = {
        DeviceName: string                 // Device name (e.g., \\.\DISPLAY1)
        Left: int                          // Left coordinate
        Top: int                           // Top coordinate
        Right: int                         // Right coordinate
        Bottom: int                        // Bottom coordinate
        Width: int                         // Calculated width
        Height: int                        // Calculated height
        IsPrimary: bool                    // Whether this is the primary monitor
    }

    /// Get active monitor bounds using EnumDisplayMonitors API
    let getActiveMonitorBounds() =
        let mutable monitorMap = Map.empty

        let monitorCallback =
            MonitorEnumDelegate(fun hMonitor hdcMonitor lprcMonitor dwData ->
                let mutable monitorInfo = MONITORINFOEX()
                monitorInfo.cbSize <- Marshal.SizeOf(typeof<MONITORINFOEX>)

                let result = GetMonitorInfo(hMonitor, &monitorInfo)
                if result then
                    let bounds = {
                        DeviceName = monitorInfo.szDevice
                        Left = monitorInfo.rcMonitor.left
                        Top = monitorInfo.rcMonitor.top
                        Right = monitorInfo.rcMonitor.right
                        Bottom = monitorInfo.rcMonitor.bottom
                        Width = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left
                        Height = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top
                        IsPrimary = (monitorInfo.dwFlags &&& 1u) <> 0u  // MONITORINFOF_PRIMARY
                    }
                    monitorMap <- Map.add monitorInfo.szDevice bounds monitorMap
                    Logging.logVerbosef " Monitor bounds: %s -> (%d,%d)-(%d,%d) %dx%d %s"
                        bounds.DeviceName bounds.Left bounds.Top bounds.Right bounds.Bottom
                        bounds.Width bounds.Height (if bounds.IsPrimary then "(Primary)" else "")

                true // Continue enumeration
            )

        let enumResult = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, monitorCallback, IntPtr.Zero)
        if enumResult then
            Logging.logVerbosef " Successfully enumerated %d active monitors" (Map.count monitorMap)
            monitorMap
        else
            Logging.logErrorf " Failed to enumerate display monitors"
            Map.empty


    /// Get bounds for a specific display device
    let getMonitorBoundsForDevice (deviceName: string) =
        let allBounds = getActiveMonitorBounds()
        Map.tryFind deviceName allBounds

    /// Get the primary monitor bounds
    let getPrimaryMonitorBounds() =
        let allBounds = getActiveMonitorBounds()
        allBounds
        |> Map.toSeq
        |> Seq.tryFind (fun (_, bounds) -> bounds.IsPrimary)
        |> Option.map snd

    /// Check if a display device has active monitor bounds
    let hasActiveMonitorBounds (deviceName: string) =
        let bounds = getActiveMonitorBounds()
        Map.containsKey deviceName bounds

    /// Get all active monitor device names
    let getActiveMonitorDeviceNames() =
        let bounds = getActiveMonitorBounds()
        bounds
        |> Map.keys
        |> Seq.toList

    /// Get monitor info with extended details using MONITORINFOEX
    let getActiveMonitorInfo() =
        let mutable monitors = Map.empty

        let monitorCallback =
            MonitorEnumDelegate(fun hMonitor hdcMonitor lprcMonitor dwData ->
                let mutable monitorInfo = MONITORINFOEX()
                monitorInfo.cbSize <- Marshal.SizeOf(typeof<MONITORINFOEX>)

                let result = GetMonitorInfo(hMonitor, &monitorInfo)
                if result then
                    monitors <- Map.add monitorInfo.szDevice monitorInfo monitors

                true // Continue enumeration
            )

        let enumResult = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, monitorCallback, IntPtr.Zero)
        if enumResult then
            monitors
        else
            Logging.logErrorf " Failed to get monitor info"
            Map.empty