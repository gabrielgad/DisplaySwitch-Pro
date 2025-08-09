namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

// Application class and startup logic
module ApplicationRunner =
    
    // Avalonia Application class
    type App() =
        inherit Application()
        
        static let mutable worldData = None
        static let mutable adapterData = None
        
        static member SetData(world: World, adapter: IPlatformAdapter) =
            worldData <- Some world
            adapterData <- Some adapter
        
        override this.Initialize() =
            this.Styles.Add(FluentTheme())
        
        override this.OnFrameworkInitializationCompleted() =
            match this.ApplicationLifetime with
            | :? IClassicDesktopStyleApplicationLifetime as desktop ->
                match worldData, adapterData with
                | Some world, Some adapter ->
                    let window = WindowManager.createMainWindow world adapter
                    desktop.MainWindow <- window
                    window.Show()
                    printfn "Window created and shown"
                | _ -> failwith "Application data not set"
            | _ -> 
                printfn "No desktop lifetime found"
            
            base.OnFrameworkInitializationCompleted()

    // Application runner
    let run (adapter: IPlatformAdapter) (world: World) =
        App.SetData(world, adapter)
        try
            printfn "Starting Avalonia application..."
            let result = 
                AppBuilder
                    .Configure<App>()
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