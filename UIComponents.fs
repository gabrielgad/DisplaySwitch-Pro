namespace DisplaySwitchPro

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

module UIComponents =
    
    type VisualDisplay = {
        Display: DisplayInfo
        Rectangle: Rectangle
        Border: Border
        Label: TextBlock
        EnableCheckBox: CheckBox
    }
    
    let createVisualDisplay (display: DisplayInfo) (onPositionChanged: DisplayId -> float * float -> unit) =
        let colors = Theme.getCurrentColors()
        let scale = 0.1
        let width = float display.Resolution.Width * scale
        let height = float display.Resolution.Height * scale
        
        let rect = Rectangle()
        rect.Width <- width
        rect.Height <- height
        
        let enabledGradient = LinearGradientBrush()
        enabledGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        enabledGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        enabledGradient.GradientStops.Add(GradientStop(colors.Primary, 0.0))
        enabledGradient.GradientStops.Add(GradientStop(colors.PrimaryDark, 1.0))
        
        let disabledGradient = LinearGradientBrush()
        disabledGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        disabledGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        disabledGradient.GradientStops.Add(GradientStop(colors.DisabledBg, 0.0))
        disabledGradient.GradientStops.Add(GradientStop(colors.DisabledBgDark, 1.0))
        
        rect.Fill <- if display.IsEnabled then enabledGradient :> IBrush else disabledGradient :> IBrush
        rect.Stroke <- SolidColorBrush(colors.Border) :> IBrush
        rect.StrokeThickness <- 1.5
        rect.RadiusX <- 8.0
        rect.RadiusY <- 8.0
        
        // Extract display number from device ID (e.g., "\\.\DISPLAY1" -> "1")
        let displayNumber = 
            let deviceName = display.Id.Replace(@"\\.\DISPLAY", "")
            if String.IsNullOrEmpty(deviceName) then "?" else deviceName
        
        // Get just the monitor name without the display number prefix
        let monitorName = 
            if display.Name.Contains("(") && display.Name.Contains(")") then
                let startIdx = display.Name.IndexOf("(") + 1
                let endIdx = display.Name.LastIndexOf(")")
                if endIdx > startIdx then
                    display.Name.Substring(startIdx, endIdx - startIdx)
                else
                    display.Name
            else
                display.Name
        
        // Create a stack panel for layered text (number, device name, resolution)
        let textStack = StackPanel()
        textStack.Orientation <- Orientation.Vertical
        textStack.HorizontalAlignment <- HorizontalAlignment.Center
        textStack.VerticalAlignment <- VerticalAlignment.Center
        
        // Display number - large and bold
        let numberLabel = TextBlock()
        numberLabel.Text <- displayNumber
        numberLabel.HorizontalAlignment <- HorizontalAlignment.Center
        numberLabel.TextAlignment <- TextAlignment.Center
        numberLabel.Foreground <- if Theme.currentTheme = Theme.Light then Brushes.White :> IBrush else SolidColorBrush(colors.Text) :> IBrush
        numberLabel.FontWeight <- FontWeight.Bold
        numberLabel.FontSize <- if width > 100.0 then 24.0 else 18.0
        numberLabel.Margin <- Thickness(0.0, 0.0, 0.0, 2.0)
        
        // Device name in parentheses
        let deviceLabel = TextBlock()
        deviceLabel.Text <- sprintf "(%s)" monitorName
        deviceLabel.HorizontalAlignment <- HorizontalAlignment.Center
        deviceLabel.TextAlignment <- TextAlignment.Center
        deviceLabel.Foreground <- if Theme.currentTheme = Theme.Light then Brushes.White :> IBrush else SolidColorBrush(colors.Text) :> IBrush
        deviceLabel.FontWeight <- FontWeight.Normal
        deviceLabel.FontSize <- if width > 100.0 then 10.0 else 8.0
        deviceLabel.Margin <- Thickness(0.0, 0.0, 0.0, 2.0)
        
        // Resolution
        let resolutionLabel = TextBlock()
        resolutionLabel.Text <- sprintf "%dx%d" display.Resolution.Width display.Resolution.Height
        resolutionLabel.HorizontalAlignment <- HorizontalAlignment.Center
        resolutionLabel.TextAlignment <- TextAlignment.Center
        resolutionLabel.Foreground <- if Theme.currentTheme = Theme.Light then Brushes.White :> IBrush else SolidColorBrush(colors.Text) :> IBrush
        resolutionLabel.FontWeight <- FontWeight.SemiBold
        resolutionLabel.FontSize <- if width > 100.0 then 9.0 else 7.0
        
        textStack.Children.Add(numberLabel)
        textStack.Children.Add(deviceLabel)
        textStack.Children.Add(resolutionLabel)
        
        // Create shadow effect for the entire stack
        let shadowStack = StackPanel()
        shadowStack.Orientation <- Orientation.Vertical
        shadowStack.HorizontalAlignment <- HorizontalAlignment.Center
        shadowStack.VerticalAlignment <- VerticalAlignment.Center
        shadowStack.Margin <- Thickness(1.0, 1.0, 0.0, 0.0)
        
        let shadowNumber = TextBlock()
        shadowNumber.Text <- numberLabel.Text
        shadowNumber.HorizontalAlignment <- numberLabel.HorizontalAlignment
        shadowNumber.TextAlignment <- numberLabel.TextAlignment
        shadowNumber.Foreground <- SolidColorBrush(Color.FromArgb(100uy, 0uy, 0uy, 0uy)) :> IBrush
        shadowNumber.FontWeight <- numberLabel.FontWeight
        shadowNumber.FontSize <- numberLabel.FontSize
        shadowNumber.Margin <- numberLabel.Margin
        
        let shadowDevice = TextBlock()
        shadowDevice.Text <- deviceLabel.Text
        shadowDevice.HorizontalAlignment <- deviceLabel.HorizontalAlignment
        shadowDevice.TextAlignment <- deviceLabel.TextAlignment
        shadowDevice.Foreground <- SolidColorBrush(Color.FromArgb(100uy, 0uy, 0uy, 0uy)) :> IBrush
        shadowDevice.FontWeight <- deviceLabel.FontWeight
        shadowDevice.FontSize <- deviceLabel.FontSize
        shadowDevice.Margin <- deviceLabel.Margin
        
        let shadowResolution = TextBlock()
        shadowResolution.Text <- resolutionLabel.Text
        shadowResolution.HorizontalAlignment <- resolutionLabel.HorizontalAlignment
        shadowResolution.TextAlignment <- resolutionLabel.TextAlignment
        shadowResolution.Foreground <- SolidColorBrush(Color.FromArgb(100uy, 0uy, 0uy, 0uy)) :> IBrush
        shadowResolution.FontWeight <- resolutionLabel.FontWeight
        shadowResolution.FontSize <- resolutionLabel.FontSize
        
        shadowStack.Children.Add(shadowNumber)
        shadowStack.Children.Add(shadowDevice)
        shadowStack.Children.Add(shadowResolution)
        
        let border = Border()
        border.Width <- width
        border.Height <- height
        border.Background <- Brushes.Transparent
        border.Cursor <- new Cursor(StandardCursorType.Hand)
        
        let grid = Grid()
        grid.Children.Add(rect)
        grid.Children.Add(shadowStack)
        grid.Children.Add(textStack)
        
        border.Child <- grid
        
        Canvas.SetLeft(border, float display.Position.X * scale)
        Canvas.SetTop(border, float display.Position.Y * scale)
        
        {
            Display = display
            Rectangle = rect
            Border = border
            Label = numberLabel
            EnableCheckBox = null
        }
    
    let createDisplayListView (displays: DisplayInfo list) (onDisplayToggle: DisplayId -> bool -> unit) (onSettingsClick: DisplayInfo -> unit) =
        let colors = Theme.getCurrentColors()
        let stackPanel = StackPanel()
        stackPanel.Orientation <- Orientation.Vertical
        stackPanel.Margin <- Thickness(15.0)
        
        for display in displays do
            // Debug output
            printfn "DEBUG: Creating card for display %s - Enabled: %b, Has Capabilities: %b" 
                display.Name display.IsEnabled display.Capabilities.IsSome
            
            // Extract display number from device ID
            let displayNumber = 
                let deviceName = display.Id.Replace(@"\\.\DISPLAY", "")
                if String.IsNullOrEmpty(deviceName) then "?" else deviceName
            
            // Get just the monitor name without the display number prefix
            let monitorName = 
                if display.Name.Contains("(") && display.Name.Contains(")") then
                    let startIdx = display.Name.IndexOf("(") + 1
                    let endIdx = display.Name.LastIndexOf(")")
                    if endIdx > startIdx then
                        display.Name.Substring(startIdx, endIdx - startIdx)
                    else
                        display.Name
                else
                    display.Name
            
            let displayCard = Border()
            displayCard.Background <- SolidColorBrush(if Theme.currentTheme = Theme.Light then Color.FromRgb(249uy, 250uy, 251uy) else colors.Surface) :> IBrush
            displayCard.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
            displayCard.BorderThickness <- Thickness(1.0)
            displayCard.CornerRadius <- CornerRadius(6.0)
            displayCard.Margin <- Thickness(0.0, 0.0, 0.0, 10.0)
            displayCard.Padding <- Thickness(12.0)
            displayCard.MinHeight <- 120.0
            displayCard.Cursor <- new Cursor(StandardCursorType.Hand)
            
            // Use vertical StackPanel for: number (top) -> text content (middle) -> buttons (bottom)
            let cardContent = StackPanel()
            cardContent.Orientation <- Orientation.Vertical
            cardContent.Spacing <- 8.0
            
            // Top: Display number badge (centered)
            let numberBadge = Border()
            numberBadge.Background <- SolidColorBrush(colors.Primary) :> IBrush
            numberBadge.CornerRadius <- CornerRadius(15.0)
            numberBadge.Width <- 30.0
            numberBadge.Height <- 30.0
            numberBadge.HorizontalAlignment <- HorizontalAlignment.Center
            
            let numberText = TextBlock()
            numberText.Text <- displayNumber
            numberText.HorizontalAlignment <- HorizontalAlignment.Center
            numberText.VerticalAlignment <- VerticalAlignment.Center
            numberText.FontWeight <- FontWeight.Bold
            numberText.FontSize <- 14.0
            numberText.Foreground <- Brushes.White
            numberBadge.Child <- numberText
            cardContent.Children.Add(numberBadge)
            
            // Middle: Display information text (centered)
            let displayInfo = StackPanel()
            displayInfo.Orientation <- Orientation.Vertical
            displayInfo.HorizontalAlignment <- HorizontalAlignment.Center
            displayInfo.Spacing <- 2.0
            
            let nameText = TextBlock()
            nameText.Text <- monitorName
            nameText.FontWeight <- FontWeight.SemiBold
            nameText.FontSize <- 13.0
            nameText.Foreground <- SolidColorBrush(colors.Text) :> IBrush
            nameText.TextAlignment <- TextAlignment.Center
            nameText.TextWrapping <- TextWrapping.Wrap
            nameText.MaxWidth <- 180.0
            displayInfo.Children.Add(nameText)
            
            let resolutionText = TextBlock()
            resolutionText.Text <- sprintf "%dx%d @ %dHz" display.Resolution.Width display.Resolution.Height display.Resolution.RefreshRate
            resolutionText.FontSize <- 11.0
            resolutionText.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
            resolutionText.TextAlignment <- TextAlignment.Center
            resolutionText.TextWrapping <- TextWrapping.Wrap
            displayInfo.Children.Add(resolutionText)
            
            let statusText = TextBlock()
            statusText.Text <- sprintf "%s • %s" (if display.IsPrimary then "Primary" else "Secondary") (if display.IsEnabled then "Active" else "Inactive")
            statusText.FontSize <- 10.0
            statusText.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
            statusText.TextAlignment <- TextAlignment.Center
            statusText.TextWrapping <- TextWrapping.Wrap
            displayInfo.Children.Add(statusText)
            
            let positionText = TextBlock()
            positionText.Text <- sprintf "Position: (%d, %d)" display.Position.X display.Position.Y
            positionText.FontSize <- 9.0
            positionText.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
            positionText.TextAlignment <- TextAlignment.Center
            positionText.TextWrapping <- TextWrapping.Wrap
            displayInfo.Children.Add(positionText)
            
            cardContent.Children.Add(displayInfo)
            
            // Bottom: Button panel for toggle and settings (centered horizontally)
            let buttonPanel = StackPanel()
            buttonPanel.Orientation <- Orientation.Horizontal
            buttonPanel.HorizontalAlignment <- HorizontalAlignment.Center
            buttonPanel.Spacing <- 8.0
            
            // Settings gear button (only for enabled displays with capabilities)
            if display.IsEnabled && display.Capabilities.IsSome then
                let settingsButton = Button()
                settingsButton.Content <- "⚙️" // Use full emoji
                settingsButton.Width <- 32.0  // Slightly wider for emoji
                settingsButton.Height <- 30.0
                settingsButton.FontSize <- 14.0  // Smaller font size
                settingsButton.CornerRadius <- CornerRadius(15.0)
                settingsButton.Background <- SolidColorBrush(colors.Surface) :> IBrush
                settingsButton.Foreground <- SolidColorBrush(colors.Text) :> IBrush
                settingsButton.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
                settingsButton.BorderThickness <- Thickness(1.0)
                settingsButton.Cursor <- new Cursor(StandardCursorType.Hand)
                settingsButton.HorizontalContentAlignment <- HorizontalAlignment.Center
                settingsButton.VerticalContentAlignment <- VerticalAlignment.Center
                settingsButton.Padding <- Thickness(0.0)
                ToolTip.SetTip(settingsButton, "Display Settings")
                
                // Call the settings callback
                settingsButton.Click.Add(fun _ ->
                    printfn "DEBUG: Opening settings for display: %s" display.Name
                    onSettingsClick display
                )
                
                buttonPanel.Children.Add(settingsButton)
            
            let toggleButton = Button()
            toggleButton.Content <- if display.IsEnabled then "✓" else "✗"
            toggleButton.Width <- 30.0
            toggleButton.Height <- 30.0
            toggleButton.FontSize <- 14.0
            toggleButton.CornerRadius <- CornerRadius(15.0)
            toggleButton.Background <- 
                if display.IsEnabled then 
                    SolidColorBrush(Color.FromRgb(34uy, 197uy, 94uy)) :> IBrush
                else 
                    SolidColorBrush(Color.FromRgb(239uy, 68uy, 68uy)) :> IBrush
            toggleButton.Foreground <- Brushes.White
            toggleButton.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
            
            toggleButton.Click.Add(fun _ ->
                onDisplayToggle display.Id (not display.IsEnabled)
            )
            
            buttonPanel.Children.Add(toggleButton)
            cardContent.Children.Add(buttonPanel)
            displayCard.Child <- cardContent
            stackPanel.Children.Add(displayCard)
        
        stackPanel

    // Resolution picker dialog window for selecting display modes
    let createResolutionPickerDialog (display: DisplayInfo) (onApply: DisplayId -> DisplayMode -> unit) (onClose: unit -> unit) =
        let colors = Theme.getCurrentColors()
        
        // Create dialog window
        let dialogWindow = Window()
        dialogWindow.Title <- sprintf "Display Settings - %s" display.Name
        dialogWindow.Width <- 700.0
        dialogWindow.Height <- 550.0
        dialogWindow.MinWidth <- 600.0
        dialogWindow.MinHeight <- 400.0
        dialogWindow.CanResize <- true
        dialogWindow.WindowStartupLocation <- WindowStartupLocation.CenterScreen
        dialogWindow.Background <- SolidColorBrush(colors.Background)
        
        // Set icon (if available)
        try
            dialogWindow.Icon <- WindowIcon("icon.ico")
        with
        | _ -> () // Ignore if icon file not found
        
        // Modal content
        let contentGrid = Grid()
        contentGrid.Margin <- Thickness(20.0)
        contentGrid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto)) // Header
        contentGrid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto)) // Current mode info
        contentGrid.RowDefinitions.Add(RowDefinition(Height = GridLength.Star)) // Resolution panels
        contentGrid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto)) // Buttons
        
        // Header with title (no close button for modeless dialog)
        let headerPanel = StackPanel()
        headerPanel.Orientation <- Orientation.Vertical
        Grid.SetRow(headerPanel, 0)
        
        let titleText = TextBlock()
        titleText.Text <- sprintf "🖥️ %s Settings" display.Name
        titleText.FontSize <- 18.0
        titleText.FontWeight <- FontWeight.Bold
        titleText.Foreground <- SolidColorBrush(colors.Text)
        titleText.HorizontalAlignment <- HorizontalAlignment.Center
        titleText.Margin <- Thickness(0.0, 0.0, 0.0, 10.0)
        headerPanel.Children.Add(titleText)
        
        contentGrid.Children.Add(headerPanel)
        
        // Current mode display
        let currentModePanel = Border()
        currentModePanel.Background <- SolidColorBrush(colors.Surface)
        currentModePanel.CornerRadius <- CornerRadius(6.0)
        currentModePanel.Padding <- Thickness(10.0)
        currentModePanel.Margin <- Thickness(0.0, 10.0, 0.0, 10.0)
        Grid.SetRow(currentModePanel, 1)
        
        let currentModeText = TextBlock()
        match display.Capabilities with
        | Some caps ->
            currentModeText.Text <- sprintf "Current: %dx%d @ %dHz" 
                caps.CurrentMode.Width 
                caps.CurrentMode.Height 
                caps.CurrentMode.RefreshRate
        | None ->
            currentModeText.Text <- sprintf "Current: %dx%d @ %dHz" 
                display.Resolution.Width 
                display.Resolution.Height 
                display.Resolution.RefreshRate
        currentModeText.FontSize <- 14.0
        currentModeText.Foreground <- SolidColorBrush(colors.Text)
        currentModeText.HorizontalAlignment <- HorizontalAlignment.Center
        currentModePanel.Child <- currentModeText
        
        contentGrid.Children.Add(currentModePanel)
        
        // Scrollable content area for resolution panels
        let contentScrollViewer = ScrollViewer()
        contentScrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
        contentScrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Disabled
        Grid.SetRow(contentScrollViewer, 2)
        
        // Placeholder for resolution/refresh rate panels (Phase 2.3)
        let panelsPlaceholder = TextBlock()
        panelsPlaceholder.Text <- "Resolution and refresh rate selection panels will go here"
        panelsPlaceholder.Foreground <- SolidColorBrush(colors.TextSecondary)
        panelsPlaceholder.HorizontalAlignment <- HorizontalAlignment.Center
        panelsPlaceholder.VerticalAlignment <- VerticalAlignment.Center
        panelsPlaceholder.Margin <- Thickness(20.0)
        
        contentScrollViewer.Content <- panelsPlaceholder
        contentGrid.Children.Add(contentScrollViewer)
        
        // Bottom buttons
        let buttonPanel = StackPanel()
        buttonPanel.Orientation <- Orientation.Horizontal
        buttonPanel.HorizontalAlignment <- HorizontalAlignment.Right
        buttonPanel.Margin <- Thickness(0.0, 10.0, 0.0, 0.0)
        Grid.SetRow(buttonPanel, 3)
        
        let cancelButton = Button()
        cancelButton.Content <- "Cancel"
        cancelButton.Padding <- Thickness(20.0, 8.0, 20.0, 8.0)
        cancelButton.Margin <- Thickness(0.0, 0.0, 10.0, 0.0)
        cancelButton.Background <- SolidColorBrush(colors.Surface)
        cancelButton.Foreground <- SolidColorBrush(colors.Text)
        cancelButton.BorderBrush <- SolidColorBrush(colors.Border)
        cancelButton.BorderThickness <- Thickness(1.0)
        cancelButton.CornerRadius <- CornerRadius(6.0)
        cancelButton.Cursor <- new Cursor(StandardCursorType.Hand)
        cancelButton.Click.Add(fun _ -> 
            dialogWindow.Close()
            onClose())
        buttonPanel.Children.Add(cancelButton)
        
        let testButton = Button()
        testButton.Content <- "Test 15s"
        testButton.Padding <- Thickness(20.0, 8.0, 20.0, 8.0)
        testButton.Margin <- Thickness(0.0, 0.0, 10.0, 0.0)
        testButton.Background <- SolidColorBrush(colors.Secondary)
        testButton.Foreground <- Brushes.White
        testButton.BorderThickness <- Thickness(0.0)
        testButton.CornerRadius <- CornerRadius(6.0)
        testButton.Cursor <- new Cursor(StandardCursorType.Hand)
        testButton.IsEnabled <- false // Will enable when selection is made
        buttonPanel.Children.Add(testButton)
        
        let applyButton = Button()
        applyButton.Content <- "Apply"
        applyButton.Padding <- Thickness(20.0, 8.0, 20.0, 8.0)
        applyButton.Background <- SolidColorBrush(colors.Primary)
        applyButton.Foreground <- Brushes.White
        applyButton.BorderThickness <- Thickness(0.0)
        applyButton.CornerRadius <- CornerRadius(6.0)
        applyButton.Cursor <- new Cursor(StandardCursorType.Hand)
        applyButton.IsEnabled <- false // Will enable when selection is made
        buttonPanel.Children.Add(applyButton)
        
        contentGrid.Children.Add(buttonPanel)
        
        // Set window content
        dialogWindow.Content <- contentGrid
        
        // Handle window closing
        dialogWindow.Closing.Add(fun _ -> onClose())
        
        // Return the dialog window
        dialogWindow

    let createPresetPanel (presets: string list) (onPresetClick: string -> unit) (onPresetDelete: string -> unit) =
        let colors = Theme.getCurrentColors()
        let mainPanel = StackPanel()
        mainPanel.Orientation <- Orientation.Vertical
        mainPanel.Margin <- Thickness(15.0)
        
        let titleText = TextBlock()
        titleText.Text <- "💾 Display Presets"
        titleText.FontWeight <- FontWeight.Bold
        titleText.FontSize <- 16.0
        titleText.Foreground <- SolidColorBrush(colors.Text) :> IBrush
        titleText.Margin <- Thickness(0.0, 0.0, 0.0, 15.0)
        mainPanel.Children.Add(titleText)
        
        let saveButton = Button()
        saveButton.Content <- "➕ Save Current Layout"
        saveButton.Margin <- Thickness(0.0, 0.0, 0.0, 15.0)
        saveButton.Height <- 40.0
        saveButton.FontSize <- 14.0
        saveButton.FontWeight <- FontWeight.SemiBold
        saveButton.CornerRadius <- CornerRadius(6.0)
        saveButton.HorizontalAlignment <- HorizontalAlignment.Stretch
        
        let saveGradient = LinearGradientBrush()
        saveGradient.StartPoint <- RelativePoint(0.0, 0.0, RelativeUnit.Relative)
        saveGradient.EndPoint <- RelativePoint(0.0, 1.0, RelativeUnit.Relative)
        saveGradient.GradientStops.Add(GradientStop(colors.Secondary, 0.0))
        saveGradient.GradientStops.Add(GradientStop(colors.SecondaryDark, 1.0))
        saveButton.Background <- saveGradient :> IBrush
        
        saveButton.BorderBrush <- SolidColorBrush(colors.SecondaryDark) :> IBrush
        saveButton.BorderThickness <- Thickness(1.0)
        saveButton.Foreground <- Brushes.White
        saveButton.Click.Add(fun _ -> onPresetClick "SAVE_NEW")
        mainPanel.Children.Add(saveButton)
        
        let listHeader = TextBlock()
        listHeader.Text <- "Saved Layouts:"
        listHeader.FontWeight <- FontWeight.Medium
        listHeader.FontSize <- 13.0
        listHeader.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
        listHeader.Margin <- Thickness(0.0, 0.0, 0.0, 8.0)
        mainPanel.Children.Add(listHeader)
        
        let scrollViewer = ScrollViewer()
        scrollViewer.Height <- 300.0
        scrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Disabled
        scrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
        
        let presetList = StackPanel()
        presetList.Orientation <- Orientation.Vertical
        
        if presets.IsEmpty then
            let emptyMessage = TextBlock()
            emptyMessage.Text <- "No saved layouts yet"
            emptyMessage.FontSize <- 12.0
            emptyMessage.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
            emptyMessage.TextAlignment <- TextAlignment.Center
            emptyMessage.Margin <- Thickness(0.0, 20.0, 0.0, 0.0)
            presetList.Children.Add(emptyMessage)
        else
            for preset in presets do
                let presetCard = Border()
                presetCard.Background <- SolidColorBrush(if Theme.currentTheme = Theme.Light then Color.FromRgb(249uy, 250uy, 251uy) else colors.Surface) :> IBrush
                presetCard.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
                presetCard.BorderThickness <- Thickness(1.0)
                presetCard.CornerRadius <- CornerRadius(6.0)
                presetCard.Margin <- Thickness(0.0, 0.0, 0.0, 6.0)
                presetCard.Padding <- Thickness(10.0, 8.0, 8.0, 8.0)
                
                let cardGrid = Grid()
                cardGrid.ColumnDefinitions.Add(ColumnDefinition())
                cardGrid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
                
                let presetButton = Button()
                presetButton.Content <- preset
                presetButton.Background <- Brushes.Transparent
                presetButton.BorderThickness <- Thickness(0.0)
                presetButton.HorizontalAlignment <- HorizontalAlignment.Stretch
                presetButton.HorizontalContentAlignment <- HorizontalAlignment.Left
                presetButton.FontSize <- 13.0
                presetButton.Foreground <- SolidColorBrush(colors.Text) :> IBrush
                presetButton.Cursor <- new Cursor(StandardCursorType.Hand)
                presetButton.Click.Add(fun _ -> onPresetClick preset)
                Grid.SetColumn(presetButton, 0)
                
                let deleteButton = Button()
                deleteButton.Content <- "✕"
                deleteButton.Width <- 24.0
                deleteButton.Height <- 24.0
                deleteButton.FontSize <- 12.0
                deleteButton.CornerRadius <- CornerRadius(12.0)
                deleteButton.Background <- SolidColorBrush(Color.FromRgb(239uy, 68uy, 68uy)) :> IBrush
                deleteButton.Foreground <- Brushes.White
                deleteButton.BorderThickness <- Thickness(0.0)
                deleteButton.Cursor <- new Cursor(StandardCursorType.Hand)
                ToolTip.SetTip(deleteButton, "Delete this preset")
                deleteButton.Click.Add(fun _ -> onPresetDelete preset)
                Grid.SetColumn(deleteButton, 1)
                
                cardGrid.Children.Add(presetButton)
                cardGrid.Children.Add(deleteButton)
                presetCard.Child <- cardGrid
                presetList.Children.Add(presetCard)
        
        scrollViewer.Content <- presetList
        mainPanel.Children.Add(scrollViewer)
        
        mainPanel