namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

// Application class and startup logic
module ApplicationRunner =
    
    // Immutable application data
    type AppData = {
        State: AppState option
        Adapter: IPlatformAdapter option
    }
    
    // Avalonia Application class with functional state
    type App(appData: AppData ref) =
        inherit Application()
        
        override this.Initialize() =
            this.Styles.Add(FluentTheme())

            // Initialize theme resources
            Theme.initializeThemeResources this

            Logging.logNormal "Avalonia application initialized with theme support"
        
        override this.OnFrameworkInitializationCompleted() =
            match this.ApplicationLifetime with
            | :? IClassicDesktopStyleApplicationLifetime as desktop ->
                match (!appData).State, (!appData).Adapter with
                | Some state, Some adapter ->
                    let window = GUI.createMainWindow state adapter
                    desktop.MainWindow <- window
                    window.Show()
                    Logging.logVerbose "Window created and shown"
                | _ -> failwith "Application data not set"
            | _ -> 
                Logging.logError "No desktop lifetime found"
            
            base.OnFrameworkInitializationCompleted()

    // Functional application runner
    let run (adapter: IPlatformAdapter) (state: AppState) =
        let appDataRef = ref { State = Some state; Adapter = Some adapter }
        try
            Logging.logNormal "Starting Avalonia application..."
            let result = 
                AppBuilder
                    .Configure<App>(fun () -> App(appDataRef))
                    .UsePlatformDetect()
                    .LogToTrace()
                    .StartWithClassicDesktopLifetime([||])
            Logging.logNormalf "Avalonia application finished with exit code: %d" result
            result
        with
        | ex -> 
            Logging.logErrorf "Error starting Avalonia: %s" ex.Message
            Logging.logErrorf "Stack trace: %s" ex.StackTrace
            1