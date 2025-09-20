namespace DisplaySwitchPro

open System
open System.IO
open System.Text.Json
open System.Threading
open System.Collections.Concurrent

/// Enhanced error types for persistence operations with detailed context
type PersistenceError =
    | ValidationFailed of ValidationError list
    | SerializationFailed of string * InnerException: Exception option
    | BackupFailed of string * InnerException: Exception option
    | WriteFailed of string * InnerException: Exception option
    | ReadFailed of string * InnerException: Exception option
    | VerificationFailed of string
    | FileLockTimeout of filePath: string * timeoutMs: int
    | DirectoryCreationFailed of string * InnerException: Exception option

and ValidationError =
    | EmptyDisplayList
    | NoPrimaryDisplay
    | MultiplePrimaryDisplays of DisplayId list
    | InvalidDisplayPosition of DisplayId * Position
    | DuplicateDisplayId of DisplayId
    | UnsupportedResolution of DisplayId * Resolution
    | OverlappingDisplays of (DisplayId * DisplayId) list
    | InvalidPresetName of string
    | EmptyConfiguration

/// Async file operations with functional composition and enhanced error handling
module AsyncFileOperations =

    /// Thread-safe file locking mechanism using semaphores per file path
    let private fileLocks = ConcurrentDictionary<string, SemaphoreSlim>()

    /// Execute an async operation with exclusive file lock
    let withFileLock (filePath: string) (timeoutMs: int) (operation: unit -> Async<Result<'T, PersistenceError>>) = async {
        let semaphore = fileLocks.GetOrAdd(filePath, fun _ -> new SemaphoreSlim(1, 1))
        let! lockAcquired = semaphore.WaitAsync(timeoutMs) |> Async.AwaitTask

        if not lockAcquired then
            return Error (FileLockTimeout(filePath, timeoutMs))
        else
            try
                return! operation()
            finally
                semaphore.Release() |> ignore
    }

    /// Read file content asynchronously with error handling
    let readFileAsync (filePath: string) = async {
        try
            let! content = File.ReadAllTextAsync(filePath) |> Async.AwaitTask
            return Ok content
        with ex ->
            return Error (ReadFailed(sprintf "Failed to read file: %s" filePath, Some ex))
    }

    /// Write file content atomically using temporary file approach
    let writeFileAtomicAsync (filePath: string) (content: string) = async {
        let tempPath = sprintf "%s.tmp.%s" filePath (Guid.NewGuid().ToString("N"))
        try
            // Ensure directory exists
            let directory = Path.GetDirectoryName(filePath)
            if not (Directory.Exists(directory)) then
                try
                    Directory.CreateDirectory(directory) |> ignore
                with ex ->
                    return Error (DirectoryCreationFailed(directory, Some ex))

            // Write to temporary file
            do! File.WriteAllTextAsync(tempPath, content) |> Async.AwaitTask

            // Verify write success
            let writtenInfo = FileInfo(tempPath)
            if writtenInfo.Length = 0L && not (String.IsNullOrEmpty(content)) then
                return Error (WriteFailed("Temporary file is empty after write", None))
            else
                // Atomic move to final location
                if File.Exists(filePath) then
                    File.Delete(filePath)
                File.Move(tempPath, filePath)
                return Ok ()
        with ex ->
            // Clean up temporary file on error
            if File.Exists(tempPath) then
                try File.Delete(tempPath) with _ -> ()
            return Error (WriteFailed(sprintf "Failed to write file: %s" filePath, Some ex))
    }

    /// Copy file asynchronously with integrity verification
    let copyFileAsync (source: string) (destination: string) = async {
        try
            let! sourceBytes = File.ReadAllBytesAsync(source) |> Async.AwaitTask
            do! File.WriteAllBytesAsync(destination, sourceBytes) |> Async.AwaitTask

            // Verify copy integrity by comparing file sizes
            let sourceInfo = FileInfo(source)
            let destInfo = FileInfo(destination)
            if sourceInfo.Length <> destInfo.Length then
                return Error (VerificationFailed(sprintf "File size mismatch: source=%d, destination=%d" sourceInfo.Length destInfo.Length))
            else
                return Ok ()
        with ex ->
            return Error (WriteFailed(sprintf "Failed to copy file from %s to %s" source destination, Some ex))
    }

    /// Create timestamped backup of file if it exists
    let createBackupAsync (filePath: string) = async {
        try
            if File.Exists(filePath) then
                let timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss")
                let backupPath = sprintf "%s.backup.%s" filePath timestamp
                let! copyResult = copyFileAsync filePath backupPath
                match copyResult with
                | Ok () -> return Ok (Some backupPath)
                | Error e -> return Error (BackupFailed(sprintf "Failed to create backup for %s" filePath, None))
            else
                return Ok None  // No file to backup
        with ex ->
            return Error (BackupFailed(sprintf "Exception creating backup for %s" filePath, Some ex))
    }

    /// Enhanced file existence check with error handling
    let fileExistsAsync (filePath: string) = async {
        try
            return Ok (File.Exists(filePath))
        with ex ->
            return Error (ReadFailed(sprintf "Failed to check file existence: %s" filePath, Some ex))
    }

    /// Get file information asynchronously
    let getFileInfoAsync (filePath: string) = async {
        try
            if File.Exists(filePath) then
                let info = FileInfo(filePath)
                return Ok (Some {| Size = info.Length; LastModified = info.LastWriteTime; Created = info.CreationTime |})
            else
                return Ok None
        with ex ->
            return Error (ReadFailed(sprintf "Failed to get file info: %s" filePath, Some ex))
    }

/// Functional composition operators for async Result operations
module AsyncResultComposition =

    /// Async bind operator for Result chaining (Kleisli composition)
    let (>=>) (f: 'a -> Async<Result<'b, 'e>>) (g: 'b -> Async<Result<'c, 'e>>) =
        fun x -> async {
            let! result = f x
            match result with
            | Ok value -> return! g value
            | Error e -> return Error e
        }

    /// Async Result bind (monadic bind)
    let (>>=) (asyncResult: Async<Result<'a, 'e>>) (f: 'a -> Async<Result<'b, 'e>>) = async {
        let! result = asyncResult
        match result with
        | Ok value -> return! f value
        | Error e -> return Error e
    }

    /// Map over successful result value
    let mapAsync (f: 'a -> 'b) (asyncResult: Async<Result<'a, 'e>>) = async {
        let! result = asyncResult
        return Result.map f result
    }

    /// Map over error value
    let mapErrorAsync (f: 'e -> 'f) (asyncResult: Async<Result<'a, 'e>>) = async {
        let! result = asyncResult
        return Result.mapError f result
    }

    /// Traverse async operations over a list, collecting all results
    let traverseAsync (f: 'a -> Async<Result<'b, 'e>>) (list: 'a list) = async {
        let mutable results = []
        let mutable errors = []

        for item in list do
            let! result = f item
            match result with
            | Ok value -> results <- value :: results
            | Error e -> errors <- e :: errors

        if List.isEmpty errors then
            return Ok (List.rev results)
        else
            return Error (List.rev errors)
    }

    /// Sequence async Result operations, stopping at first error
    let sequenceAsync (asyncResults: Async<Result<'a, 'e>> list) = async {
        let mutable results = []
        let mutable error = None

        for asyncResult in asyncResults do
            if Option.isNone error then
                let! result = asyncResult
                match result with
                | Ok value -> results <- value :: results
                | Error e -> error <- Some e

        match error with
        | None -> return Ok (List.rev results)
        | Some e -> return Error e
    }

/// File operations specifically for preset management with enhanced validation
module PresetFileOperations =

    open AsyncFileOperations
    open AsyncResultComposition

    /// Serialize preset data to JSON with error handling
    let serializePresets (presets: Map<string, DisplayConfiguration>) = async {
        try
            let presetArray = presets |> Map.values |> Seq.toArray
            let options = JsonSerializerOptions(WriteIndented = true)
            // Add converters if needed
            let json = JsonSerializer.Serialize(presetArray, options)
            return Ok json
        with ex ->
            return Error (SerializationFailed("Failed to serialize presets", Some ex))
    }

    /// Deserialize preset data from JSON with validation
    let deserializePresets (json: string) = async {
        try
            if String.IsNullOrWhiteSpace(json) then
                return Ok Map.empty
            else
                let options = JsonSerializerOptions()
                // Add converters if needed
                let presetArray = JsonSerializer.Deserialize<DisplayConfiguration[]>(json, options)
                if presetArray = null then
                    return Error (SerializationFailed("Deserialized preset array is null", None))
                else
                    let presetMap =
                        presetArray
                        |> Array.fold (fun acc preset -> Map.add preset.Name preset acc) Map.empty
                    return Ok presetMap
        with ex ->
            return Error (SerializationFailed("Failed to deserialize presets", Some ex))
    }

    /// Load presets from file with full error handling and backup recovery
    let loadPresetsAsync (filePath: string) = async {
        let! fileExists = fileExistsAsync filePath
        match fileExists with
        | Error e -> return Error e
        | Ok false -> return Ok Map.empty  // No file exists, return empty
        | Ok true ->
            let! content = readFileAsync filePath
            match content with
            | Error e ->
                // Try backup recovery
                let backupPath = filePath + ".backup"
                let! backupExists = fileExistsAsync backupPath
                match backupExists with
                | Ok true ->
                    let! backupContent = readFileAsync backupPath
                    match backupContent with
                    | Ok json -> return! deserializePresets json
                    | Error _ -> return Error e  // Return original error
                | _ -> return Error e
            | Ok json -> return! deserializePresets json
    }

    /// Save presets to file with atomic writes and backup creation
    let savePresetsAsync (filePath: string) (presets: Map<string, DisplayConfiguration>) = async {
        // Create backup first
        let! backupResult = createBackupAsync filePath
        match backupResult with
        | Error e -> return Error e
        | Ok _ ->
            // Serialize presets
            let! serialized = serializePresets presets
            match serialized with
            | Error e -> return Error e
            | Ok json ->
                // Write atomically with file lock
                return! withFileLock filePath 5000 (fun () -> writeFileAtomicAsync filePath json)
    }

    /// Validate preset configuration with enhanced error reporting
    let validatePresetConfiguration (preset: DisplayConfiguration) = async {
        let errors = System.Collections.Generic.List<ValidationError>()

        // Check basic structure
        if String.IsNullOrWhiteSpace(preset.Name) then
            errors.Add(InvalidPresetName preset.Name)

        if List.isEmpty preset.Displays then
            errors.Add(EmptyDisplayList)
        else
            // Check for primary display requirements
            let primaryDisplays = preset.Displays |> List.filter (fun d -> d.IsPrimary)
            match primaryDisplays.Length with
            | 0 -> errors.Add(NoPrimaryDisplay)
            | 1 -> () // Correct
            | _ -> errors.Add(MultiplePrimaryDisplays (primaryDisplays |> List.map (fun d -> d.Id)))

            // Check for duplicate display IDs
            let duplicateIds =
                preset.Displays
                |> List.groupBy (fun d -> d.Id)
                |> List.filter (fun (_, group) -> List.length group > 1)
                |> List.map fst

            for duplicateId in duplicateIds do
                errors.Add(DuplicateDisplayId duplicateId)

        if errors.Count = 0 then
            return Ok preset
        else
            return Error (ValidationFailed (errors |> Seq.toList))
    }

    /// Batch validate multiple presets
    let validatePresetsAsync (presets: Map<string, DisplayConfiguration>) = async {
        let validationTasks =
            presets
            |> Map.toList
            |> List.map (fun (name, preset) -> async {
                let! result = validatePresetConfiguration preset
                return (name, result)
            })

        let! results = Async.Parallel validationTasks

        let validPresets =
            results
            |> Array.choose (fun (name, result) ->
                match result with
                | Ok preset -> Some (name, preset)
                | Error _ -> None)
            |> Map.ofArray

        let validationErrors =
            results
            |> Array.choose (fun (name, result) ->
                match result with
                | Error (ValidationFailed errors) -> Some (name, errors)
                | Error e -> Some (name, [InvalidPresetName name])
                | Ok _ -> None)
            |> Array.toList

        return (validPresets, validationErrors)
    }