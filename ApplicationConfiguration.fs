namespace DisplaySwitchPro

open System
open System.IO
open System.Text.Json
open System.Threading
open System.Collections.Generic
open ApplicationState

/// Comprehensive Application Configuration Management
/// Provides hierarchical configuration with validation, hot reload, and user preferences
module ApplicationConfiguration =

    // ===== Configuration Types =====

    /// User preference management
    type UserPreferences = {
        StartupBehavior: StartupBehavior
        NotificationSettings: NotificationSettings
        DisplayDetectionSettings: DisplayDetectionSettings
        UIPreferences: UIPreferences
        AdvancedSettings: AdvancedSettings
        TraySettings: TraySettings
        LastModified: DateTime
        Version: string
    }

    and StartupBehavior =
        | ShowMainWindow
        | MinimizeToTray
        | ApplyLastConfiguration of presetName: string
        | ApplySpecificPreset of presetName: string
        | RestoreLastSession

    and NotificationSettings = {
        ShowDisplayChangeNotifications: bool
        ShowPresetApplicationNotifications: bool
        ShowErrorNotifications: bool
        PlaySoundOnSuccess: bool
        NotificationDurationSeconds: int
        NotificationPosition: NotificationPosition
    }

    and NotificationPosition =
        | TopRight
        | TopLeft
        | BottomRight
        | BottomLeft
        | Center

    and DisplayDetectionSettings = {
        AutoRefreshInterval: TimeSpan option
        DetectHotplugEvents: bool
        ValidateDisplayCapabilities: bool
        RetryFailedDetections: bool
        MaxRetryAttempts: int
        DetectionTimeout: TimeSpan
        EnableBackgroundDetection: bool
    }

    and UIPreferences = {
        Theme: Theme.Theme
        WindowSize: float * float
        WindowPosition: (float * float) option
        ShowAdvancedControls: bool
        RememberWindowState: bool
        AutoHideInactiveDisplays: bool
        EnableAnimations: bool
        ScalingFactor: float
        Language: string
    }

    and AdvancedSettings = {
        LogLevel: LogLevel
        EnableDebugMode: bool
        EnablePerformanceLogging: bool
        CacheSettings: CacheSettings
        PerformanceSettings: PerformanceSettings
        ExperimentalFeatures: Set<string>
        CustomHotkeys: Map<string, KeyBinding>
    }

    and PerformanceSettings = {
        MaxConcurrentOperations: int
        UIRefreshRateHz: int
        StateUpdateBatchSize: int
        EnableMultithreading: bool
        GarbageCollectionMode: GCMode
    }

    and GCMode =
        | Default
        | LowLatency
        | HighThroughput

    /// System tray settings and configuration
    and TraySettings = {
        EnableSystemTray: bool                // Master enable/disable
        StartMinimized: bool                  // Start application minimized to tray
        MinimizeToTray: bool                  // Minimize sends to tray instead of taskbar
        CloseToTray: bool                     // Close button sends to tray instead of exit
        ShowTrayNotifications: bool           // Enable tray notifications
        MaxRecentPresets: int                 // Number of recent presets in tray menu (default: 5)
        AutoHideMainWindow: bool              // Hide main window after preset selection
        TrayClickAction: TrayClickAction      // Action for single-click on tray icon
        DoubleClickAction: TrayClickAction    // Action for double-click on tray icon
        NotificationSettings: TrayNotificationSettings
    }

    and TrayClickAction =
        | ShowMainWindow                      // Restore main window
        | ShowTrayMenu                        // Show context menu
        | ApplyLastPreset                     // Apply most recently used preset
        | DoNothing                           // No action

    and TrayNotificationSettings = {
        ShowPresetNotifications: bool         // Show notifications when presets applied
        NotificationDuration: TimeSpan        // How long notifications stay visible
        Position: TrayNotificationPosition    // Where notifications appear
        ShowSuccessOnly: bool                 // Only show successful operations
    }

    and TrayNotificationPosition =
        | Default                             // System default
        | TopRight
        | TopLeft
        | BottomRight
        | BottomLeft

    /// Configuration validation results
    type ValidationResult = {
        IsValid: bool
        Errors: ValidationError list
        Warnings: ValidationWarning list
        ValidatedAt: DateTime
    }

    and ValidationError = {
        Property: string
        Message: string
        Severity: ValidationSeverity
        SuggestedFix: string option
    }

    and ValidationWarning = {
        Property: string
        Message: string
        Impact: PerformanceImpact
    }

    and ValidationSeverity =
        | Critical
        | High
        | Medium
        | Low

    and PerformanceImpact =
        | None
        | Low
        | Medium
        | High

    /// Configuration migration support
    type ConfigurationMigration = {
        FromVersion: string
        ToVersion: string
        MigrationSteps: MigrationStep list
        Description: string
    }

    and MigrationStep = {
        StepName: string
        Action: MigrationAction
        IsRequired: bool
        BackupRequired: bool
    }

    and MigrationAction =
        | RenameProperty of oldName: string * newName: string
        | ChangePropertyType of propertyName: string * converter: (obj -> obj)
        | AddProperty of propertyName: string * defaultValue: obj
        | RemoveProperty of propertyName: string
        | CustomMigration of (JsonElement -> JsonElement)

    // ===== Default Configurations =====

    /// Default user preferences with sensible defaults
    let defaultUserPreferences = {
        StartupBehavior = ShowMainWindow
        NotificationSettings = {
            ShowDisplayChangeNotifications = true
            ShowPresetApplicationNotifications = true
            ShowErrorNotifications = true
            PlaySoundOnSuccess = false
            NotificationDurationSeconds = 5
            NotificationPosition = TopRight
        }
        DisplayDetectionSettings = {
            AutoRefreshInterval = Some (TimeSpan.FromSeconds(30.0))
            DetectHotplugEvents = true
            ValidateDisplayCapabilities = true
            RetryFailedDetections = true
            MaxRetryAttempts = 3
            DetectionTimeout = TimeSpan.FromSeconds(10.0)
            EnableBackgroundDetection = true
        }
        UIPreferences = {
            Theme = Theme.Theme.System
            WindowSize = (1200.0, 800.0)
            WindowPosition = None
            ShowAdvancedControls = false
            RememberWindowState = true
            AutoHideInactiveDisplays = false
            EnableAnimations = true
            ScalingFactor = 1.0
            Language = "en-US"
        }
        AdvancedSettings = {
            LogLevel = LogLevel.Normal
            EnableDebugMode = false
            EnablePerformanceLogging = false
            CacheSettings = {
                MaxCacheSize = 100
                CacheExpirationTime = TimeSpan.FromMinutes(30.0)
                EnableWriteThrough = true
                EnableReadAhead = false
            }
            PerformanceSettings = {
                MaxConcurrentOperations = Environment.ProcessorCount
                UIRefreshRateHz = 60
                StateUpdateBatchSize = 10
                EnableMultithreading = true
                GarbageCollectionMode = Default
            }
            ExperimentalFeatures = Set.empty
            CustomHotkeys = Map.empty
        }
        TraySettings = {
            EnableSystemTray = true
            StartMinimized = false
            MinimizeToTray = true
            CloseToTray = true
            ShowTrayNotifications = true
            MaxRecentPresets = 5
            AutoHideMainWindow = false
            TrayClickAction = ShowMainWindow
            DoubleClickAction = ShowMainWindow
            NotificationSettings = {
                ShowPresetNotifications = true
                NotificationDuration = TimeSpan.FromSeconds(3.0)
                Position = Default
                ShowSuccessOnly = false
            }
        }
        LastModified = DateTime.Now
        Version = "1.0.0"
    }

    // ===== Configuration Validation =====

    /// Comprehensive configuration validation
    module Validation =

        let validateNotificationSettings (settings: NotificationSettings) : ValidationError list =
            [
                if settings.NotificationDurationSeconds < 1 || settings.NotificationDurationSeconds > 60 then
                    yield {
                        Property = "NotificationDurationSeconds"
                        Message = "Notification duration must be between 1 and 60 seconds"
                        Severity = Medium
                        SuggestedFix = Some "Set to 5 seconds (default)"
                    }
            ]

        let validateDisplayDetectionSettings (settings: DisplayDetectionSettings) : ValidationError list =
            [
                if settings.MaxRetryAttempts < 1 || settings.MaxRetryAttempts > 10 then
                    yield {
                        Property = "MaxRetryAttempts"
                        Message = "Max retry attempts must be between 1 and 10"
                        Severity = High
                        SuggestedFix = Some "Set to 3 (default)"
                    }

                if settings.DetectionTimeout < TimeSpan.FromSeconds(1.0) || settings.DetectionTimeout > TimeSpan.FromMinutes(5.0) then
                    yield {
                        Property = "DetectionTimeout"
                        Message = "Detection timeout must be between 1 second and 5 minutes"
                        Severity = High
                        SuggestedFix = Some "Set to 10 seconds (default)"
                    }

                if settings.AutoRefreshInterval.IsSome && settings.AutoRefreshInterval.Value < TimeSpan.FromSeconds(5.0) then
                    yield {
                        Property = "AutoRefreshInterval"
                        Message = "Auto refresh interval must be at least 5 seconds"
                        Severity = Medium
                        SuggestedFix = Some "Set to 30 seconds (default)"
                    }
            ]

        let validateUIPreferences (preferences: UIPreferences) : ValidationError list =
            [
                let (width, height) = preferences.WindowSize
                if width < 800.0 || height < 600.0 then
                    yield {
                        Property = "WindowSize"
                        Message = "Window size must be at least 800x600 pixels"
                        Severity = Medium
                        SuggestedFix = Some "Set to 1200x800 (default)"
                    }

                if preferences.ScalingFactor < 0.5 || preferences.ScalingFactor > 3.0 then
                    yield {
                        Property = "ScalingFactor"
                        Message = "Scaling factor must be between 0.5 and 3.0"
                        Severity = High
                        SuggestedFix = Some "Set to 1.0 (default)"
                    }

                if preferences.UIRefreshRateHz < 30 || preferences.UIRefreshRateHz > 120 then
                    yield {
                        Property = "UIRefreshRateHz"
                        Message = "UI refresh rate must be between 30 and 120 Hz"
                        Severity = Medium
                        SuggestedFix = Some "Set to 60 Hz (default)"
                    }
            ]

        let validateAdvancedSettings (settings: AdvancedSettings) : ValidationError list =
            [
                if settings.PerformanceSettings.MaxConcurrentOperations < 1 || settings.PerformanceSettings.MaxConcurrentOperations > 32 then
                    yield {
                        Property = "MaxConcurrentOperations"
                        Message = "Max concurrent operations must be between 1 and 32"
                        Severity = High
                        SuggestedFix = Some (sprintf "Set to %d (CPU count)" Environment.ProcessorCount)
                    }

                if settings.PerformanceSettings.StateUpdateBatchSize < 1 || settings.PerformanceSettings.StateUpdateBatchSize > 100 then
                    yield {
                        Property = "StateUpdateBatchSize"
                        Message = "State update batch size must be between 1 and 100"
                        Severity = Medium
                        SuggestedFix = Some "Set to 10 (default)"
                    }

                if settings.CacheSettings.MaxCacheSize < 10 || settings.CacheSettings.MaxCacheSize > 1000 then
                    yield {
                        Property = "MaxCacheSize"
                        Message = "Cache size must be between 10 and 1000 entries"
                        Severity = Medium
                        SuggestedFix = Some "Set to 100 (default)"
                    }
            ]

        let validateTraySettings (settings: TraySettings) : ValidationError list =
            [
                if settings.MaxRecentPresets < 1 || settings.MaxRecentPresets > 10 then
                    yield {
                        Property = "MaxRecentPresets"
                        Message = "Max recent presets must be between 1 and 10"
                        Severity = Medium
                        SuggestedFix = Some "Set to 5 (default)"
                    }

                if settings.NotificationSettings.NotificationDuration < TimeSpan.FromSeconds(1.0) ||
                   settings.NotificationSettings.NotificationDuration > TimeSpan.FromSeconds(30.0) then
                    yield {
                        Property = "NotificationDuration"
                        Message = "Notification duration must be between 1 and 30 seconds"
                        Severity = Medium
                        SuggestedFix = Some "Set to 3 seconds (default)"
                    }
            ]

        let validateUserPreferences (preferences: UserPreferences) : ValidationResult =
            let errors = [
                yield! validateNotificationSettings preferences.NotificationSettings
                yield! validateDisplayDetectionSettings preferences.DisplayDetectionSettings
                yield! validateUIPreferences preferences.UIPreferences
                yield! validateAdvancedSettings preferences.AdvancedSettings
                yield! validateTraySettings preferences.TraySettings
            ]

            let warnings = [
                if preferences.AdvancedSettings.EnableDebugMode then
                    yield {
                        Property = "EnableDebugMode"
                        Message = "Debug mode is enabled, which may impact performance"
                        Impact = Medium
                    }

                if preferences.UIPreferences.EnableAnimations && preferences.UIPreferences.ScalingFactor > 1.5 then
                    yield {
                        Property = "EnableAnimations"
                        Message = "Animations with high scaling factor may cause visual artifacts"
                        Impact = Low
                    }
            ]

            {
                IsValid = List.isEmpty errors
                Errors = errors
                Warnings = warnings
                ValidatedAt = DateTime.Now
            }

    // ===== Configuration Management =====

    /// Configuration manager with file I/O and hot reload
    module Manager =

        let private configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplaySwitch-Pro")
        let private userPreferencesPath = Path.Combine(configDirectory, "user-preferences.json")
        let private appConfigurationPath = Path.Combine(configDirectory, "app-configuration.json")
        let private backupDirectory = Path.Combine(configDirectory, "backups")

        /// JSON serialization options
        let private jsonOptions = JsonSerializerOptions()
        do jsonOptions.WriteIndented <- true
           jsonOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
           jsonOptions.Converters.Add(System.Text.Json.Serialization.JsonStringEnumConverter())

        /// Ensure configuration directory exists
        let private ensureConfigurationDirectory() =
            if not (Directory.Exists(configDirectory)) then
                Directory.CreateDirectory(configDirectory) |> ignore
            if not (Directory.Exists(backupDirectory)) then
                Directory.CreateDirectory(backupDirectory) |> ignore

        /// Create backup of configuration file
        let private createBackup (filePath: string) = async {
            try
                if File.Exists(filePath) then
                    let fileName = Path.GetFileName(filePath)
                    let timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss")
                    let backupPath = Path.Combine(backupDirectory, sprintf "%s.%s.backup" fileName timestamp)

                    do! File.ReadAllTextAsync(filePath) |> Async.AwaitTask
                        |> Async.bind (fun content -> File.WriteAllTextAsync(backupPath, content) |> Async.AwaitTask)

                    return Ok backupPath
                else
                    return Ok "No existing file to backup"
            with ex ->
                return Error (sprintf "Failed to create backup: %s" ex.Message)
        }

        /// Load user preferences from file
        let loadUserPreferences() : Async<Result<UserPreferences, string>> = async {
            try
                ensureConfigurationDirectory()

                if File.Exists(userPreferencesPath) then
                    let! json = File.ReadAllTextAsync(userPreferencesPath) |> Async.AwaitTask
                    let preferences = JsonSerializer.Deserialize<UserPreferences>(json, jsonOptions)

                    // Validate loaded preferences
                    let validationResult = Validation.validateUserPreferences preferences
                    if validationResult.IsValid then
                        return Ok preferences
                    else
                        Logging.logWarningf "Loaded preferences have validation errors: %A" validationResult.Errors
                        return Ok preferences  // Return anyway, but log warnings
                else
                    return Ok defaultUserPreferences
            with ex ->
                return Error (sprintf "Failed to load user preferences: %s" ex.Message)
        }

        /// Save user preferences to file with backup
        let saveUserPreferences (preferences: UserPreferences) : Async<Result<unit, string>> = async {
            try
                ensureConfigurationDirectory()

                // Validate before saving
                let validationResult = Validation.validateUserPreferences preferences
                if not validationResult.IsValid then
                    let errorMessages = validationResult.Errors |> List.map (fun e -> e.Message) |> String.concat "; "
                    return Error (sprintf "Cannot save invalid preferences: %s" errorMessages)

                // Create backup
                let! backupResult = createBackup userPreferencesPath
                match backupResult with
                | Error e ->
                    Logging.logWarningf "Failed to create backup: %s" e
                | Ok _ -> ()

                // Save new preferences
                let updatedPreferences =
                    { preferences with
                        LastModified = DateTime.Now
                        Version = "1.0.0" }

                let json = JsonSerializer.Serialize(updatedPreferences, jsonOptions)
                do! File.WriteAllTextAsync(userPreferencesPath, json) |> Async.AwaitTask

                Logging.logInfof "User preferences saved successfully to %s" userPreferencesPath
                return Ok ()

            with ex ->
                return Error (sprintf "Failed to save user preferences: %s" ex.Message)
        }

        /// Load application configuration
        let loadApplicationConfiguration() : Async<Result<ApplicationState.ApplicationConfiguration, string>> = async {
            try
                ensureConfigurationDirectory()

                if File.Exists(appConfigurationPath) then
                    let! json = File.ReadAllTextAsync(appConfigurationPath) |> Async.AwaitTask
                    let config = JsonSerializer.Deserialize<ApplicationState.ApplicationConfiguration>(json, jsonOptions)
                    return Ok config
                else
                    return Ok ApplicationState.defaultConfiguration
            with ex ->
                return Error (sprintf "Failed to load application configuration: %s" ex.Message)
        }

        /// Save application configuration
        let saveApplicationConfiguration (config: ApplicationState.ApplicationConfiguration) : Async<Result<unit, string>> = async {
            try
                ensureConfigurationDirectory()

                // Create backup
                let! backupResult = createBackup appConfigurationPath
                match backupResult with
                | Error e ->
                    Logging.logWarningf "Failed to create backup: %s" e
                | Ok _ -> ()

                let json = JsonSerializer.Serialize(config, jsonOptions)
                do! File.WriteAllTextAsync(appConfigurationPath, json) |> Async.AwaitTask

                return Ok ()
            with ex ->
                return Error (sprintf "Failed to save application configuration: %s" ex.Message)
        }

        /// Configuration file watcher for hot reload
        type ConfigurationWatcher private (onUserPreferencesChanged: UserPreferences -> unit,
                                         onAppConfigChanged: ApplicationState.ApplicationConfiguration -> unit) =

            let userPrefsWatcher = new FileSystemWatcher(configDirectory, "user-preferences.json")
            let appConfigWatcher = new FileSystemWatcher(configDirectory, "app-configuration.json")
            let mutable isDisposed = false

            do
                // Configure watchers
                userPrefsWatcher.NotifyFilter <- NotifyFilters.LastWrite ||| NotifyFilters.Size
                appConfigWatcher.NotifyFilter <- NotifyFilters.LastWrite ||| NotifyFilters.Size

                // Handle user preferences changes
                userPrefsWatcher.Changed.Add(fun e ->
                    if not isDisposed then
                        async {
                            // Debounce file changes
                            do! Async.Sleep(500)
                            match! loadUserPreferences() with
                            | Ok preferences ->
                                onUserPreferencesChanged preferences
                                Logging.logInfo "User preferences reloaded from file change"
                            | Error e ->
                                Logging.logErrorf "Failed to reload user preferences: %s" e
                        } |> Async.Start)

                // Handle app config changes
                appConfigWatcher.Changed.Add(fun e ->
                    if not isDisposed then
                        async {
                            do! Async.Sleep(500)
                            match! loadApplicationConfiguration() with
                            | Ok config ->
                                onAppConfigChanged config
                                Logging.logInfo "Application configuration reloaded from file change"
                            | Error e ->
                                Logging.logErrorf "Failed to reload application configuration: %s" e
                        } |> Async.Start)

                // Enable watchers
                userPrefsWatcher.EnableRaisingEvents <- true
                appConfigWatcher.EnableRaisingEvents <- true

            /// Start watching for configuration changes
            member this.Start() =
                if not isDisposed then
                    userPrefsWatcher.EnableRaisingEvents <- true
                    appConfigWatcher.EnableRaisingEvents <- true
                    Logging.logInfo "Configuration hot reload enabled"

            /// Stop watching for configuration changes
            member this.Stop() =
                userPrefsWatcher.EnableRaisingEvents <- false
                appConfigWatcher.EnableRaisingEvents <- false
                Logging.logInfo "Configuration hot reload disabled"

            /// Dispose resources
            interface IDisposable with
                member this.Dispose() =
                    if not isDisposed then
                        this.Stop()
                        userPrefsWatcher.Dispose()
                        appConfigWatcher.Dispose()
                        isDisposed <- true

        /// Create configuration watcher for hot reload
        let createConfigurationWatcher (onUserPreferencesChanged: UserPreferences -> unit)
                                     (onAppConfigChanged: ApplicationState.ApplicationConfiguration -> unit) : IDisposable =
            new ConfigurationWatcher(onUserPreferencesChanged, onAppConfigChanged) :> IDisposable

    // ===== Configuration Migration =====

    /// Configuration migration utilities
    module Migration =

        /// Available migrations
        let availableMigrations = [
            // Example migration from version 0.9.0 to 1.0.0
            {
                FromVersion = "0.9.0"
                ToVersion = "1.0.0"
                Description = "Add new notification settings and performance optimizations"
                MigrationSteps = [
                    {
                        StepName = "Add notification position setting"
                        Action = AddProperty ("NotificationPosition", box TopRight)
                        IsRequired = true
                        BackupRequired = true
                    }
                    {
                        StepName = "Rename old cache setting"
                        Action = RenameProperty ("CacheSize", "MaxCacheSize")
                        IsRequired = true
                        BackupRequired = false
                    }
                ]
            }
        ]

        /// Check if migration is needed
        let needsMigration (currentVersion: string) (targetVersion: string) : bool =
            match availableMigrations |> List.tryFind (fun m -> m.FromVersion = currentVersion && m.ToVersion = targetVersion) with
            | Some _ -> true
            | None -> false

        /// Perform configuration migration
        let migrateConfiguration (fromVersion: string) (toVersion: string) (configJson: string) : Result<string, string> =
            try
                match availableMigrations |> List.tryFind (fun m -> m.FromVersion = fromVersion && m.ToVersion = toVersion) with
                | None -> Error (sprintf "No migration path found from %s to %s" fromVersion toVersion)
                | Some migration ->
                    let mutable document = JsonDocument.Parse(configJson).RootElement

                    for step in migration.MigrationSteps do
                        match step.Action with
                        | AddProperty (name, value) ->
                            // In a real implementation, would modify the JSON
                            Logging.logInfof "Migration: Adding property %s" name
                        | RenameProperty (oldName, newName) ->
                            Logging.logInfof "Migration: Renaming %s to %s" oldName newName
                        | ChangePropertyType (name, converter) ->
                            Logging.logInfof "Migration: Converting type for %s" name
                        | RemoveProperty name ->
                            Logging.logInfof "Migration: Removing property %s" name
                        | CustomMigration migrationFn ->
                            document <- migrationFn document

                    // Return the modified JSON (simplified - real implementation would rebuild JSON)
                    Ok configJson

            with ex ->
                Error (sprintf "Migration failed: %s" ex.Message)

    // ===== Integration with State Manager =====

    /// Integration utilities for connecting configuration to state manager
    module Integration =

        /// Sync user preferences with application state
        let syncUserPreferencesToState (preferences: UserPreferences) (stateManager: ApplicationStateManager.IStateManager) =
            // Update UI theme
            let themeEvent = ApplicationState.UIThemeChanged (preferences.UIPreferences.Theme, DateTime.Now)
            stateManager.UpdateState(themeEvent) |> ignore

            // Update configuration settings
            let configEvent = ApplicationState.ConfigurationReloaded DateTime.Now
            stateManager.UpdateState(configEvent) |> ignore

        /// Extract user preferences from application state
        let extractUserPreferencesFromState (stateManager: ApplicationStateManager.IStateManager) : UserPreferences =
            let state = stateManager.GetState()

            { defaultUserPreferences with
                UIPreferences =
                    { defaultUserPreferences.UIPreferences with
                        Theme = state.UI.Theme
                        WindowSize = state.Configuration.WindowSize
                        WindowPosition = state.Configuration.WindowPosition }
                DisplayDetectionSettings =
                    { defaultUserPreferences.DisplayDetectionSettings with
                        AutoRefreshInterval = state.Configuration.RefreshInterval
                        MaxRetryAttempts = state.Configuration.MaxRetryAttempts }
                AdvancedSettings =
                    { defaultUserPreferences.AdvancedSettings with
                        LogLevel = state.Configuration.LogLevel
                        EnableDebugMode = state.Configuration.EnableDebugMode
                        CacheSettings = state.Configuration.CacheSettings }
                LastModified = DateTime.Now }

        /// Create configuration change handler for state manager integration
        let createConfigurationChangeHandler (stateManager: ApplicationStateManager.IStateManager) =
            fun (preferences: UserPreferences) ->
                syncUserPreferencesToState preferences stateManager
                Logging.logInfo "Configuration changes synchronized with application state"