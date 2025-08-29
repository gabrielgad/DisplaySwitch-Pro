namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

// Application class and startup logic
module ApplicationRunner =
    
    // Immutable application data
    type AppData = {
        World: World option
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
                match (!appData).World, (!appData).Adapter with
                | Some world, Some adapter ->
                    let window = GUI.createMainWindow world adapter
                    desktop.MainWindow <- window
                    window.Show()
                    printfn "Window created and shown"
                | _ -> failwith "Application data not set"
            | _ -> 
                printfn "No desktop lifetime found"
            
            base.OnFrameworkInitializationCompleted()

    // Functional application runner
    let run (adapter: IPlatformAdapter) (world: World) =
        let appDataRef = ref { World = Some world; Adapter = Some adapter }
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