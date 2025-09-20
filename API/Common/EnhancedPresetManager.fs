namespace DisplaySwitchPro

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open AsyncFileOperations
open AsyncResultComposition
open EnhancedCache
open PresetMetadata

/// Enhanced preset manager with async operations, caching, and advanced organization
module EnhancedPresetManager =

    /// Cache policy for preset caching
    let private defaultCachePolicy = {
        MaxAge = Some (TimeSpan.FromHours(1.0))
        MaxEntries = 100
        MaxMemorySize = 50L * 1024L * 1024L  // 50 MB
        WriteDelay = TimeSpan.FromSeconds(5.0)
        PersistencePath = None
        EvictionStrategy = Hybrid
        WriteStrategy = WriteBehind
    }

    /// Global preset cache
    let private presetCache = ref (EnhancedCache.create defaultCachePolicy)

    /// Configuration for enhanced preset manager
    type EnhancedPresetConfig = {
        PresetsFilePath: string
        CachePolicy: CachePolicy
        AutoSaveEnabled: bool
        AutoSaveInterval: TimeSpan
        BackupRetentionDays: int
        EnableUsageAnalytics: bool
        EnableCompatibilityChecking: bool
    }

    /// Create default configuration
    let createDefaultConfig() = {
        PresetsFilePath =
            let appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplaySwitch-Pro")
            Directory.CreateDirectory(appFolder) |> ignore
            Path.Combine(appFolder, "enhanced-presets.json")
        CachePolicy = defaultCachePolicy
        AutoSaveEnabled = true
        AutoSaveInterval = TimeSpan.FromMinutes(5.0)
        BackupRetentionDays = 30
        EnableUsageAnalytics = true
        EnableCompatibilityChecking = true
    }

    /// JSON converter for enhanced display configurations
    type EnhancedDisplayConfigurationJsonConverter() =
        inherit JsonConverter<EnhancedDisplayConfiguration>()

        override _.Read(reader: byref<Utf8JsonReader>, typeToConvert: Type, options: JsonSerializerOptions) =
            use doc = JsonDocument.ParseValue(&reader)
            let root = doc.RootElement

            // Extract configuration
            let configElement = root.GetProperty("configuration")
            let config = JsonSerializer.Deserialize<DisplayConfiguration>(configElement.GetRawText(), options)

            // Extract metadata with defaults for missing fields
            let metadataElement = root.GetProperty("metadata")
            let category =
                if metadataElement.TryGetProperty("category", &Unchecked.defaultof<JsonElement>) then
                    JsonSerializer.Deserialize<PresetCategory>(metadataElement.GetProperty("category").GetRawText(), options)
                else Work

            let tags =
                if metadataElement.TryGetProperty("tags", &Unchecked.defaultof<JsonElement>) then
                    metadataElement.GetProperty("tags").EnumerateArray()
                    |> Seq.map (fun el -> el.GetString())
                    |> Set.ofSeq
                else Set.empty

            let metadata = {
                Category = category
                Tags = tags
                Description = if metadataElement.TryGetProperty("description", &Unchecked.defaultof<JsonElement>) then Some (metadataElement.GetProperty("description").GetString()) else None
                LastUsed = if metadataElement.TryGetProperty("lastUsed", &Unchecked.defaultof<JsonElement>) then Some (metadataElement.GetProperty("lastUsed").GetDateTime()) else None
                UsageCount = if metadataElement.TryGetProperty("usageCount", &Unchecked.defaultof<JsonElement>) then metadataElement.GetProperty("usageCount").GetInt32() else 0
                IsFavorite = if metadataElement.TryGetProperty("isFavorite", &Unchecked.defaultof<JsonElement>) then metadataElement.GetProperty("isFavorite").GetBoolean() else false
                Author = if metadataElement.TryGetProperty("author", &Unchecked.defaultof<JsonElement>) then Some (metadataElement.GetProperty("author").GetString()) else None
                Version = if metadataElement.TryGetProperty("version", &Unchecked.defaultof<JsonElement>) then metadataElement.GetProperty("version").GetString() else "1.0"
                CreatedAt = if metadataElement.TryGetProperty("createdAt", &Unchecked.defaultof<JsonElement>) then metadataElement.GetProperty("createdAt").GetDateTime() else DateTime.Now
                ModifiedAt = if metadataElement.TryGetProperty("modifiedAt", &Unchecked.defaultof<JsonElement>) then metadataElement.GetProperty("modifiedAt").GetDateTime() else DateTime.Now
                CompatibilityInfo = PresetOrganization.defaultCompatibilityInfo
                UsageAnalytics = PresetOrganization.defaultUsageAnalytics
                CustomProperties = Map.empty
            }

            {
                Configuration = config
                Metadata = metadata
                ValidationReport = None
                Hash = None
            }

        override _.Write(writer: Utf8JsonWriter, value: EnhancedDisplayConfiguration, options: JsonSerializerOptions) =
            writer.WriteStartObject()
            writer.WritePropertyName("configuration")
            JsonSerializer.Serialize(writer, value.Configuration, options)
            writer.WritePropertyName("metadata")
            JsonSerializer.Serialize(writer, value.Metadata, options)
            if value.ValidationReport.IsSome then
                writer.WritePropertyName("validationReport")
                JsonSerializer.Serialize(writer, value.ValidationReport.Value, options)
            if value.Hash.IsSome then
                writer.WritePropertyName("hash")
                writer.WriteStringValue(value.Hash.Value)
            writer.WriteEndObject()

    /// Enhanced JSON serializer options
    let private getJsonSerializerOptions() =
        let options = JsonSerializerOptions(WriteIndented = true)
        options.Converters.Add(EnhancedDisplayConfigurationJsonConverter())
        options

    /// Async preset operations with caching and validation
    module AsyncPresetOperations =

        /// Load presets from disk with caching
        let loadPresetsAsync (config: EnhancedPresetConfig) = async {
            // Check cache first
            let (cachedPresets, updatedCache) = EnhancedCache.get "all_presets" !presetCache
            presetCache := updatedCache

            match cachedPresets with
            | Some presets -> return Ok presets
            | None ->
                // Load from disk
                let! loadResult = PresetFileOperations.loadPresetsAsync config.PresetsFilePath
                match loadResult with
                | Ok basicPresets ->
                    // Convert to enhanced presets with default metadata
                    let enhancedPresets =
                        basicPresets
                        |> Map.map (fun name config ->
                            let metadata = PresetOrganization.createDefaultMetadata Work
                            PresetOrganization.createEnhancedConfiguration config metadata)

                    // Cache the result
                    presetCache := EnhancedCache.put "all_presets" enhancedPresets !presetCache
                    return Ok enhancedPresets
                | Error e -> return Error e
        }

        /// Save presets to disk with caching
        let savePresetsAsync (config: EnhancedPresetConfig) (presets: Map<string, EnhancedDisplayConfiguration>) = async {
            // Convert enhanced presets back to basic configurations for compatibility
            let basicPresets =
                presets
                |> Map.map (fun _ enhanced -> enhanced.Configuration)

            let! saveResult = PresetFileOperations.savePresetsAsync config.PresetsFilePath basicPresets
            match saveResult with
            | Ok () ->
                // Update cache
                presetCache := EnhancedCache.put "all_presets" presets !presetCache
                return Ok ()
            | Error e -> return Error e
        }

        /// Create preset from current display state with enhanced metadata
        let createPresetFromCurrentStateAsync (name: string) (category: PresetCategory) (tags: string list) (description: string option) = async {
            try
                let displays = DisplayDetection.getConnectedDisplays()
                let config = DisplayHelpers.createDisplayConfiguration name displays

                let metadata = {
                    PresetOrganization.createDefaultMetadata category with
                        Tags = Set.ofList tags
                        Description = description
                }

                let enhancedConfig = PresetOrganization.createEnhancedConfiguration config metadata

                // Validate the preset
                let! validationResult = PresetFileOperations.validatePresetConfiguration config
                match validationResult with
                | Ok validatedConfig ->
                    return Ok { enhancedConfig with Configuration = validatedConfig }
                | Error (ValidationFailed errors) ->
                    // Create validation report
                    let validationReport = {
                        IsValid = false
                        Errors = errors
                        Warnings = []
                        Suggestions = []
                        CompatibilityScore = 0.0
                        LastValidated = DateTime.Now
                    }
                    return Ok { enhancedConfig with ValidationReport = Some validationReport }
                | Error e ->
                    return Error e
            with ex ->
                return Error (SerializationFailed("Failed to create preset from current state", Some ex))
        }

        /// Apply preset with usage tracking
        let applyPresetAsync (config: EnhancedPresetConfig) (presetName: string) (preset: EnhancedDisplayConfiguration) = async {
            let startTime = DateTime.Now

            try
                // Apply the preset using existing logic
                match PresetManager.applyPreset preset.Configuration with
                | Ok () ->
                    let duration = DateTime.Now - startTime

                    if config.EnableUsageAnalytics then
                        // Update usage analytics
                        let updatedMetadata = PresetOrganization.updateUsageMetadata preset.Metadata true (Some duration) None
                        let updatedPreset = { preset with Metadata = updatedMetadata }

                        // Load current presets, update, and save
                        let! currentPresets = loadPresetsAsync config
                        match currentPresets with
                        | Ok presets ->
                            let updatedPresets = Map.add presetName updatedPreset presets
                            let! saveResult = savePresetsAsync config updatedPresets
                            match saveResult with
                            | Ok () -> return Ok updatedPreset
                            | Error e -> return Ok updatedPreset  // Still return success even if save fails
                        | Error _ -> return Ok updatedPreset  // Still return success
                    else
                        return Ok preset

                | Error errorMsg ->
                    let duration = DateTime.Now - startTime

                    if config.EnableUsageAnalytics then
                        // Update usage analytics with failure
                        let updatedMetadata = PresetOrganization.updateUsageMetadata preset.Metadata false (Some duration) (Some errorMsg)
                        let updatedPreset = { preset with Metadata = updatedMetadata }

                        // Load current presets, update, and save
                        let! currentPresets = loadPresetsAsync config
                        match currentPresets with
                        | Ok presets ->
                            let updatedPresets = Map.add presetName updatedPreset presets
                            let! _ = savePresetsAsync config updatedPresets
                            return Error (WriteFailed(errorMsg, None))
                        | Error _ -> return Error (WriteFailed(errorMsg, None))
                    else
                        return Error (WriteFailed(errorMsg, None))

            with ex ->
                let duration = DateTime.Now - startTime
                if config.EnableUsageAnalytics then
                    let updatedMetadata = PresetOrganization.updateUsageMetadata preset.Metadata false (Some duration) (Some ex.Message)
                    let updatedPreset = { preset with Metadata = updatedMetadata }
                    return Error (WriteFailed(ex.Message, Some ex))
                else
                    return Error (WriteFailed(ex.Message, Some ex))
        }

        /// Delete preset with cache invalidation
        let deletePresetAsync (config: EnhancedPresetConfig) (presetName: string) = async {
            let! currentPresets = loadPresetsAsync config
            match currentPresets with
            | Ok presets ->
                if Map.containsKey presetName presets then
                    let updatedPresets = Map.remove presetName presets
                    let! saveResult = savePresetsAsync config updatedPresets
                    match saveResult with
                    | Ok () -> return Ok updatedPresets
                    | Error e -> return Error e
                else
                    return Error (ValidationFailed [InvalidPresetName presetName])
            | Error e -> return Error e
        }

        /// Export presets with enhanced metadata
        let exportPresetsAsync (presets: Map<string, EnhancedDisplayConfiguration>) = async {
            try
                let options = getJsonSerializerOptions()
                let json = JsonSerializer.Serialize(presets, options)
                return Ok json
            with ex ->
                return Error (SerializationFailed("Failed to export presets", Some ex))
        }

        /// Import presets with validation and conflict resolution
        let importPresetsAsync (config: EnhancedPresetConfig) (json: string) (conflictResolution: ConflictResolution) = async {
            try
                if String.IsNullOrWhiteSpace(json) then
                    return Error (ValidationFailed [EmptyConfiguration])
                else
                    let options = getJsonSerializerOptions()
                    let importedPresets = JsonSerializer.Deserialize<Map<string, EnhancedDisplayConfiguration>>(json, options)

                    // Validate imported presets
                    let validationTasks =
                        importedPresets
                        |> Map.toList
                        |> List.map (fun (name, preset) -> async {
                            let! result = PresetFileOperations.validatePresetConfiguration preset.Configuration
                            return (name, preset, result)
                        })

                    let! validationResults = Async.Parallel validationTasks

                    let validPresets =
                        validationResults
                        |> Array.choose (fun (name, preset, result) ->
                            match result with
                            | Ok _ -> Some (name, preset)
                            | Error _ -> None)
                        |> Map.ofArray

                    if Map.isEmpty validPresets then
                        return Error (ValidationFailed [EmptyConfiguration])
                    else
                        // Load current presets and merge
                        let! currentPresets = loadPresetsAsync config
                        match currentPresets with
                        | Ok existing ->
                            let mergedPresets =
                                match conflictResolution with
                                | OverwriteExisting -> Map.fold (fun acc key value -> Map.add key value acc) existing validPresets
                                | KeepExisting -> Map.fold (fun acc key value -> if Map.containsKey key acc then acc else Map.add key value acc) existing validPresets
                                | RenameImported ->
                                    validPresets
                                    |> Map.fold (fun acc key value ->
                                        let finalKey = if Map.containsKey key acc then sprintf "%s_imported" key else key
                                        Map.add finalKey value acc) existing

                            let! saveResult = savePresetsAsync config mergedPresets
                            match saveResult with
                            | Ok () -> return Ok (mergedPresets, validPresets.Count)
                            | Error e -> return Error e
                        | Error e -> return Error e

            with ex ->
                return Error (SerializationFailed("Failed to import presets", Some ex))
        }

    and ConflictResolution =
        | OverwriteExisting
        | KeepExisting
        | RenameImported

    /// Background maintenance operations
    module BackgroundMaintenance =

        /// Clean up old backups
        let cleanupOldBackups (config: EnhancedPresetConfig) = async {
            try
                let backupDirectory = Path.GetDirectoryName(config.PresetsFilePath)
                let baseFileName = Path.GetFileNameWithoutExtension(config.PresetsFilePath)
                let backupPattern = sprintf "%s.backup.*" baseFileName

                let cutoffDate = DateTime.Now.AddDays(-float config.BackupRetentionDays)

                let backupFiles = Directory.GetFiles(backupDirectory, backupPattern)
                let oldBackups =
                    backupFiles
                    |> Array.filter (fun file ->
                        let fileInfo = FileInfo(file)
                        fileInfo.CreationTime < cutoffDate)

                for backupFile in oldBackups do
                    File.Delete(backupFile)

                return Ok oldBackups.Length
            with ex ->
                return Error (sprintf "Failed to cleanup old backups: %s" ex.Message)
        }

        /// Flush cache pending writes
        let flushCacheWrites (config: EnhancedPresetConfig) = async {
            let writeFunction key value = async {
                let! saveResult = AsyncPresetOperations.savePresetsAsync config value
                match saveResult with
                | Ok () -> return Ok ()
                | Error e -> return Error e
            }

            let! flushResult = WriteBehindCache.flushPendingWrites !presetCache writeFunction
            match flushResult with
            | Ok updatedCache ->
                presetCache := updatedCache
                return Ok ()
            | Error e -> return Error e
        }

        /// Optimize cache performance
        let optimizeCache() = async {
            presetCache := EnhancedCache.cleanupExpired !presetCache
            return Ok (EnhancedCache.getStatistics !presetCache)
        }

    /// Statistics and monitoring
    module Statistics =

        /// Get comprehensive preset statistics
        let getPresetStatistics (presets: Map<string, EnhancedDisplayConfiguration>) =
            PresetAnalytics.generateUsageReport presets

        /// Get cache statistics
        let getCacheStatistics() =
            EnhancedCache.getStatistics !presetCache

        /// Get system health information
        let getSystemHealth (config: EnhancedPresetConfig) = async {
            let cacheStats = getCacheStatistics()
            let sizeInfo = EnhancedCache.getSizeInfo !presetCache

            let! fileInfo = AsyncFileOperations.getFileInfoAsync config.PresetsFilePath
            let fileSize = match fileInfo with | Ok (Some info) -> Some info.Size | _ -> None

            return {|
                CacheHealth = {|
                    HitRatio = cacheStats.HitRatio
                    MemoryUtilization = sizeInfo.MemoryUtilization
                    EntryUtilization = sizeInfo.EntryUtilization
                    PendingWrites = (!presetCache).PendingWrites.Count
                |}
                FileSystem = {|
                    PresetsFileSize = fileSize
                    PresetsFileExists = fileInfo |> Result.map Option.isSome |> Result.defaultValue false
                |}
                Performance = {|
                    AverageAccessTime = cacheStats.AverageAccessTime
                    TotalRequests = cacheStats.TotalRequests
                    WriteOperations = cacheStats.WriteOperations
                |}
            |}
        }