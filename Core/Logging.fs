namespace DisplaySwitchPro

open System

/// Log levels for the application
type LogLevel =
    | Error = 0
    | Normal = 1
    | Verbose = 2

/// Centralized logging module for DisplaySwitch-Pro
module Logging =

    /// Mutable current log level - can be changed at runtime
    let mutable private currentLogLevel = LogLevel.Normal

    /// Set the current log level
    let setLogLevel level =
        currentLogLevel <- level

    /// Get the current log level
    let getLogLevel () = currentLogLevel

    /// Internal function to format log messages with timestamp and level
    let private formatMessage level message =
        let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
        let levelStr =
            match level with
            | LogLevel.Error -> "ERROR"
            | LogLevel.Normal -> "INFO"
            | LogLevel.Verbose -> "DEBUG"
            | _ -> "UNKNOWN"
        sprintf "[%s] [%s] %s" timestamp levelStr message

    /// Internal function to determine if a message should be logged
    let private shouldLog messageLevel =
        int messageLevel <= int currentLogLevel

    /// Log an error message (always shown unless completely disabled)
    let logError message =
        if shouldLog LogLevel.Error then
            formatMessage LogLevel.Error message |> printfn "%s"

    /// Log a normal/info message (shown at Normal and Verbose levels)
    let logNormal message =
        if shouldLog LogLevel.Normal then
            formatMessage LogLevel.Normal message |> printfn "%s"

    /// Log a verbose/debug message (only shown at Verbose level)
    let logVerbose message =
        if shouldLog LogLevel.Verbose then
            formatMessage LogLevel.Verbose message |> printfn "%s"

    /// Log an error message with printf-style formatting
    let logErrorf format = Printf.kprintf logError format

    /// Log a normal message with printf-style formatting
    let logNormalf format = Printf.kprintf logNormal format

    /// Log a verbose message with printf-style formatting
    let logVerbosef format = Printf.kprintf logVerbose format