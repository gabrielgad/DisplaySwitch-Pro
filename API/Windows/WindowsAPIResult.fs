namespace DisplaySwitchPro

open System

/// Enhanced result types and helpers for Windows API operations
/// Provides better error classification and handling without breaking existing APIs
module WindowsAPIResult =

    /// Enhanced error classification for Windows API operations
    type WindowsAPIError =
        | Win32Error of code: int * description: string
        | CcdError of code: uint32 * operation: string * context: string
        | ValidationError of message: string * attempts: int
        | HardwareError of deviceId: string * issue: string
        | TimeoutError of operation: string * duration: int
        | ConfigurationError of setting: string * value: string * reason: string
        | PermissionError of operation: string * requirements: string
        | DeviceError of deviceName: string * errorCode: int
        | UnknownError of source: string * originalMessage: string

    /// Error context for enhanced diagnostics
    type ErrorContext = {
        Operation: string
        DisplayId: string option
        AttemptNumber: int
        Timestamp: DateTime
        AdditionalData: Map<string, string>
    }

    /// Enhanced result type with rich error information
    type WindowsAPIResult<'T> = Result<'T, WindowsAPIError * ErrorContext option>

    /// Helper functions for error classification
    module ErrorClassification =

        /// Classify Windows API error codes
        let classifyWin32Error (errorCode: int) =
            match errorCode with
            | 5 -> Win32Error(errorCode, "Access denied - requires administrator privileges")
            | 87 -> Win32Error(errorCode, "Invalid parameter - check input values")
            | 1219 -> Win32Error(errorCode, "Multiple connections to server not allowed")
            | 1004 -> Win32Error(errorCode, "Invalid flags specified")
            | 170 -> Win32Error(errorCode, "Resource in use - device may be busy")
            | 1450 -> Win32Error(errorCode, "Insufficient system resources")
            | _ -> Win32Error(errorCode, sprintf "Windows API error %d" errorCode)

        /// Classify CCD API error codes
        let classifyCcdError (errorCode: uint32) (operation: string) =
            match errorCode with
            | 0u -> CcdError(errorCode, operation, "Success - no error")
            | 87u -> CcdError(errorCode, operation, "Invalid parameter - display configuration invalid")
            | 1004u -> CcdError(errorCode, operation, "Invalid flags - unsupported configuration options")
            | 1169u -> CcdError(errorCode, operation, "Device not found - display may be disconnected")
            | 1359u -> CcdError(errorCode, operation, "Internal error - driver or system issue")
            | 6u -> CcdError(errorCode, operation, "Invalid handle - display adapter issue")
            | _ -> CcdError(errorCode, operation, sprintf "CCD API error %u in %s" errorCode operation)

        /// Classify display validation errors
        let classifyValidationError (message: string) (attempts: int) =
            if message.Contains("timeout") || message.Contains("not responding") then
                TimeoutError("Display validation", attempts * 500)
            elif message.Contains("permission") || message.Contains("access") then
                PermissionError("Display validation", "Administrator privileges required")
            else
                ValidationError(message, attempts)

    /// Helper functions for creating enhanced results
    module ResultHelpers =

        /// Create success result
        let success (value: 'T) : WindowsAPIResult<'T> =
            Ok value

        /// Create error result with context
        let errorWithContext (error: WindowsAPIError) (context: ErrorContext) : WindowsAPIResult<'T> =
            Error (error, Some context)

        /// Create error result without context
        let error (error: WindowsAPIError) : WindowsAPIResult<'T> =
            Error (error, None)

        /// Create context for error reporting
        let createContext operation displayId attemptNumber additionalData =
            {
                Operation = operation
                DisplayId = displayId
                AttemptNumber = attemptNumber
                Timestamp = DateTime.Now
                AdditionalData = additionalData |> Map.ofList
            }

        /// Convert standard Result to WindowsAPIResult
        let fromStandardResult (result: Result<'T, string>) (operation: string) (displayId: string option) =
            match result with
            | Ok value -> success value
            | Error msg ->
                let error = UnknownError(operation, msg)
                let context = createContext operation displayId 1 []
                errorWithContext error context

        /// Map error with additional context
        let mapError (f: WindowsAPIError * ErrorContext option -> WindowsAPIError * ErrorContext option) (result: WindowsAPIResult<'T>) =
            match result with
            | Ok value -> Ok value
            | Error errorInfo -> Error (f errorInfo)

        /// Bind with error context preservation
        let bind (f: 'T -> WindowsAPIResult<'U>) (result: WindowsAPIResult<'T>) =
            match result with
            | Ok value -> f value
            | Error errorInfo -> Error errorInfo

    /// Retry helpers that can be optionally used
    module RetryHelpers =

        /// Retry configuration
        type RetryConfig = {
            MaxAttempts: int
            BaseDelay: int
            MaxDelay: int
            BackoffMultiplier: float
        }

        /// Default retry configuration for Windows API operations
        let defaultRetryConfig = {
            MaxAttempts = 3
            BaseDelay = 500
            MaxDelay = 4000
            BackoffMultiplier = 1.5
        }

        /// Calculate delay for retry attempt
        let calculateDelay (config: RetryConfig) (attempt: int) =
            let delay = float config.BaseDelay * (config.BackoffMultiplier ** float (attempt - 1))
            min (int delay) config.MaxDelay

        /// Retry a Windows API operation with exponential backoff
        let retryOperation (config: RetryConfig) (operation: int -> WindowsAPIResult<'T>) =
            let rec retry attempt =
                match operation attempt with
                | Ok value -> Ok value
                | Error (error, context) when attempt < config.MaxAttempts ->
                    let delay = calculateDelay config attempt
                    System.Threading.Thread.Sleep(delay)
                    retry (attempt + 1)
                | Error errorInfo -> Error errorInfo

            retry 1

        /// Retry with custom predicate to determine if retry should happen
        let retryOperationIf (config: RetryConfig) (shouldRetry: WindowsAPIError -> bool) (operation: int -> WindowsAPIResult<'T>) =
            let rec retry attempt =
                match operation attempt with
                | Ok value -> Ok value
                | Error (error, context) when attempt < config.MaxAttempts && shouldRetry error ->
                    let delay = calculateDelay config attempt
                    System.Threading.Thread.Sleep(delay)
                    retry (attempt + 1)
                | Error errorInfo -> Error errorInfo

            retry 1

    /// Enhanced error reporting and diagnostics
    module ErrorReporting =

        /// Format error for logging
        let formatError (error: WindowsAPIError) (context: ErrorContext option) =
            let errorMessage =
                match error with
                | Win32Error (code, desc) -> sprintf "Win32 Error %d: %s" code desc
                | CcdError (code, op, ctx) -> sprintf "CCD Error %u in %s: %s" code op ctx
                | ValidationError (msg, attempts) -> sprintf "Validation Error (attempt %d): %s" attempts msg
                | HardwareError (deviceId, issue) -> sprintf "Hardware Error [%s]: %s" deviceId issue
                | TimeoutError (operation, duration) -> sprintf "Timeout in %s after %dms" operation duration
                | ConfigurationError (setting, value, reason) -> sprintf "Configuration Error [%s=%s]: %s" setting value reason
                | PermissionError (operation, requirements) -> sprintf "Permission Error in %s: %s" operation requirements
                | DeviceError (deviceName, errorCode) -> sprintf "Device Error [%s]: Code %d" deviceName errorCode
                | UnknownError (source, originalMessage) -> sprintf "Unknown Error in %s: %s" source originalMessage

            match context with
            | Some ctx ->
                let additionalInfo =
                    if ctx.AdditionalData.IsEmpty then ""
                    else
                        ctx.AdditionalData
                        |> Map.fold (fun acc key value -> acc + sprintf ", %s=%s" key value) ""

                sprintf "[%s] %s (Attempt %d%s)%s"
                    ctx.Operation
                    errorMessage
                    ctx.AttemptNumber
                    (ctx.DisplayId |> Option.map (sprintf " - %s") |> Option.defaultValue "")
                    additionalInfo
            | None -> errorMessage

        /// Get user-friendly error message
        let getUserFriendlyMessage (error: WindowsAPIError) =
            match error with
            | Win32Error (5, _) -> "Access denied. Please run as administrator."
            | Win32Error (87, _) -> "Invalid display configuration. Please check your settings."
            | CcdError (_, _, ctx) when ctx.Contains("disconnected") -> "Display appears to be disconnected. Please check cables."
            | ValidationError (_, _) -> "Display did not respond as expected. This may be normal for some hardware."
            | HardwareError (_, _) -> "Hardware issue detected. Please check display connections and drivers."
            | TimeoutError (_, _) -> "Operation timed out. The display may need more time to respond."
            | PermissionError (_, _) -> "Insufficient permissions. Please run as administrator."
            | _ -> "An unexpected error occurred. Check the logs for more details."

    /// Optional performance tracking
    module PerformanceTracking =

        /// Performance metric
        type PerformanceMetric = {
            Operation: string
            Duration: TimeSpan
            Success: bool
            Error: WindowsAPIError option
            Timestamp: DateTime
        }

        /// Simple in-memory performance tracker (opt-in)
        let mutable private performanceMetrics: PerformanceMetric list = []

        /// Track performance of an operation
        let trackPerformance (operation: string) (f: unit -> WindowsAPIResult<'T>) =
            let startTime = DateTime.Now
            let result = f ()
            let endTime = DateTime.Now
            let duration = endTime - startTime

            let metric = {
                Operation = operation
                Duration = duration
                Success = Result.isOk result
                Error = match result with | Error (error, _) -> Some error | _ -> None
                Timestamp = startTime
            }

            performanceMetrics <- metric :: performanceMetrics
            result

        /// Get recent performance metrics
        let getRecentMetrics (count: int) =
            performanceMetrics
            |> List.take (min count performanceMetrics.Length)
            |> List.rev

        /// Clear performance metrics
        let clearMetrics () =
            performanceMetrics <- []

        /// Get performance summary
        let getPerformanceSummary () =
            let total = performanceMetrics.Length
            let successful = performanceMetrics |> List.filter (fun m -> m.Success) |> List.length
            let avgDuration =
                if total > 0 then
                    performanceMetrics
                    |> List.map (fun m -> m.Duration.TotalMilliseconds)
                    |> List.average
                else 0.0

            {|
                TotalOperations = total
                SuccessfulOperations = successful
                SuccessRate = if total > 0 then (float successful / float total) * 100.0 else 0.0
                AverageDurationMs = avgDuration
            |}