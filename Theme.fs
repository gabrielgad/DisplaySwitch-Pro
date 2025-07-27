namespace DisplaySwitchPro

open Avalonia.Media

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
            }
    
    let mutable currentTheme = Light
    
    let getCurrentColors () = getThemeColors currentTheme
    
    let toggleTheme () =
        currentTheme <- if currentTheme = Light then Dark else Light
        currentTheme
    
    let setTheme newTheme =
        currentTheme <- newTheme