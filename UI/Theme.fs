namespace DisplaySwitchPro

open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Styling

module Theme =
    
    type Theme = 
        | Light
        | Dark
    
    type ThemeColors = {
        Background: Color
        Surface: Color
        Primary: Color
        PrimaryDark: Color
        Secondary: Color
        SecondaryDark: Color
        Text: Color
        TextSecondary: Color
        Border: Color
        DisabledBg: Color
        DisabledBgDark: Color
        CanvasBg: Color
        CanvasBgDark: Color
        Success: Color
        Error: Color
        // TextBox-specific colors for better visibility
        TextBoxBackground: Color
        TextBoxForeground: Color
        TextBoxBorder: Color
    }
    
    let getThemeColors theme =
        match theme with
        | Light ->
            {
                Background = Color.FromRgb(248uy, 250uy, 252uy)
                Surface = Color.FromRgb(255uy, 255uy, 255uy)
                Primary = Color.FromRgb(100uy, 149uy, 237uy)  // Cornflower blue
                PrimaryDark = Color.FromRgb(65uy, 105uy, 225uy)  // Royal blue
                Secondary = Color.FromRgb(52uy, 152uy, 219uy)
                SecondaryDark = Color.FromRgb(41uy, 128uy, 185uy)
                Text = Color.FromRgb(31uy, 41uy, 55uy)
                TextSecondary = Color.FromRgb(107uy, 114uy, 128uy)
                Border = Color.FromRgb(220uy, 225uy, 230uy)
                DisabledBg = Color.FromRgb(220uy, 220uy, 220uy)
                DisabledBgDark = Color.FromRgb(180uy, 180uy, 180uy)
                CanvasBg = Color.FromRgb(245uy, 248uy, 250uy)
                CanvasBgDark = Color.FromRgb(230uy, 235uy, 240uy)
                Success = Color.FromRgb(34uy, 197uy, 94uy)
                Error = Color.FromRgb(239uy, 68uy, 68uy)
                // TextBox colors for light theme
                TextBoxBackground = Color.FromRgb(255uy, 255uy, 255uy)  // Pure white
                TextBoxForeground = Color.FromRgb(31uy, 41uy, 55uy)     // Dark gray
                TextBoxBorder = Color.FromRgb(220uy, 225uy, 230uy)      // Light gray border
            }
        | Dark ->
            {
                Background = Color.FromRgb(17uy, 24uy, 39uy)  // Very dark blue
                Surface = Color.FromRgb(31uy, 41uy, 55uy)  // Dark blue-gray
                Primary = Color.FromRgb(96uy, 165uy, 250uy)  // Light blue
                PrimaryDark = Color.FromRgb(59uy, 130uy, 246uy)  // Blue
                Secondary = Color.FromRgb(79uy, 70uy, 229uy)  // Indigo
                SecondaryDark = Color.FromRgb(67uy, 56uy, 202uy)  // Dark indigo
                Text = Color.FromRgb(243uy, 244uy, 246uy)  // Very light gray
                TextSecondary = Color.FromRgb(156uy, 163uy, 175uy)  // Gray
                Border = Color.FromRgb(55uy, 65uy, 81uy)  // Dark gray
                DisabledBg = Color.FromRgb(75uy, 85uy, 99uy)
                DisabledBgDark = Color.FromRgb(55uy, 65uy, 81uy)
                CanvasBg = Color.FromRgb(24uy, 32uy, 47uy)
                CanvasBgDark = Color.FromRgb(17uy, 24uy, 39uy)
                Success = Color.FromRgb(34uy, 197uy, 94uy)
                Error = Color.FromRgb(248uy, 113uy, 113uy)
                // TextBox colors for dark theme - much better contrast
                TextBoxBackground = Color.FromRgb(75uy, 85uy, 99uy)    // Lighter gray for better visibility
                TextBoxForeground = Color.FromRgb(243uy, 244uy, 246uy) // Light text
                TextBoxBorder = Color.FromRgb(107uy, 114uy, 128uy)     // Medium gray border
            }
    
    let mutable currentTheme = Light
    
    let getCurrentColors () = getThemeColors currentTheme
    
    let toggleTheme () =
        currentTheme <- if currentTheme = Light then Dark else Light
        currentTheme
    
    let setTheme newTheme =
        currentTheme <- newTheme

    // Convert Theme to Avalonia ThemeVariant
    let toThemeVariant theme =
        match theme with
        | Light -> ThemeVariant.Light
        | Dark -> ThemeVariant.Dark

    // Convert Avalonia ThemeVariant to Theme
    let fromThemeVariant (variant: ThemeVariant) =
        if variant = ThemeVariant.Light then Light
        elif variant = ThemeVariant.Dark then Dark
        else Light // Default to Light for unknown variants

    // Create brush resources from theme colors
    let private createBrushResources (colors: ThemeColors) =
        [
            // Background colors
            ("AppBackgroundBrush", SolidColorBrush(colors.Background) :> IBrush)
            ("AppSurfaceBrush", SolidColorBrush(colors.Surface) :> IBrush)
            ("AppCanvasBackgroundBrush", SolidColorBrush(colors.CanvasBg) :> IBrush)
            ("AppCanvasBackgroundDarkBrush", SolidColorBrush(colors.CanvasBgDark) :> IBrush)

            // Primary colors
            ("AppPrimaryBrush", SolidColorBrush(colors.Primary) :> IBrush)
            ("AppPrimaryDarkBrush", SolidColorBrush(colors.PrimaryDark) :> IBrush)
            ("AppSecondaryBrush", SolidColorBrush(colors.Secondary) :> IBrush)
            ("AppSecondaryDarkBrush", SolidColorBrush(colors.SecondaryDark) :> IBrush)

            // Text colors
            ("AppTextBrush", SolidColorBrush(colors.Text) :> IBrush)
            ("AppTextSecondaryBrush", SolidColorBrush(colors.TextSecondary) :> IBrush)

            // TextBox specific colors
            ("AppTextBoxBackgroundBrush", SolidColorBrush(colors.TextBoxBackground) :> IBrush)
            ("AppTextBoxForegroundBrush", SolidColorBrush(colors.TextBoxForeground) :> IBrush)
            ("AppTextBoxBorderBrush", SolidColorBrush(colors.TextBoxBorder) :> IBrush)

            // Border and state colors
            ("AppBorderBrush", SolidColorBrush(colors.Border) :> IBrush)
            ("AppDisabledBackgroundBrush", SolidColorBrush(colors.DisabledBg) :> IBrush)
            ("AppDisabledBackgroundDarkBrush", SolidColorBrush(colors.DisabledBgDark) :> IBrush)

            // Status colors
            ("AppSuccessBrush", SolidColorBrush(colors.Success) :> IBrush)
            ("AppErrorBrush", SolidColorBrush(colors.Error) :> IBrush)

            // Derived colors for enhanced UI
            ("AppPrimaryTransparentBrush", SolidColorBrush(Color.FromArgb(100uy, colors.Primary.R, colors.Primary.G, colors.Primary.B)) :> IBrush)
            ("AppSurfaceTransparentBrush", SolidColorBrush(Color.FromArgb(90uy, colors.Surface.R, colors.Surface.G, colors.Surface.B)) :> IBrush)
            ("AppBorderTransparentBrush", SolidColorBrush(Color.FromArgb(60uy, colors.Border.R, colors.Border.G, colors.Border.B)) :> IBrush)
        ]

    // Create a resource dictionary from theme colors
    let private createResourceDictionary theme =
        let colors = getThemeColors theme
        let resources = createBrushResources colors
        let dictionary = ResourceDictionary()

        resources |> List.iter (fun (key, brush) ->
            dictionary.Add(key, brush))

        dictionary

    // Initialize theme resources for the application
    let initializeThemeResources (app: Application) =
        try
            // Create light and dark resource dictionaries
            let lightResources = createResourceDictionary Light
            let darkResources = createResourceDictionary Dark

            // Create main resource dictionary with theme dictionaries
            let mainDict = ResourceDictionary()

            // Add light theme resources
            mainDict.ThemeDictionaries.Add(ThemeVariant.Light, lightResources)

            // Add dark theme resources
            mainDict.ThemeDictionaries.Add(ThemeVariant.Dark, darkResources)

            // Add to application resources
            app.Resources.MergedDictionaries.Add(mainDict)

            // Set initial theme based on current theme
            let initialTheme = currentTheme
            let themeVariant = toThemeVariant initialTheme

            app.RequestedThemeVariant <- themeVariant

            Logging.logNormalf "Initialized Avalonia theme resources with %A theme" initialTheme

        with
        | ex ->
            Logging.logErrorf "Failed to initialize theme resources: %s" ex.Message
            Logging.logErrorf "Stack trace: %s" ex.StackTrace

    // Switch theme variant and update resources
    let private switchThemeVariant (app: Application) theme =
        try
            let themeVariant = toThemeVariant theme

            // Update Avalonia's requested theme variant
            app.RequestedThemeVariant <- themeVariant

            Logging.logNormalf "Switched to %A theme variant" theme

        with
        | ex ->
            Logging.logErrorf "Failed to switch theme variant: %s" ex.Message

    // Switch theme with Avalonia integration
    let switchTheme () =
        let newTheme = toggleTheme()

        // Update Avalonia theme if application is available
        try
            match Application.Current with
            | null ->
                Logging.logVerbose "No Avalonia application instance found for theme switching"
            | app ->
                switchThemeVariant app newTheme
                Logging.logVerbosef "Theme switched to %A with Avalonia integration" newTheme
        with
        | ex ->
            Logging.logErrorf "Error switching Avalonia theme: %s" ex.Message

        newTheme

    // Set theme with Avalonia integration
    let setThemeWithAvalonia newTheme =
        setTheme newTheme

        try
            match Application.Current with
            | null ->
                Logging.logVerbose "No Avalonia application instance found for theme setting"
            | app ->
                switchThemeVariant app newTheme
                Logging.logVerbosef "Theme set to %A with Avalonia integration" newTheme
        with
        | ex ->
            Logging.logErrorf "Error setting Avalonia theme: %s" ex.Message