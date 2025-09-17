namespace DisplaySwitchPro

open System
open System.Collections.Concurrent

/// Strategy performance tracking for Windows display operations
/// This is an opt-in module for tracking strategy success rates and performance
module StrategyPerformance =

    /// Strategy types from WindowsControl.fs
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

    /// Operation types we track
    type OperationType =
        | EnableDisplay
        | DisableDisplay
        | SetDisplayMode
        | SetDisplayPosition
        | SetPrimaryDisplay

    /// Strategy execution result
    type StrategyExecutionResult = {
        Strategy: EnableStrategy
        Operation: OperationType
        DisplayId: string
        Success: bool
        Duration: TimeSpan
        ErrorMessage: string option
        AttemptNumber: int
        Timestamp: DateTime
    }

    /// Strategy performance statistics
    type StrategyStats = {
        Strategy: EnableStrategy
        TotalAttempts: int
        SuccessfulAttempts: int
        SuccessRate: float
        AverageDuration: TimeSpan
        LastUsed: DateTime option
        RecentFailures: string list
    }

    /// Thread-safe performance tracking store
    let private performanceStore = ConcurrentBag<StrategyExecutionResult>()

    /// Configuration for performance tracking
    type PerformanceConfig = {
        EnableTracking: bool
        MaxStoredResults: int
        RecentFailureCount: int
    }

    /// Default configuration (tracking disabled by default)
    let mutable private config = {
        EnableTracking = false
        MaxStoredResults = 1000
        RecentFailureCount = 5
    }

    /// Enable performance tracking (opt-in)
    let enableTracking (maxResults: int option) (recentFailures: int option) =
        config <- {
            EnableTracking = true
            MaxStoredResults = maxResults |> Option.defaultValue 1000
            RecentFailureCount = recentFailures |> Option.defaultValue 5
        }
        Logging.logVerbosef " Strategy performance tracking enabled (max results: %d)" config.MaxStoredResults

    /// Disable performance tracking
    let disableTracking () =
        config <- { config with EnableTracking = false }
        Logging.logVerbosef " Strategy performance tracking disabled"

    /// Check if tracking is enabled
    let isTrackingEnabled () = config.EnableTracking

    /// Record strategy execution result
    let recordStrategyExecution strategy operation displayId success duration errorMessage attemptNumber =
        if config.EnableTracking then
            let result = {
                Strategy = strategy
                Operation = operation
                DisplayId = displayId
                Success = success
                Duration = duration
                ErrorMessage = errorMessage
                AttemptNumber = attemptNumber
                Timestamp = DateTime.Now
            }

            performanceStore.Add(result)

            // Clean up old results if we exceed the limit
            let allResults = performanceStore.ToArray()
            if allResults.Length > config.MaxStoredResults then
                // Keep only the most recent results
                let recentResults =
                    allResults
                    |> Array.sortByDescending (fun r -> r.Timestamp)
                    |> Array.take config.MaxStoredResults

                // Clear and re-add recent results
                while not (performanceStore.IsEmpty) do
                    performanceStore.TryTake() |> ignore

                for result in recentResults do
                    performanceStore.Add(result)

    /// Track a strategy execution with timing
    let trackStrategyExecution strategy operation displayId (f: unit -> Result<'T, string>) attemptNumber =
        if config.EnableTracking then
            let startTime = DateTime.Now
            let result = f ()
            let endTime = DateTime.Now
            let duration = endTime - startTime

            let success = Result.isOk result
            let errorMessage = match result with | Error msg -> Some msg | _ -> None

            recordStrategyExecution strategy operation displayId success duration errorMessage attemptNumber
            result
        else
            f ()

    /// Get all recorded results
    let getAllResults () =
        performanceStore.ToArray() |> Array.toList

    /// Get results for a specific strategy
    let getResultsForStrategy strategy =
        performanceStore.ToArray()
        |> Array.filter (fun r -> r.Strategy = strategy)
        |> Array.toList

    /// Get results for a specific operation type
    let getResultsForOperation operation =
        performanceStore.ToArray()
        |> Array.filter (fun r -> r.Operation = operation)
        |> Array.toList

    /// Get results for a specific display
    let getResultsForDisplay displayId =
        performanceStore.ToArray()
        |> Array.filter (fun r -> r.DisplayId = displayId)
        |> Array.toList

    /// Calculate strategy statistics
    let getStrategyStats strategy =
        let results = getResultsForStrategy strategy

        if results.IsEmpty then
            {
                Strategy = strategy
                TotalAttempts = 0
                SuccessfulAttempts = 0
                SuccessRate = 0.0
                AverageDuration = TimeSpan.Zero
                LastUsed = None
                RecentFailures = []
            }
        else
            let totalAttempts = results.Length
            let successfulAttempts = results |> List.filter (fun r -> r.Success) |> List.length
            let successRate = (float successfulAttempts / float totalAttempts) * 100.0

            let averageDuration =
                if totalAttempts > 0 then
                    let totalMs = results |> List.sumBy (fun r -> r.Duration.TotalMilliseconds)
                    TimeSpan.FromMilliseconds(totalMs / float totalAttempts)
                else
                    TimeSpan.Zero

            let lastUsed =
                results
                |> List.maxBy (fun r -> r.Timestamp)
                |> fun r -> Some r.Timestamp

            let recentFailures =
                results
                |> List.filter (fun r -> not r.Success)
                |> List.sortByDescending (fun r -> r.Timestamp)
                |> List.take (min config.RecentFailureCount (results |> List.filter (fun r -> not r.Success) |> List.length))
                |> List.choose (fun r -> r.ErrorMessage)

            {
                Strategy = strategy
                TotalAttempts = totalAttempts
                SuccessfulAttempts = successfulAttempts
                SuccessRate = successRate
                AverageDuration = averageDuration
                LastUsed = lastUsed
                RecentFailures = recentFailures
            }

    /// Get statistics for all strategies
    let getAllStrategyStats () =
        [
            CCDTargeted; CCDModePopulation; CCDTopologyExtend; CCDMinimalPaths;
            CCDDirectPath; DEVMODEDirect; DEVMODEWithReset; HardwareReset; DisplaySwitchFallback
        ]
        |> List.map getStrategyStats
        |> List.filter (fun stats -> stats.TotalAttempts > 0)

    /// Get strategies ranked by success rate
    let getStrategiesBySuccessRate () =
        getAllStrategyStats ()
        |> List.sortByDescending (fun stats -> stats.SuccessRate)

    /// Get strategies ranked by average speed
    let getStrategiesBySpeed () =
        getAllStrategyStats ()
        |> List.filter (fun stats -> stats.SuccessfulAttempts > 0)
        |> List.sortBy (fun stats -> stats.AverageDuration.TotalMilliseconds)

    /// Get recommended strategy order based on performance
    let getRecommendedStrategyOrder () =
        let allStats = getAllStrategyStats ()

        if allStats.IsEmpty then
            // Default order when no performance data is available
            [CCDTargeted; CCDModePopulation; CCDMinimalPaths; CCDDirectPath; CCDTopologyExtend;
             DEVMODEDirect; DEVMODEWithReset; HardwareReset; DisplaySwitchFallback]
        else
            // Sort by success rate first, then by speed for ties
            allStats
            |> List.sortByDescending (fun stats ->
                (stats.SuccessRate, -stats.AverageDuration.TotalMilliseconds))
            |> List.map (fun stats -> stats.Strategy)

    /// Generate performance report
    let generatePerformanceReport () =
        if not config.EnableTracking then
            "Performance tracking is disabled. Call StrategyPerformance.enableTracking() to enable."
        else
            let allResults = getAllResults ()
            let allStats = getAllStrategyStats ()

            if allResults.IsEmpty then
                "No performance data available yet."
            else
                let totalOperations = allResults.Length
                let successfulOperations = allResults |> List.filter (fun r -> r.Success) |> List.length
                let overallSuccessRate = (float successfulOperations / float totalOperations) * 100.0

                let report = System.Text.StringBuilder()
                report.AppendLine("=== Strategy Performance Report ===") |> ignore
                report.AppendLine(sprintf "Total Operations: %d" totalOperations) |> ignore
                report.AppendLine(sprintf "Successful Operations: %d" successfulOperations) |> ignore
                report.AppendLine(sprintf "Overall Success Rate: %.1f%%" overallSuccessRate) |> ignore
                report.AppendLine() |> ignore

                report.AppendLine("Strategy Performance (ranked by success rate):") |> ignore
                for stats in getStrategiesBySuccessRate () do
                    report.AppendLine(sprintf "  %A: %.1f%% success (%d/%d), avg %.0fms"
                        stats.Strategy stats.SuccessRate stats.SuccessfulAttempts
                        stats.TotalAttempts stats.AverageDuration.TotalMilliseconds) |> ignore

                report.AppendLine() |> ignore
                report.AppendLine("Recommended Strategy Order:") |> ignore
                getRecommendedStrategyOrder ()
                |> List.iteri (fun i strategy ->
                    report.AppendLine(sprintf "  %d. %A" (i + 1) strategy) |> ignore)

                report.ToString()

    /// Clear all performance data
    let clearPerformanceData () =
        while not (performanceStore.IsEmpty) do
            performanceStore.TryTake() |> ignore
        Logging.logVerbosef " Performance data cleared"

    /// Export performance data to a list for external analysis
    let exportPerformanceData () =
        getAllResults ()
        |> List.map (fun result ->
            {|
                Strategy = result.Strategy.ToString()
                Operation = result.Operation.ToString()
                DisplayId = result.DisplayId
                Success = result.Success
                DurationMs = result.Duration.TotalMilliseconds
                ErrorMessage = result.ErrorMessage |> Option.defaultValue ""
                AttemptNumber = result.AttemptNumber
                Timestamp = result.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            |})

    /// Get performance insights
    let getPerformanceInsights () =
        let allStats = getAllStrategyStats ()

        if allStats.IsEmpty then
            []
        else
            let insights = System.Collections.Generic.List<string>()

            // Find the most reliable strategy
            let mostReliable = allStats |> List.maxBy (fun s -> s.SuccessRate)
            if mostReliable.SuccessRate > 80.0 then
                insights.Add(sprintf "%A is the most reliable strategy with %.1f%% success rate"
                    mostReliable.Strategy mostReliable.SuccessRate)

            // Find the fastest strategy
            let fastestReliable =
                allStats
                |> List.filter (fun s -> s.SuccessRate > 50.0)  // Only consider reasonably reliable strategies
                |> List.sortBy (fun s -> s.AverageDuration.TotalMilliseconds)
                |> List.tryHead

            match fastestReliable with
            | Some fastest when fastest.AverageDuration.TotalMilliseconds < 1000.0 ->
                insights.Add(sprintf "%A is the fastest reliable strategy at %.0fms average"
                    fastest.Strategy fastest.AverageDuration.TotalMilliseconds)
            | _ -> ()

            // Identify problematic strategies
            let problematic = allStats |> List.filter (fun s -> s.SuccessRate < 30.0 && s.TotalAttempts > 5)
            for strategy in problematic do
                insights.Add(sprintf "%A has low success rate (%.1f%%) and may need investigation"
                    strategy.Strategy strategy.SuccessRate)

            insights |> List.ofSeq