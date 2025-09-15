namespace DisplaySwitchPro

open System
open System.Threading
open Avalonia.Threading

/// Functional display change monitoring and notification system
module DisplayMonitor =

    type DisplayChangeType =
        | DisplayAdded of DisplayInfo list
        | DisplayRemoved of DisplayInfo list
        | DisplayStateChanged of DisplayInfo list  // Enable/disable changes
        | DisplayConfigurationChanged of DisplayInfo list  // Resolution/position changes

    type DisplayChangeEvent = {
        PreviousDisplays: DisplayInfo list
        CurrentDisplays: DisplayInfo list
        ChangeType: DisplayChangeType
        Timestamp: DateTime
    }

    type MonitorState = {
        LastDisplays: DisplayInfo list
        LastDisplayCount: int
        LastNoChangeLogTime: DateTime option
        Timer: Timer option
    }

    /// Pure function to detect changes between display lists
    let detectDisplayChanges (previousDisplays: DisplayInfo list) (currentDisplays: DisplayInfo list) : DisplayChangeType option =
        let prevIds = previousDisplays |> List.map (fun d -> d.Id) |> Set.ofList
        let currIds = currentDisplays |> List.map (fun d -> d.Id) |> Set.ofList

        let addedIds = Set.difference currIds prevIds
        let removedIds = Set.difference prevIds currIds
        let commonIds = Set.intersect prevIds currIds

        let added = currentDisplays |> List.filter (fun d -> Set.contains d.Id addedIds)
        let removed = previousDisplays |> List.filter (fun d -> Set.contains d.Id removedIds)

        // Check for state changes in common displays
        let stateChanged =
            commonIds
            |> Set.toList
            |> List.choose (fun id ->
                let prevDisplay = previousDisplays |> List.find (fun d -> d.Id = id)
                let currDisplay = currentDisplays |> List.find (fun d -> d.Id = id)

                if prevDisplay.IsEnabled <> currDisplay.IsEnabled ||
                   prevDisplay.Resolution <> currDisplay.Resolution ||
                   prevDisplay.Position <> currDisplay.Position ||
                   prevDisplay.IsPrimary <> currDisplay.IsPrimary then
                    Some currDisplay
                else None)

        // Return the most significant change
        if not added.IsEmpty then
            Some (DisplayAdded added)
        elif not removed.IsEmpty then
            Some (DisplayRemoved removed)
        elif not stateChanged.IsEmpty then
            Some (DisplayStateChanged stateChanged)
        else
            None

    /// Pure function to create a display change event
    let createDisplayChangeEvent (previous: DisplayInfo list) (current: DisplayInfo list) (changeType: DisplayChangeType) : DisplayChangeEvent =
        {
            PreviousDisplays = previous
            CurrentDisplays = current
            ChangeType = changeType
            Timestamp = DateTime.Now
        }

    /// Lightweight function to get connected display count using GetSystemMetrics
    let getConnectedDisplayCount() : int =
        WindowsAPI.GetSystemMetrics(WindowsAPI.SystemMetrics.SM_CMONITORS)

    /// Create a monitoring function that can be used with a timer
    let createMonitorFunction (getDisplays: unit -> DisplayInfo list) (onDisplayChanged: DisplayChangeEvent -> unit) (stateRef: MonitorState ref) : (obj -> unit) =
        fun _ ->
            try
                // Step 1: Lightweight check - get current display count
                let currentDisplayCount = getConnectedDisplayCount()
                let currentState = !stateRef
                let lastDisplayCount = currentState.LastDisplayCount

                // Step 2: Early exit if display count hasn't changed
                if currentDisplayCount = lastDisplayCount then
                    // Only log "no changes" every 30 seconds to reduce spam
                    let shouldLog =
                        match currentState.LastNoChangeLogTime with
                        | None -> true  // First time, log it
                        | Some lastLog -> (DateTime.Now - lastLog).TotalSeconds > 30.0

                    if shouldLog then
                        printfn "[DisplayMonitor] No changes detected (count: %d)" currentDisplayCount
                        stateRef := { currentState with LastNoChangeLogTime = Some DateTime.Now }
                    ()
                else
                    // Step 3: Display count changed, run full enumeration
                    printfn "[DisplayMonitor] Display count changed from %d to %d, running full detection" lastDisplayCount currentDisplayCount
                    let currentDisplays = getDisplays()
                    let previousDisplays = currentState.LastDisplays

                    match detectDisplayChanges previousDisplays currentDisplays with
                    | Some changeType ->
                        let changeEvent = createDisplayChangeEvent previousDisplays currentDisplays changeType

                        // Update state immutably with new count and displays
                        stateRef := {
                            currentState with
                                LastDisplays = currentDisplays
                                LastDisplayCount = currentDisplayCount
                                LastNoChangeLogTime = None  // Reset since we had a change
                        }

                        // Notify on UI thread
                        Dispatcher.UIThread.InvokeAsync(fun () -> onDisplayChanged changeEvent) |> ignore

                        printfn "[DisplayMonitor] Change detected: %A" changeType
                    | None ->
                        // Update display count even if no logical changes detected
                        stateRef := { currentState with LastDisplayCount = currentDisplayCount }
                        printfn "[DisplayMonitor] Display count changed but no logical changes detected"
            with
            | ex ->
                printfn "[DisplayMonitor] Error during monitoring: %s" ex.Message

    /// Start display monitoring with functional composition
    let startMonitoring (onDisplayChanged: DisplayChangeEvent -> unit) (intervalMs: int) : MonitorState =
        let initialDisplays = DisplayDetection.getConnectedDisplays()
        let initialDisplayCount = getConnectedDisplayCount()
        let stateRef = ref {
            LastDisplays = initialDisplays
            LastDisplayCount = initialDisplayCount
            LastNoChangeLogTime = None
            Timer = None
        }

        let monitorFunc = createMonitorFunction DisplayDetection.getConnectedDisplays onDisplayChanged stateRef
        let timer = new Timer(monitorFunc, null, intervalMs, intervalMs)

        let finalState = {
            LastDisplays = initialDisplays
            LastDisplayCount = initialDisplayCount
            LastNoChangeLogTime = None
            Timer = Some timer
        }
        stateRef := finalState

        printfn "[DisplayMonitor] Started monitoring with %d displays, %d count (interval: %dms)"
            initialDisplays.Length initialDisplayCount intervalMs

        finalState

    /// Stop monitoring and dispose resources
    let stopMonitoring (state: MonitorState) : unit =
        match state.Timer with
        | Some timer ->
            timer.Dispose()
            printfn "[DisplayMonitor] Monitoring stopped"
        | None ->
            printfn "[DisplayMonitor] No active monitoring to stop"

    /// Check for display changes immediately (pure function)
    let checkDisplayChanges (currentState: MonitorState) : DisplayChangeEvent option =
        let currentDisplays = DisplayDetection.getConnectedDisplays()
        match detectDisplayChanges currentState.LastDisplays currentDisplays with
        | Some changeType ->
            Some (createDisplayChangeEvent currentState.LastDisplays currentDisplays changeType)
        | None -> None