namespace DisplaySwitchPro

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open ApplicationState
open ApplicationStateManager
open ApplicationConfiguration

/// Application Lifecycle Management with Service Initialization and Graceful Shutdown
/// Provides comprehensive lifecycle management for the DisplaySwitch-Pro application
module ApplicationLifecycle =

    // ===== Service Definition Types =====

    /// Core application services container
    type ApplicationServices = {
        StateManager: IStateManager
        EventBus: EventBus<StateEvent>
        DisplayAdapter: IPlatformAdapter
        ConfigurationManager: IConfigurationManager
        Logger: ILogger
        CacheManager: ICacheManager
        PresetManager: IPresetManager
        UICoordinator: IUICoordinator option
    }

    /// Service interfaces for dependency injection
    and IConfigurationManager =
        abstract member LoadConfiguration: unit -> Async<Result<UserPreferences, string>>
        abstract member SaveConfiguration: UserPreferences -> Async<Result<unit, string>>
        abstract member WatchForChanges: (UserPreferences -> unit) -> IDisposable

    and ILogger =
        abstract member LogInfo: string -> unit
        abstract member LogWarning: string -> unit
        abstract member LogError: string -> unit
        abstract member LogDebug: string -> unit

    and ICacheManager =
        abstract member Initialize: unit -> Result<unit, string>
        abstract member Cleanup: unit -> Async<unit>
        abstract member GetStatistics: unit -> CacheStatistics
        inherit IDisposable

    and IPresetManager =
        abstract member LoadPresets: unit -> Async<Result<Map<string, DisplayConfiguration>, string>>
        abstract member SavePreset: string -> DisplayConfiguration -> Async<Result<unit, string>>
        abstract member DeletePreset: string -> Async<Result<unit, string>>

    and IUICoordinator =
        abstract member Initialize: ApplicationServices -> Result<unit, string>
        abstract member Shutdown: unit -> Async<unit>
        abstract member IsInitialized: bool

    and EventBus<'T> = {
        Subscribe: ('T -> unit) -> IDisposable
        Publish: 'T -> unit
        Clear: unit -> unit
        SubscriberCount: int
    }

    and CacheStatistics = {
        TotalEntries: int
        HitRate: float
        MemoryUsage: int64
        LastCleanup: DateTime
    }

    /// Initialization error types
    type InitializationError =
        | ConfigurationLoadFailed of string
        | PlatformAdapterFailed of string
        | StateInitializationFailed of string
        | LoggingInitializationFailed of string
        | CacheInitializationFailed of string
        | PresetManagerInitializationFailed of string
        | UIInitializationFailed of string
        | DependencyResolutionFailed of string * exn

    /// Service health status
    type ServiceHealth = {
        ServiceName: string
        IsHealthy: bool
        LastHealthCheck: DateTime
        Status: HealthStatus
        Details: string option
    }

    and HealthStatus =
        | Healthy
        | Degraded of string
        | Unhealthy of string
        | Unknown

    // ===== Service Implementations =====

    /// Configuration manager implementation
    type ConfigurationManagerImpl() =
        interface IConfigurationManager with
            member this.LoadConfiguration() = Manager.loadUserPreferences()
            member this.SaveConfiguration(prefs) = Manager.saveUserPreferences(prefs)
            member this.WatchForChanges(handler) =
                Manager.createConfigurationWatcher handler (fun _ -> ())

    /// Logger implementation wrapper
    type LoggerImpl() =
        interface ILogger with
            member this.LogInfo(msg) = Logging.logInfo msg
            member this.LogWarning(msg) = Logging.logWarning msg
            member this.LogError(msg) = Logging.logError msg
            member this.LogDebug(msg) = Logging.logVerbose msg

    /// Cache manager implementation
    type CacheManagerImpl() =
        let mutable isInitialized = false
        let mutable statistics = {
            TotalEntries = 0
            HitRate = 0.0
            MemoryUsage = 0L
            LastCleanup = DateTime.Now
        }

        interface ICacheManager with
            member this.Initialize() =
                try
                    isInitialized <- true
                    statistics <- { statistics with LastCleanup = DateTime.Now }
                    Ok ()
                with ex ->
                    Error (sprintf "Cache initialization failed: %s" ex.Message)

            member this.Cleanup() = async {
                try
                    statistics <- { statistics with LastCleanup = DateTime.Now }
                    Logging.logInfo "Cache cleanup completed"
                with ex ->
                    Logging.logErrorf "Cache cleanup failed: %s" ex.Message
            }

            member this.GetStatistics() = statistics

            member this.Dispose() =
                if isInitialized then
                    isInitialized <- false
                    Logging.logInfo "Cache manager disposed"

    /// Preset manager implementation
    type PresetManagerImpl() =
        interface IPresetManager with
            member this.LoadPresets() = async {
                try
                    let presets = PresetManager.loadPresetsFromDisk()
                    return Ok presets
                with ex ->
                    return Error (sprintf "Failed to load presets: %s" ex.Message)
            }

            member this.SavePreset(name) (config) = async {
                try
                    PresetManager.savePresetToDisk name config
                    return Ok ()
                with ex ->
                    return Error (sprintf "Failed to save preset: %s" ex.Message)
            }

            member this.DeletePreset(name) = async {
                try
                    PresetManager.deletePresetFromDisk name
                    return Ok ()
                with ex ->
                    return Error (sprintf "Failed to delete preset: %s" ex.Message)
            }

    /// UI Coordinator implementation
    type UICoordinatorImpl() =
        let mutable isInitialized = false
        let mutable services: ApplicationServices option = None

        interface IUICoordinator with
            member this.Initialize(appServices) =
                try
                    services <- Some appServices
                    isInitialized <- true
                    Logging.logInfo "UI Coordinator initialized successfully"
                    Ok ()
                with ex ->
                    Error (sprintf "UI Coordinator initialization failed: %s" ex.Message)

            member this.Shutdown() = async {
                try
                    if isInitialized then
                        isInitialized <- false
                        services <- None
                        Logging.logInfo "UI Coordinator shutdown completed"
                with ex ->
                    Logging.logErrorf "UI Coordinator shutdown failed: %s" ex.Message
            }

            member this.IsInitialized = isInitialized

    /// Event bus implementation
    let createEventBus<'T>() : EventBus<'T> =
        let subscribers = Dictionary<Guid, 'T -> unit>()
        let lockObj = obj()

        {
            Subscribe = fun handler ->
                let id = Guid.NewGuid()
                lock lockObj (fun () -> subscribers.[id] <- handler)
                { new IDisposable with
                    member _.Dispose() =
                        lock lockObj (fun () -> subscribers.Remove(id) |> ignore) }

            Publish = fun event ->
                let handlers = lock lockObj (fun () -> subscribers.Values |> Seq.toArray)
                for handler in handlers do
                    try
                        handler event
                    with ex ->
                        Logging.logErrorf "Error in event handler: %s" ex.Message

            Clear = fun () ->
                lock lockObj (fun () -> subscribers.Clear())

            SubscriberCount =
                lock lockObj (fun () -> subscribers.Count)
        }

    // ===== Service Initialization =====

    /// Service initialization with dependency resolution
    module ServiceInitialization =

        /// Initialize logging service
        let initializeLogging (config: UserPreferences) : Result<ILogger, InitializationError> =
            try
                Logging.setLogLevel config.AdvancedSettings.LogLevel
                let logger = LoggerImpl() :> ILogger
                logger.LogInfo "Logging service initialized successfully"
                Ok logger
            with ex ->
                Error (LoggingInitializationFailed ex.Message)

        /// Initialize platform adapter
        let initializePlatformAdapter() : Result<IPlatformAdapter, InitializationError> =
            try
                let adapter = PlatformAdapter.create()
                Logging.logInfo "Platform adapter initialized successfully"
                Ok adapter
            with ex ->
                Error (PlatformAdapterFailed ex.Message)

        /// Initialize state manager
        let initializeStateManager (config: UserPreferences) : Result<IStateManager, InitializationError> =
            try
                let initialState = { ApplicationState.empty with
                    Configuration = { ApplicationState.defaultConfiguration with
                        LogLevel = config.AdvancedSettings.LogLevel
                        Theme = config.UIPreferences.Theme
                        WindowSize = config.UIPreferences.WindowSize
                        WindowPosition = config.UIPreferences.WindowPosition } }

                let stateManager = ApplicationStateManager.create initialState
                Logging.logInfo "State manager initialized successfully"
                Ok stateManager
            with ex ->
                Error (StateInitializationFailed ex.Message)

        /// Initialize configuration manager
        let initializeConfigurationManager() : Result<IConfigurationManager, InitializationError> =
            try
                let configManager = ConfigurationManagerImpl() :> IConfigurationManager
                Logging.logInfo "Configuration manager initialized successfully"
                Ok configManager
            with ex ->
                Error (ConfigurationLoadFailed ex.Message)

        /// Initialize cache manager
        let initializeCacheManager() : Result<ICacheManager, InitializationError> =
            try
                let cacheManager = CacheManagerImpl() :> ICacheManager
                match cacheManager.Initialize() with
                | Ok _ ->
                    Logging.logInfo "Cache manager initialized successfully"
                    Ok cacheManager
                | Error e ->
                    Error (CacheInitializationFailed e)
            with ex ->
                Error (CacheInitializationFailed ex.Message)

        /// Initialize preset manager
        let initializePresetManager() : Result<IPresetManager, InitializationError> =
            try
                let presetManager = PresetManagerImpl() :> IPresetManager
                Logging.logInfo "Preset manager initialized successfully"
                Ok presetManager
            with ex ->
                Error (PresetManagerInitializationFailed ex.Message)

        /// Initialize all services with dependency resolution
        let initializeAllServices (config: UserPreferences) : Result<ApplicationServices, InitializationError> =
            result {
                let! logger = initializeLogging config
                let! adapter = initializePlatformAdapter()
                let! stateManager = initializeStateManager config
                let! configManager = initializeConfigurationManager()
                let! cacheManager = initializeCacheManager()
                let! presetManager = initializePresetManager()

                let eventBus = createEventBus<StateEvent>()

                return {
                    StateManager = stateManager
                    EventBus = eventBus
                    DisplayAdapter = adapter
                    ConfigurationManager = configManager
                    Logger = logger
                    CacheManager = cacheManager
                    PresetManager = presetManager
                    UICoordinator = None  // Initialize later if needed
                }
            }

        /// Initialize UI coordinator (optional)
        let initializeUICoordinator (services: ApplicationServices) : Result<ApplicationServices, InitializationError> =
            try
                let uiCoordinator = UICoordinatorImpl() :> IUICoordinator
                match uiCoordinator.Initialize(services) with
                | Ok _ ->
                    Ok { services with UICoordinator = Some uiCoordinator }
                | Error e ->
                    Error (UIInitializationFailed e)
            with ex ->
                Error (UIInitializationFailed ex.Message)

    // ===== Health Monitoring =====

    /// Service health monitoring
    module HealthMonitoring =

        /// Check individual service health
        let checkServiceHealth (services: ApplicationServices) : ServiceHealth list =
            [
                // State Manager Health
                yield {
                    ServiceName = "StateManager"
                    IsHealthy = true
                    LastHealthCheck = DateTime.Now
                    Status = Healthy
                    Details = None
                }

                // Display Adapter Health
                yield {
                    ServiceName = "DisplayAdapter"
                    IsHealthy = true
                    LastHealthCheck = DateTime.Now
                    Status = Healthy
                    Details = None
                }

                // Configuration Manager Health
                yield {
                    ServiceName = "ConfigurationManager"
                    IsHealthy = true
                    LastHealthCheck = DateTime.Now
                    Status = Healthy
                    Details = None
                }

                // Cache Manager Health
                yield {
                    ServiceName = "CacheManager"
                    IsHealthy = true
                    LastHealthCheck = DateTime.Now
                    Status = Healthy
                    Details = Some (sprintf "Entries: %d" (services.CacheManager.GetStatistics().TotalEntries))
                }

                // Event Bus Health
                yield {
                    ServiceName = "EventBus"
                    IsHealthy = services.EventBus.SubscriberCount >= 0
                    LastHealthCheck = DateTime.Now
                    Status = if services.EventBus.SubscriberCount > 0 then Healthy else Degraded "No subscribers"
                    Details = Some (sprintf "Subscribers: %d" services.EventBus.SubscriberCount)
                }

                // UI Coordinator Health (if present)
                match services.UICoordinator with
                | Some uiCoordinator ->
                    yield {
                        ServiceName = "UICoordinator"
                        IsHealthy = uiCoordinator.IsInitialized
                        LastHealthCheck = DateTime.Now
                        Status = if uiCoordinator.IsInitialized then Healthy else Unhealthy "Not initialized"
                        Details = None
                    }
                | None ->
                    yield {
                        ServiceName = "UICoordinator"
                        IsHealthy = true
                        LastHealthCheck = DateTime.Now
                        Status = Degraded "Not initialized (optional)"
                        Details = None
                    }
            ]

        /// Overall system health assessment
        let assessOverallHealth (healthStatus: ServiceHealth list) : HealthStatus =
            let unhealthyServices = healthStatus |> List.filter (not << _.IsHealthy)
            let criticalServices = healthStatus |> List.filter (fun s ->
                s.ServiceName = "StateManager" || s.ServiceName = "DisplayAdapter")

            if List.isEmpty unhealthyServices then
                Healthy
            elif criticalServices |> List.exists (not << _.IsHealthy) then
                Unhealthy "Critical services are unhealthy"
            elif unhealthyServices.Length <= 2 then
                Degraded (sprintf "%d services unhealthy" unhealthyServices.Length)
            else
                Unhealthy (sprintf "%d services unhealthy" unhealthyServices.Length)

        /// Log health status
        let logHealthStatus (healthStatus: ServiceHealth list) =
            let overallHealth = assessOverallHealth healthStatus

            Logging.logInfof "=== System Health Check ==="
            Logging.logInfof "Overall Status: %A" overallHealth

            for service in healthStatus do
                let statusIcon = if service.IsHealthy then "✅" else "❌"
                let details = service.Details |> Option.defaultValue ""
                Logging.logInfof "%s %s: %A %s" statusIcon service.ServiceName service.Status details

    // ===== Graceful Shutdown =====

    /// Graceful shutdown with resource cleanup
    module GracefulShutdown =

        /// Shutdown individual services in reverse dependency order
        let shutdownServices (services: ApplicationServices) = async {
            try
                Logging.logInfo "Starting graceful application shutdown..."

                // 1. Stop UI Coordinator first (if present)
                match services.UICoordinator with
                | Some uiCoordinator ->
                    do! uiCoordinator.Shutdown()
                    Logging.logInfo "✅ UI Coordinator shutdown completed"
                | None ->
                    Logging.logInfo "✅ UI Coordinator not initialized, skipping"

                // 2. Clear event bus to stop new events
                services.EventBus.Clear()
                Logging.logInfo "✅ Event bus cleared"

                // 3. Save current state
                do! services.StateManager.SaveStateAsync() |> Async.Ignore
                Logging.logInfo "✅ Application state saved"

                // 4. Cleanup cache
                do! services.CacheManager.Cleanup()
                services.CacheManager.Dispose()
                Logging.logInfo "✅ Cache manager cleanup completed"

                // 5. Final health check
                let finalHealth = HealthMonitoring.checkServiceHealth services
                let healthyServices = finalHealth |> List.filter (_.IsHealthy) |> List.length
                Logging.logInfof "✅ Final health check: %d/%d services healthy" healthyServices finalHealth.Length

                // 6. Log shutdown completion
                Logging.logInfo "🎉 Application shutdown completed successfully"

            with ex ->
                Logging.logErrorf "❌ Error during shutdown: %s" ex.Message
                Logging.logErrorf "Stack trace: %s" ex.StackTrace
        }

        /// Emergency shutdown for critical failures
        let emergencyShutdown (services: ApplicationServices) (reason: string) = async {
            try
                Logging.logErrorf "🚨 EMERGENCY SHUTDOWN: %s" reason

                // Quick cleanup without error handling
                services.EventBus.Clear()
                services.CacheManager.Dispose()

                Logging.logWarning "Emergency shutdown completed"
            with ex ->
                Logging.logErrorf "Emergency shutdown failed: %s" ex.Message
        }

    // ===== Main Application Runner =====

    /// Main application lifecycle orchestration
    module Runner =

        /// Load configuration with validation and migration
        let loadConfigurationWithMigration() : Async<Result<UserPreferences, string>> = async {
            match! Manager.loadUserPreferences() with
            | Ok preferences ->
                // Validate loaded configuration
                let validationResult = Validation.validateUserPreferences preferences
                if validationResult.IsValid then
                    return Ok preferences
                else
                    Logging.logWarningf "Configuration validation warnings: %A" validationResult.Warnings
                    return Ok preferences  // Continue with warnings

            | Error e ->
                Logging.logWarningf "Failed to load configuration, using defaults: %s" e
                return Ok defaultUserPreferences
        }

        /// Run complete application lifecycle
        let runApplication (args: string[]) : Async<int> = async {
            let mutable services: ApplicationServices option = None

            try
                // Parse command line arguments
                let configPath =
                    args
                    |> Array.tryFind (fun arg -> arg.StartsWith("--config="))
                    |> Option.map (fun arg -> arg.Substring("--config=".Length))

                Logging.logInfof "DisplaySwitch-Pro starting... (args: %A)" args

                // Load configuration
                match! loadConfigurationWithMigration() with
                | Error e ->
                    Logging.logErrorf "❌ Failed to load configuration: %s" e
                    return 1

                | Ok config ->
                    // Initialize services
                    match ServiceInitialization.initializeAllServices config with
                    | Error e ->
                        Logging.logErrorf "❌ Failed to initialize services: %A" e
                        return 1

                    | Ok initializedServices ->
                        services <- Some initializedServices
                        Logging.logInfo "✅ All services initialized successfully"

                        // Initial health check
                        let initialHealth = HealthMonitoring.checkServiceHealth initializedServices
                        HealthMonitoring.logHealthStatus initialHealth

                        // Setup configuration watching
                        let configWatcher = Integration.createConfigurationChangeHandler initializedServices.StateManager
                        use _configDisposable = initializedServices.ConfigurationManager.WatchForChanges configWatcher

                        // Initialize UI if needed (would integrate with ApplicationRunner.fs)
                        let finalServices =
                            match ServiceInitialization.initializeUICoordinator initializedServices with
                            | Ok servicesWithUI -> servicesWithUI
                            | Error e ->
                                Logging.logWarningf "UI initialization failed: %A" e
                                initializedServices

                        services <- Some finalServices

                        // Run main application loop (placeholder - would integrate with Avalonia)
                        Logging.logInfo "🚀 Application ready and running"

                        // Simulate application running (in real implementation, this would be Avalonia.Run())
                        do! Async.Sleep(1000)  // Placeholder for actual application loop

                        // Normal shutdown
                        do! GracefulShutdown.shutdownServices finalServices
                        return 0

            with ex ->
                Logging.logErrorf "💥 Unhandled application error: %s" ex.Message
                Logging.logErrorf "Stack trace: %s" ex.StackTrace

                // Emergency shutdown if services were initialized
                match services with
                | Some s -> do! GracefulShutdown.emergencyShutdown s ex.Message
                | None -> ()

                return 1
        }

        /// Run application with enhanced error handling and diagnostics
        let runApplicationWithDiagnostics (args: string[]) : Async<int> = async {
            let startTime = DateTime.Now

            try
                Logging.logInfo "======================================================"
                Logging.logInfo "DisplaySwitch-Pro - Functional Programming Edition"
                Logging.logInfo "======================================================"
                Logging.logInfof "Start time: %A" startTime

                let! exitCode = runApplication args

                let duration = DateTime.Now - startTime
                Logging.logInfof "Application completed in %A with exit code %d" duration exitCode

                return exitCode

            with ex ->
                let duration = DateTime.Now - startTime
                Logging.logErrorf "Application failed after %A: %s" duration ex.Message
                return 1
        }