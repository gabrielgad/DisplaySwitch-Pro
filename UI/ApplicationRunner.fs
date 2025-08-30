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
        
        override this.OnFrameworkInitializationCompleted() =
            match this.ApplicationLifetime with
            | :? IClassicDesktopStyleApplicationLifetime as desktop ->
                match (!appData).State, (!appData).Adapter with
                | Some state, Some adapter ->
                    let window = GUI.createMainWindow state adapter
                    desktop.MainWindow <- window
                    window.Show()
                    printfn "Window created and shown"
                | _ -> failwith "Application data not set"
            | _ -> 
                printfn "No desktop lifetime found"
            
            base.OnFrameworkInitializationCompleted()

    // Functional application runner
    let run (adapter: IPlatformAdapter) (state: AppState) =
        let appDataRef = ref { State = Some state; Adapter = Some adapter }
        try
            printfn "Starting Avalonia application..."
            let result = 
                AppBuilder
                    .Configure<App>(fun () -> App(appDataRef))
                    .UsePlatformDetect()
                    .LogToTrace()
                    .StartWithClassicDesktopLifetime([||])
            printfn "Avalonia application finished with exit code: %d" result
            result
        with
        | ex -> 
            printfn "Error starting Avalonia: %s" ex.Message
            printfn "Stack trace: %s" ex.StackTrace
            1