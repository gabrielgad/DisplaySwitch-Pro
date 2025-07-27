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
    
    let createDisplayListView (displays: DisplayInfo list) (onDisplayToggle: DisplayId -> bool -> unit) =
        let colors = Theme.getCurrentColors()
        let stackPanel = StackPanel()
        stackPanel.Orientation <- Orientation.Vertical
        stackPanel.Margin <- Thickness(15.0)
        
        for display in displays do
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
            displayCard.Padding <- Thickness(12.0, 12.0, 12.0, 12.0)
            displayCard.MinHeight <- 80.0
            displayCard.Cursor <- new Cursor(StandardCursorType.Hand)
            
            let cardContent = Grid()
            cardContent.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
            cardContent.ColumnDefinitions.Add(ColumnDefinition())
            cardContent.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
            
            // Display number badge
            let numberBadge = Border()
            numberBadge.Background <- SolidColorBrush(colors.Primary) :> IBrush
            numberBadge.CornerRadius <- CornerRadius(12.0)
            numberBadge.Width <- 24.0
            numberBadge.Height <- 24.0
            numberBadge.Margin <- Thickness(0.0, 0.0, 10.0, 0.0)
            Grid.SetColumn(numberBadge, 0)
            
            let numberText = TextBlock()
            numberText.Text <- displayNumber
            numberText.HorizontalAlignment <- HorizontalAlignment.Center
            numberText.VerticalAlignment <- VerticalAlignment.Center
            numberText.FontWeight <- FontWeight.Bold
            numberText.FontSize <- 12.0
            numberText.Foreground <- Brushes.White
            numberBadge.Child <- numberText
            
            let displayContent = StackPanel()
            displayContent.Orientation <- Orientation.Vertical
            Grid.SetColumn(displayContent, 1)
            
            let nameText = TextBlock()
            nameText.Text <- sprintf "%s %s" displayNumber monitorName
            nameText.FontWeight <- FontWeight.SemiBold
            nameText.FontSize <- 13.0
            nameText.Foreground <- SolidColorBrush(colors.Text) :> IBrush
            nameText.Margin <- Thickness(0.0, 0.0, 0.0, 4.0)
            nameText.TextWrapping <- TextWrapping.Wrap
            nameText.MaxWidth <- 200.0
            displayContent.Children.Add(nameText)
            
            let resolutionText = TextBlock()
            resolutionText.Text <- sprintf "%dx%d @ %dHz" display.Resolution.Width display.Resolution.Height display.Resolution.RefreshRate
            resolutionText.FontSize <- 11.0
            resolutionText.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
            resolutionText.Margin <- Thickness(0.0, 0.0, 0.0, 2.0)
            resolutionText.TextWrapping <- TextWrapping.Wrap
            displayContent.Children.Add(resolutionText)
            
            let statusText = TextBlock()
            statusText.Text <- sprintf "%s • %s" (if display.IsPrimary then "Primary" else "Secondary") (if display.IsEnabled then "Active" else "Inactive")
            statusText.FontSize <- 10.0
            statusText.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
            statusText.Margin <- Thickness(0.0, 0.0, 0.0, 2.0)
            statusText.TextWrapping <- TextWrapping.Wrap
            displayContent.Children.Add(statusText)
            
            let positionText = TextBlock()
            positionText.Text <- sprintf "Position: (%d, %d)" display.Position.X display.Position.Y
            positionText.FontSize <- 9.0
            positionText.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
            positionText.TextWrapping <- TextWrapping.Wrap
            displayContent.Children.Add(positionText)
            
            cardContent.Children.Add(numberBadge)
            cardContent.Children.Add(displayContent)
            
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
            Grid.SetColumn(toggleButton, 2)
            
            toggleButton.Click.Add(fun _ ->
                onDisplayToggle display.Id (not display.IsEnabled)
            )
            
            cardContent.Children.Add(toggleButton)
            displayCard.Child <- cardContent
            stackPanel.Children.Add(displayCard)
        
        stackPanel

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