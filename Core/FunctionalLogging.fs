namespace DisplaySwitchPro

open System
open System.IO
open System.Text.Json

/// Functional logging system with structured output and pure function design
/// Provides immutable configuration and composable logging operations
module FunctionalLogging =

    /// Log level enumeration for filtering messages
    type LogLevel =
        | Trace = 0
        | Debug = 1
        | Info = 2
        | Warning = 3
        | Error = 4
        | Critical = 5

    /// Structured log entry with rich metadata
    type LogEntry = {
        /// When the log entry was created
        Timestamp: DateTime
        /// Severity level of the message
        Level: LogLevel
        /// The log message text
        Message: string
        /// Source component or module
        Source: string option
        /// Correlation ID for tracking related operations
        CorrelationId: string option
        /// Additional structured data
        Properties: Map<string, obj>
        /// Exception information if applicable
        Exception: exn option
        /// Thread ID for concurrent operation tracking
        ThreadId: int
    }

    /// Log output destination configuration
    type LogOutput =
        | Console
        | File of path: string
        | Memory of buffer: LogEntry list ref
        | Structured of writer: (LogEntry -> unit)
        | Multiple of outputs: LogOutput list

    /// Log formatting options
    type LogFormatter =
        | Simple
        | Detailed
        | Json
        | Custom of (LogEntry -> string)

    /// Immutable logging configuration
    type LogConfig = {
        /// Minimum level to log
        MinLevel: LogLevel
        /// Output destination
        Output: LogOutput
        /// Message formatter
        Formatter: LogFormatter
        /// Whether to include source information
        IncludeSource: bool
        /// Whether to include thread information
        IncludeThreadId: bool
        /// Whether to include timestamp
        IncludeTimestamp: bool
        /// Maximum message length (truncate if longer)
        MaxMessageLength: int option
        /// Time zone for timestamps
        TimeZone: TimeZoneInfo
        /// Additional global properties to include in all log entries
        GlobalProperties: Map<string, obj>
    }

    /// Default configuration for console logging
    let defaultConfig = {
        MinLevel = LogLevel.Info
        Output = Console
        Formatter = Simple
        IncludeSource = true
        IncludeThreadId = false
        IncludeTimestamp = true
        MaxMessageLength = None
        TimeZone = TimeZoneInfo.Local
        GlobalProperties = Map.empty
    }

    /// Core logging functionality
    module Core =

        /// Create a log entry with the specified parameters
        let createLogEntry level message source correlationId properties exceptionInfo =
            {
                Timestamp = DateTime.Now
                Level = level
                Message = message
                Source = source
                CorrelationId = correlationId
                Properties = properties
                Exception = exceptionInfo
                ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId
            }

        /// Truncate message if it exceeds maximum length
        let truncateMessage maxLength message =
            match maxLength with
            | Some max when String.length message > max ->
                message.Substring(0, max - 3) + "..."
            | _ -> message

        /// Check if a log entry should be written based on configuration
        let shouldLog (config: LogConfig) (entry: LogEntry) =
            entry.Level >= config.MinLevel

        /// Format a log entry as a simple string
        let formatSimple (config: LogConfig) (entry: LogEntry) =
            let timestampPart =
                if config.IncludeTimestamp then
                    sprintf "[%s] " (entry.Timestamp.ToString("HH:mm:ss.fff"))
                else ""

            let levelPart = sprintf "[%A] " entry.Level

            let sourcePart =
                if config.IncludeSource then
                    match entry.Source with
                    | Some source -> sprintf "[%s] " source
                    | None -> ""
                else ""

            let threadPart =
                if config.IncludeThreadId then
                    sprintf "[T%d] " entry.ThreadId
                else ""

            let messagePart = truncateMessage config.MaxMessageLength entry.Message

            let exceptionPart =
                match entry.Exception with
                | Some ex -> sprintf " | Exception: %s" ex.Message
                | None -> ""

            sprintf "%s%s%s%s%s%s" timestampPart levelPart sourcePart threadPart messagePart exceptionPart

        /// Format a log entry with detailed information
        let formatDetailed (config: LogConfig) (entry: LogEntry) =
            let basicFormat = formatSimple config entry

            let propertiesPart =
                if Map.isEmpty entry.Properties then ""
                else
                    let propStrings =
                        entry.Properties
                        |> Map.toList
                        |> List.map (fun (k, v) -> sprintf "%s=%A" k v)
                        |> String.concat ", "
                    sprintf " | Properties: {%s}" propStrings

            let correlationPart =
                match entry.CorrelationId with
                | Some id -> sprintf " | CorrelationId: %s" id
                | None -> ""

            sprintf "%s%s%s" basicFormat propertiesPart correlationPart

        /// Format a log entry as JSON
        let formatJson (config: LogConfig) (entry: LogEntry) =
            let jsonObject =
                [
                    ("timestamp", box (entry.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")))
                    ("level", box (string entry.Level))
                    ("message", box (truncateMessage config.MaxMessageLength entry.Message))
                    ("threadId", box entry.ThreadId)
                ]
                |> List.append (
                    match entry.Source with
                    | Some source -> [("source", box source)]
                    | None -> []
                )
                |> List.append (
                    match entry.CorrelationId with
                    | Some id -> [("correlationId", box id)]
                    | None -> []
                )
                |> List.append (
                    match entry.Exception with
                    | Some ex -> [("exception", box ex.Message); ("stackTrace", box ex.StackTrace)]
                    | None -> []
                )
                |> List.append (
                    entry.Properties |> Map.toList |> List.map (fun (k, v) -> (k, v))
                )
                |> dict

            JsonSerializer.Serialize(jsonObject)

        /// Apply the configured formatter to a log entry
        let formatEntry (config: LogConfig) (entry: LogEntry) =
            match config.Formatter with
            | Simple -> formatSimple config entry
            | Detailed -> formatDetailed config entry
            | Json -> formatJson config entry
            | Custom formatter -> formatter entry

        /// Write a log entry to the console
        let writeToConsole (formattedMessage: string) =
            printfn "%s" formattedMessage

        /// Write a log entry to a file
        let writeToFile (filePath: string) (formattedMessage: string) =
            try
                File.AppendAllText(filePath, formattedMessage + Environment.NewLine)
            with
            | ex -> eprintfn "Failed to write to log file %s: %s" filePath ex.Message

        /// Write a log entry to a memory buffer
        let writeToMemory (buffer: LogEntry list ref) (entry: LogEntry) =
            lock buffer (fun () ->
                buffer := entry :: !buffer
            )

        /// Write a log entry to the configured output
        let writeToOutput (config: LogConfig) (entry: LogEntry) =
            let rec writeToSingleOutput output =
                match output with
                | Console ->
                    let formatted = formatEntry config entry
                    writeToConsole formatted

                | File path ->
                    let formatted = formatEntry config entry
                    writeToFile path formatted

                | Memory buffer ->
                    writeToMemory buffer entry

                | Structured writer ->
                    writer entry

                | Multiple outputs ->
                    outputs |> List.iter writeToSingleOutput

            writeToSingleOutput config.Output

        /// The core logging function - pure except for the final write operation
        let log (config: LogConfig) (entry: LogEntry) =
            if shouldLog config entry then
                // Merge global properties with entry properties
                let mergedEntry =
                    { entry with
                        Properties =
                            Map.fold (fun acc key value -> Map.add key value acc)
                                entry.Properties
                                config.GlobalProperties }

                writeToOutput config mergedEntry

    /// High-level logging interface with pre-configured functions
    module Logger =

        /// Create a logger function with the specified configuration
        let create (config: LogConfig) =
            fun level source message properties correlationId exceptionInfo ->
                let entry = Core.createLogEntry level message source correlationId properties exceptionInfo
                Core.log config entry

        /// Create a simple logger that just takes level and message
        let createSimple (config: LogConfig) =
            let logger = create config
            fun level message ->
                logger level None message Map.empty None None

        /// Create a logger with a fixed source
        let createWithSource (config: LogConfig) (source: string) =
            let logger = create config
            fun level message properties correlationId exceptionInfo ->
                logger level (Some source) message properties correlationId exceptionInfo

        /// Create level-specific logging functions
        let createLevelLoggers (config: LogConfig) (source: string option) =
            let logger = create config
            let logAtLevel level = fun message -> logger level source message Map.empty None None
            let logAtLevelWithProps level = fun message props -> logger level source message props None None
            let logAtLevelWithEx level = fun message ex -> logger level source message Map.empty None (Some ex)

            {|
                Trace = logAtLevel LogLevel.Trace
                Debug = logAtLevel LogLevel.Debug
                Info = logAtLevel LogLevel.Info
                Warning = logAtLevel LogLevel.Warning
                Error = logAtLevel LogLevel.Error
                Critical = logAtLevel LogLevel.Critical
                TraceWithProps = logAtLevelWithProps LogLevel.Trace
                DebugWithProps = logAtLevelWithProps LogLevel.Debug
                InfoWithProps = logAtLevelWithProps LogLevel.Info
                WarningWithProps = logAtLevelWithProps LogLevel.Warning
                ErrorWithProps = logAtLevelWithProps LogLevel.Error
                CriticalWithProps = logAtLevelWithProps LogLevel.Critical
                TraceWithException = logAtLevelWithEx LogLevel.Trace
                DebugWithException = logAtLevelWithEx LogLevel.Debug
                InfoWithException = logAtLevelWithEx LogLevel.Info
                WarningWithException = logAtLevelWithEx LogLevel.Warning
                ErrorWithException = logAtLevelWithEx LogLevel.Error
                CriticalWithException = logAtLevelWithEx LogLevel.Critical
            |}

    /// Configuration builders for creating log configurations
    module ConfigBuilder =

        /// Start building a configuration from the default
        let fromDefault () = defaultConfig

        /// Set the minimum log level
        let withMinLevel level config = { config with MinLevel = level }

        /// Set console output
        let withConsoleOutput config = { config with Output = Console }

        /// Set file output
        let withFileOutput path config = { config with Output = File path }

        /// Set memory output (useful for testing)
        let withMemoryOutput buffer config = { config with Output = Memory buffer }

        /// Set structured output
        let withStructuredOutput writer config = { config with Output = Structured writer }

        /// Set multiple outputs
        let withMultipleOutputs outputs config = { config with Output = Multiple outputs }

        /// Set simple formatting
        let withSimpleFormatter config = { config with Formatter = Simple }

        /// Set detailed formatting
        let withDetailedFormatter config = { config with Formatter = Detailed }

        /// Set JSON formatting
        let withJsonFormatter config = { config with Formatter = Json }

        /// Set custom formatting
        let withCustomFormatter formatter config = { config with Formatter = Custom formatter }

        /// Include source information
        let includeSource config = { config with IncludeSource = true }

        /// Exclude source information
        let excludeSource config = { config with IncludeSource = false }

        /// Include thread ID
        let includeThreadId config = { config with IncludeThreadId = true }

        /// Exclude thread ID
        let excludeThreadId config = { config with IncludeThreadId = false }

        /// Include timestamp
        let includeTimestamp config = { config with IncludeTimestamp = true }

        /// Exclude timestamp
        let excludeTimestamp config = { config with IncludeTimestamp = false }

        /// Set maximum message length
        let withMaxMessageLength length config = { config with MaxMessageLength = Some length }

        /// Remove message length limit
        let withoutMaxMessageLength config = { config with MaxMessageLength = None }

        /// Set time zone for timestamps
        let withTimeZone timeZone config = { config with TimeZone = timeZone }

        /// Add global properties
        let withGlobalProperties properties config =
            { config with GlobalProperties = Map.fold (fun acc k v -> Map.add k v acc) config.GlobalProperties properties }

        /// Add a single global property
        let withGlobalProperty key value config =
            { config with GlobalProperties = Map.add key value config.GlobalProperties }

    /// Correlation ID utilities for tracking related operations
    module Correlation =

        /// Generate a new correlation ID
        let newId () = Guid.NewGuid().ToString("N").[0..7]

        /// Create a logging function with a fixed correlation ID
        let withCorrelationId correlationId (logger: LogLevel -> string option -> string -> Map<string, obj> -> string option -> exn option -> unit) =
            fun level source message properties exceptionInfo ->
                logger level source message properties (Some correlationId) exceptionInfo

        /// Execute a function with a new correlation ID and return both result and correlation ID
        let withNewCorrelationId (f: string -> 'a) =
            let correlationId = newId ()
            let result = f correlationId
            (result, correlationId)

    /// Performance logging utilities
    module Performance =

        /// Measure execution time and log the result
        let measureAndLog (logger: LogLevel -> string option -> string -> Map<string, obj> -> string option -> exn option -> unit) level source operation correlationId f =
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            try
                let result = f ()
                stopwatch.Stop()
                let properties = Map.ofList [
                    ("operation", box operation)
                    ("durationMs", box stopwatch.ElapsedMilliseconds)
                    ("success", box true)
                ]
                logger level source (sprintf "Completed %s in %dms" operation stopwatch.ElapsedMilliseconds) properties correlationId None
                result
            with
            | ex ->
                stopwatch.Stop()
                let properties = Map.ofList [
                    ("operation", box operation)
                    ("durationMs", box stopwatch.ElapsedMilliseconds)
                    ("success", box false)
                ]
                logger LogLevel.Error source (sprintf "Failed %s after %dms" operation stopwatch.ElapsedMilliseconds) properties correlationId (Some ex)
                reraise()

        /// Simple timing function that returns duration
        let time f =
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            let result = f ()
            stopwatch.Stop()
            (result, stopwatch.Elapsed)

    /// Testing utilities for verifying logging behavior
    module Testing =

        /// Create a memory logger for testing
        let createMemoryLogger minLevel =
            let buffer = ref []
            let config =
                ConfigBuilder.fromDefault()
                |> ConfigBuilder.withMinLevel minLevel
                |> ConfigBuilder.withMemoryOutput buffer

            let logger = Logger.create config
            (logger, buffer)

        /// Get all log entries from a memory buffer
        let getLogEntries (buffer: LogEntry list ref) =
            List.rev !buffer

        /// Clear a memory buffer
        let clearLogEntries (buffer: LogEntry list ref) =
            buffer := []

        /// Count log entries at a specific level
        let countEntriesAtLevel level (buffer: LogEntry list ref) =
            !buffer |> List.filter (fun entry -> entry.Level = level) |> List.length

        /// Find log entries containing specific text
        let findEntriesWithText text (buffer: LogEntry list ref) =
            !buffer |> List.filter (fun entry -> entry.Message.Contains(text))

    /// Backward compatibility with existing Logging module
    module Compatibility =

        /// Create a logger that matches the existing Logging module interface
        let createCompatibleLogger () =
            let config =
                ConfigBuilder.fromDefault()
                |> ConfigBuilder.withMinLevel LogLevel.Info
                |> ConfigBuilder.withConsoleOutput
                |> ConfigBuilder.withSimpleFormatter

            let logger = Logger.createSimple config

            {|
                LogError = fun message -> logger LogLevel.Error message
                LogNormal = fun message -> logger LogLevel.Info message
                LogVerbose = fun message -> logger LogLevel.Debug message
                LogErrorf = fun format -> Printf.kprintf (fun msg -> logger LogLevel.Error msg) format
                LogNormalf = fun format -> Printf.kprintf (fun msg -> logger LogLevel.Info msg) format
                LogVerbosef = fun format -> Printf.kprintf (fun msg -> logger LogLevel.Debug msg) format
            |}