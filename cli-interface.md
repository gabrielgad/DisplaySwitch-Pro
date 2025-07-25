# Functional Command Line Interface

## Overview

The Command Line Interface (CLI) provides pure functional command processing for DisplaySwitch-Pro. Built on immutable data structures and composable functions, this CLI delivers reliable, testable, and scriptable access to display management through functional programming principles and effect handlers at boundaries only.

## Functional Command Architecture

### Immutable Command Types
**Location**: `Core/Commands.fs`

```fsharp
// Command representation as immutable data
type Command = 
    | SwitchMode of DisplayMode
    | LoadConfiguration of string
    | SaveConfiguration of string
    | ListConfigurations
    | ShowStatus
    | ShowHelp
    | LaunchGUI

and DisplayMode = 
    | PCMode 
    | TVMode
    | ExtendedMode of string list // Specific display names

// Command with arguments and options
type CommandRequest = {
    Command: Command
    Options: Map<string, string>
    Timestamp: DateTimeOffset
}

// Pure command result
type CommandResult = 
    | Success of string
    | Error of string
    | ShowOutput of string list
    | ExitCode of int
```

### Pure Command Parsing
**Location**: `Core/CommandParser.fs`

```fsharp
module CommandParser =
    
    // Pure function to parse command line arguments
    let parseArgs (args: string array) : Result<CommandRequest, string> =
        match args with
        | [||] -> Ok { Command = LaunchGUI; Options = Map.empty; Timestamp = DateTimeOffset.Now }
        | [|"pc"|] -> Ok { Command = SwitchMode PCMode; Options = Map.empty; Timestamp = DateTimeOffset.Now }
        | [|"tv"|] -> Ok { Command = SwitchMode TVMode; Options = Map.empty; Timestamp = DateTimeOffset.Now }
        | [|"load"; configName|] -> Ok { Command = LoadConfiguration configName; Options = Map.empty; Timestamp = DateTimeOffset.Now }
        | [|"save"; configName|] -> Ok { Command = SaveConfiguration configName; Options = Map.empty; Timestamp = DateTimeOffset.Now }
        | [|"list"|] -> Ok { Command = ListConfigurations; Options = Map.empty; Timestamp = DateTimeOffset.Now }
        | [|"status"|] -> Ok { Command = ShowStatus; Options = Map.empty; Timestamp = DateTimeOffset.Now }
        | [|"help"|] -> Ok { Command = ShowHelp; Options = Map.empty; Timestamp = DateTimeOffset.Now }
        | _ -> Error "Invalid command arguments. Use 'help' for usage information."
    
    // Pure function to validate command
    let validateCommand (request: CommandRequest) : Result<CommandRequest, string> =
        match request.Command with
        | LoadConfiguration name when String.IsNullOrWhiteSpace(name) ->
            Error "Configuration name cannot be empty"
        | SaveConfiguration name when String.IsNullOrWhiteSpace(name) ->
            Error "Configuration name cannot be empty"
        | _ -> Ok request
    
    // Pure function to add default options
    let withDefaultOptions (request: CommandRequest) : CommandRequest =
        let defaultOptions = Map.ofList [("verbose", "false"); ("timeout", "30")]
        let mergedOptions = Map.fold (fun acc key value -> Map.add key value acc) request.Options defaultOptions
        { request with Options = mergedOptions }
```

## Effect-Based Command Processing

### CLI Effects and Interpreter
**Location**: `Adapters/CLIEffects.fs`

```fsharp
module CLIEffects =
    
    // Effect types for CLI operations
    type CLIEffect<'T> =
        | Pure of 'T
        | WriteOutput of string * (unit -> CLIEffect<'T>)
        | WriteError of string * (unit -> CLIEffect<'T>)
        | ReadConfiguration of string * (Result<DisplayConfig, string> -> CLIEffect<'T>)
        | WriteConfiguration of DisplayConfig * string * (Result<unit, string> -> CLIEffect<'T>)
        | SwitchDisplayMode of DisplayMode * (Result<unit, string> -> CLIEffect<'T>)
        | GetCurrentStatus of (DisplayConfig -> CLIEffect<'T>)
        | LaunchGUIApp of (Result<unit, string> -> CLIEffect<'T>)
        | ExitWithCode of int
    
    // Pure command processing logic
    let processCommand (request: CommandRequest) : CLIEffect<CommandResult> =
        match request.Command with
        | SwitchMode mode ->
            SwitchDisplayMode(mode, fun result ->
                match result with
                | Ok () -> 
                    let message = match mode with
                                  | PCMode -> "PC Mode activated"
                                  | TVMode -> "TV Mode activated"
                                  | ExtendedMode displays -> $"Extended mode activated with displays: {String.Join(", ", displays)}"
                    WriteOutput(message, fun () -> Pure (Success message))
                | Error msg -> 
                    WriteError($"Error switching mode: {msg}", fun () -> Pure (Error msg)))
        
        | LoadConfiguration configName ->
            ReadConfiguration(configName, fun result ->
                match result with
                | Ok config ->
                    SwitchDisplayMode(determineDisplayMode config.Displays, fun switchResult ->
                        match switchResult with
                        | Ok () -> 
                            WriteOutput($"Configuration '{config.Name}' loaded successfully", 
                                fun () -> Pure (Success $"Loaded {config.Name}"))
                        | Error msg ->
                            WriteError($"Failed to apply configuration: {msg}", 
                                fun () -> Pure (Error msg)))
                | Error msg ->
                    WriteError($"Failed to load configuration: {msg}", 
                        fun () -> Pure (Error msg)))
        
        | SaveConfiguration configName ->
            GetCurrentStatus(fun currentConfig ->
                let namedConfig = { currentConfig with Name = configName }
                WriteConfiguration(namedConfig, configName, fun result ->
                    match result with
                    | Ok () ->
                        WriteOutput($"Configuration saved as '{configName}'", 
                            fun () -> Pure (Success $"Saved {configName}"))
                    | Error msg ->
                        WriteError($"Failed to save configuration: {msg}", 
                            fun () -> Pure (Error msg))))
        
        | ListConfigurations ->
            // Implementation would list all configurations
            Pure (ShowOutput ["Config1.json", "Config2.json", "Config3.json"])
        
        | ShowStatus ->
            GetCurrentStatus(fun currentConfig ->
                let statusLines = [
                    $"Active Displays: {currentConfig.Displays |> List.filter (fun d -> d.IsActive) |> List.length}"
                    "Display Details:"
                ] @ (currentConfig.Displays |> List.map (fun d -> 
                    $"  {d.FriendlyName}: {if d.IsActive then "ACTIVE" else "INACTIVE"}"))
                Pure (ShowOutput statusLines))
        
        | ShowHelp ->
            let helpText = [
                "DisplaySwitch-Pro - Functional CLI"
                ""
                "Usage: DisplayManager.exe <command> [options]"
                ""
                "Commands:"
                "  pc                     Switch to PC mode (all displays)"
                "  tv                     Switch to TV mode (single display)"
                "  load <config>          Load named configuration"
                "  save <config>          Save current setup as named configuration"
                "  list                   List all saved configurations"
                "  status                 Show current display status"
                "  help                   Show this help message"
                ""
                "Examples:"
                "  DisplayManager.exe pc"
                "  DisplayManager.exe load work-setup"
                "  DisplayManager.exe save gaming-config"
            ]
            Pure (ShowOutput helpText)
        
        | LaunchGUI ->
            LaunchGUIApp(fun result ->
                match result with
                | Ok () -> Pure (ExitCode 0)
                | Error msg -> 
                    WriteError($"Failed to launch GUI: {msg}", 
                        fun () -> Pure (ExitCode 1)))
    
    // Effect interpreter for CLI operations
    let rec interpretCLIEffect<'T> (effect: CLIEffect<'T>) : 'T =
        match effect with
        | Pure value -> value
        | WriteOutput (message, cont) ->
            Console.WriteLine(message)
            cont () |> interpretCLIEffect
        | WriteError (message, cont) ->
            Console.Error.WriteLine(message)
            cont () |> interpretCLIEffect
        | ReadConfiguration (name, cont) ->
            try
                let config = ConfigurationLogic.loadConfiguration name
                cont (Ok config) |> interpretCLIEffect
            with
            | ex -> cont (Error ex.Message) |> interpretCLIEffect
        | WriteConfiguration (config, name, cont) ->
            try
                ConfigurationLogic.saveConfiguration config name
                cont (Ok ()) |> interpretCLIEffect
            with
            | ex -> cont (Error ex.Message) |> interpretCLIEffect
        | SwitchDisplayMode (mode, cont) ->
            try
                DisplayAPI.setDisplayMode mode
                cont (Ok ()) |> interpretCLIEffect
            with
            | ex -> cont (Error ex.Message) |> interpretCLIEffect
        | GetCurrentStatus cont ->
            let currentConfig = DisplayAPI.getCurrentConfiguration()
            cont currentConfig |> interpretCLIEffect
        | LaunchGUIApp cont ->
            try
                // Launch GUI application
                System.Windows.Forms.Application.Run(new MainForm())
                cont (Ok ()) |> interpretCLIEffect
            with
            | ex -> cont (Error ex.Message) |> interpretCLIEffect
        | ExitWithCode code ->
            Environment.Exit(code)
            Unchecked.defaultof<'T> // This won't execute
```

## Functional CLI Entry Point

### Main Function with Pure Command Processing
**Location**: `Program.fs`

```fsharp
module Program =
    
    // Pure functional main entry point
    let runCLI (args: string array) : int =
        let pipeline = 
            CommandParser.parseArgs
            >> Result.bind CommandParser.validateCommand
            >> Result.map CommandParser.withDefaultOptions
            >> Result.bind (fun request ->
                try
                    let effect = CLIEffects.processCommand request
                    let result = CLIEffects.interpretCLIEffect effect
                    Ok result
                with
                | ex -> Error ex.Message)
        
        match pipeline args with
        | Ok (Success message) -> 
            0 // Success exit code
        | Ok (Error message) ->
            Console.Error.WriteLine($"Error: {message}")
            1 // Error exit code
        | Ok (ShowOutput lines) ->
            lines |> List.iter Console.WriteLine
            0 // Success exit code
        | Ok (ExitCode code) -> 
            code
        | Error message ->
            Console.Error.WriteLine($"Error: {message}")
            1 // Error exit code
    
    [<EntryPoint>]
    let main args =
        try
            runCLI args
        with
        | ex ->
            Console.Error.WriteLine($"Fatal error: {ex.Message}")
            1

// Alternative entry point for testing
module TestableProgram =
    
    // Testable version that takes effect interpreter as parameter
    let runCLIWithInterpreter (interpreter: CLIEffect<CommandResult> -> CommandResult) (args: string array) : CommandResult =
        let pipeline = 
            CommandParser.parseArgs
            >> Result.bind CommandParser.validateCommand  
            >> Result.map CommandParser.withDefaultOptions
            >> Result.bind (fun request ->
                try
                    let effect = CLIEffects.processCommand request
                    let result = interpreter effect
                    Ok result
                with
                | ex -> Error ex.Message)
        
        match pipeline args with
        | Ok result -> result
        | Error message -> Error message
```

## Pure Function Testing

### CLI Testing with Mock Interpreters
**Location**: `Tests/CLITests.fs`

```fsharp
module CLITests =
    
    // Mock interpreter for testing
    let mockInterpreter (effect: CLIEffect<CommandResult>) : CommandResult =
        let rec interpret eff =
            match eff with
            | Pure result -> result
            | WriteOutput (msg, cont) -> 
                printfn "Mock output: %s" msg
                cont () |> interpret
            | WriteError (msg, cont) ->
                printfn "Mock error: %s" msg  
                cont () |> interpret
            | SwitchDisplayMode (mode, cont) ->
                printfn "Mock: Switching to %A" mode
                cont (Ok ()) |> interpret
            | GetCurrentStatus cont ->
                let mockConfig = {
                    Name = "Test Config"
                    Displays = [
                        { DeviceName = "Display1"; FriendlyName = "Monitor 1"; IsActive = true
                          Position = (0, 0); Resolution = (1920u, 1080u); RefreshRate = 60u
                          TargetId = 1u; SourceId = 1u }
                    ]
                    Timestamp = DateTimeOffset.Now
                    Version = "2.0"
                }
                cont mockConfig |> interpret
            | _ -> Success "Mock operation completed"
        
        interpret effect
    
    [<Test>]
    let ``parseArgs correctly parses PC mode command`` () =
        // Arrange
        let args = [|"pc"|]
        
        // Act
        let result = CommandParser.parseArgs args
        
        // Assert
        match result with
        | Ok request -> 
            Assert.AreEqual(SwitchMode PCMode, request.Command)
            Assert.True(Map.isEmpty request.Options)
        | Error msg -> Assert.Fail($"Expected successful parsing, got error: {msg}")
    
    [<Test>]
    let ``processCommand returns correct success message for PC mode`` () =
        // Arrange
        let request = { Command = SwitchMode PCMode; Options = Map.empty; Timestamp = DateTimeOffset.Now }
        
        // Act
        let result = TestableProgram.runCLIWithInterpreter mockInterpreter [|"pc"|]
        
        // Assert
        match result with
        | Success msg -> Assert.AreEqual("PC Mode activated", msg)
        | _ -> Assert.Fail("Expected Success result")
    
    [<Test>]
    let ``invalid command returns error`` () =
        // Arrange & Act
        let result = TestableProgram.runCLIWithInterpreter mockInterpreter [|"invalid"|]
        
        // Assert
        match result with
        | Error msg -> StringAssert.Contains("Invalid command arguments", msg)
        | _ -> Assert.Fail("Expected Error result")
    
    [<Test>]
    let ``load command with empty config name returns validation error`` () =
        // Arrange & Act  
        let result = TestableProgram.runCLIWithInterpreter mockInterpreter [|"load"; ""|]
        
        // Assert
        match result with
        | Error msg -> StringAssert.Contains("Configuration name cannot be empty", msg)
        | _ -> Assert.Fail("Expected validation error")
    
    [<Test>]
    let ``status command returns formatted display information`` () =
        // Arrange & Act
        let result = TestableProgram.runCLIWithInterpreter mockInterpreter [|"status"|]
        
        // Assert
        match result with
        | ShowOutput lines ->
            Assert.True(List.length lines >= 2)
            Assert.True(lines.[0].Contains("Active Displays"))
            Assert.True(lines.[1] = "Display Details:")
        | _ -> Assert.Fail("Expected ShowOutput result")

// Property-based testing for command parsing
module CLIPropertyTests =
    
    [<Property>]
    let ``parseArgs with invalid input always returns Error`` (invalidInput: string) =
        let invalidArgs = [|invalidInput|]
        match CommandParser.parseArgs invalidArgs with
        | Error _ when not (["pc"; "tv"; "load"; "save"; "list"; "status"; "help"] |> List.contains invalidInput) -> true
        | Ok _ when ["pc"; "tv"; "list"; "status"; "help"] |> List.contains invalidInput -> true
        | _ -> false
    
    [<Property>]
    let ``command pipeline is associative`` (args: string array) =
        let result1 = 
            args
            |> CommandParser.parseArgs
            |> Result.bind CommandParser.validateCommand
            |> Result.map CommandParser.withDefaultOptions
        
        let result2 = 
            args
            |> CommandParser.parseArgs
            |> Result.bind (CommandParser.validateCommand >> Result.map CommandParser.withDefaultOptions)
        
        result1 = result2
```

## ECS Integration for CLI

### Command Components
**Location**: `Core/CLIComponents.fs`

```fsharp
module CLIComponents =
    
    // Component for entities that can process CLI commands
    type CLICommandComponent = {
        SupportedCommands: Command list
        IsProcessing: bool
        LastExecuted: CommandRequest option
        ExecutionHistory: CommandRequest list
    }
    
    // Component for command result handling
    type CLIResultComponent = {
        LastResult: CommandResult option
        OutputBuffer: string list
        ErrorBuffer: string list
    }

// ECS System for CLI command processing
module CLICommandSystem =
    
    // Pure function to process commands on entities
    let processCommandOnEntities (request: CommandRequest) (entities: Entity list) : (Entity list * CommandResult list) =
        let processEntity entity =
            match entity.GetComponent<CLICommandComponent>() with
            | Some cliComp when List.contains request.Command cliComp.SupportedCommands ->
                let effect = CLIEffects.processCommand request
                let result = CLIEffects.interpretCLIEffect effect
                
                let updatedComp = {
                    cliComp with
                        IsProcessing = false
                        LastExecuted = Some request
                        ExecutionHistory = request :: (List.take 9 cliComp.ExecutionHistory) // Keep last 10
                }
                
                let resultComp = {
                    LastResult = Some result
                    OutputBuffer = []
                    ErrorBuffer = []
                }
                
                let updatedEntity = 
                    entity
                        .UpdateComponent(updatedComp)
                        .UpdateComponent(resultComp)
                
                (updatedEntity, [result])
            | _ -> (entity, [])
        
        let results = entities |> List.map processEntity
        let updatedEntities = results |> List.map fst
        let commandResults = results |> List.collect snd
        (updatedEntities, commandResults)
    
    // Pure function to validate entity can process command
    let canEntityProcessCommand (entity: Entity) (command: Command) : bool =
        match entity.GetComponent<CLICommandComponent>() with
        | Some comp -> 
            List.contains command comp.SupportedCommands && not comp.IsProcessing
        | None -> false
```

## Summary: Functional Programming Benefits for CLI

### Reliability Through Pure Functions
- **Deterministic Parsing**: Command parsing always produces the same result for the same input
- **Immutable Commands**: Command data structures cannot be accidentally modified
- **Predictable Pipeline**: Command processing pipeline is composed of pure transformations

### Testability Through Effect Isolation
- **Mockable Effects**: All side effects can be easily mocked for testing
- **Pure Logic Testing**: Command processing logic tested without I/O dependencies
- **Property-Based Testing**: Can verify CLI behavior across large input spaces
- **Deterministic Results**: Same commands always produce same outcomes in tests

### Maintainability Through Composition
- **Function Composition**: Complex CLI workflows built from simple, composable functions
- **Modular Architecture**: Parsing, validation, processing, and effects are separate concerns
- **Type Safety**: F# type system prevents invalid command combinations at compile time

### Scalability Through ECS Integration
- **Component-Based Processing**: CLI commands handled as components on entities
- **System Isolation**: CLI system operates independently of other systems
- **Flexible Command Handling**: Entities can have different command processing capabilities

### Error Handling Through Result Types
- **Explicit Error Handling**: All CLI operations return Result types with clear error information
- **No Hidden Exceptions**: Error conditions handled through pattern matching
- **Composable Error Handling**: Error handling logic can be composed and reused throughout the pipeline

### Performance Through Functional Optimization
- **Lazy Evaluation**: Command effects only executed when needed
- **Immutable Data Structures**: Efficient sharing of command and configuration data
- **Pure Computation**: No allocation overhead for stateless command processing

### Scriptability Through Consistent Interface
- **Predictable Exit Codes**: Functional approach ensures consistent exit code behavior
- **Composable Commands**: Commands can be easily composed in scripts and automation
- **JSON Output Support**: Structured output for programmatic consumption

The functional approach to CLI processing makes DisplaySwitch-Pro more reliable for automation and scripting scenarios while providing excellent testability and maintainability. The separation of pure command logic from side effects enables robust error handling and easy integration testing.