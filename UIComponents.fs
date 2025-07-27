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
        
        let label = TextBlock()
        label.Text <- sprintf "%s\n%dx%d" display.Name display.Resolution.Width display.Resolution.Height
        label.HorizontalAlignment <- HorizontalAlignment.Center
        label.VerticalAlignment <- VerticalAlignment.Center
        label.TextAlignment <- TextAlignment.Center
        label.Foreground <- if Theme.currentTheme = Theme.Light then Brushes.White :> IBrush else SolidColorBrush(colors.Text) :> IBrush
        label.FontWeight <- FontWeight.SemiBold
        label.FontSize <- if width > 100.0 then 12.0 else 10.0
        
        let textShadow = TextBlock()
        textShadow.Text <- label.Text
        textShadow.HorizontalAlignment <- label.HorizontalAlignment
        textShadow.VerticalAlignment <- label.VerticalAlignment
        textShadow.TextAlignment <- label.TextAlignment
        textShadow.Foreground <- SolidColorBrush(Color.FromArgb(100uy, 0uy, 0uy, 0uy)) :> IBrush
        textShadow.FontWeight <- label.FontWeight
        textShadow.FontSize <- label.FontSize
        textShadow.Margin <- Thickness(1.0, 1.0, 0.0, 0.0)
        
        let border = Border()
        border.Width <- width
        border.Height <- height
        border.Background <- Brushes.Transparent
        border.Cursor <- new Cursor(StandardCursorType.Hand)
        
        let grid = Grid()
        grid.Children.Add(rect)
        grid.Children.Add(textShadow)
        grid.Children.Add(label)
        
        border.Child <- grid
        
        Canvas.SetLeft(border, float display.Position.X * scale)
        Canvas.SetTop(border, float display.Position.Y * scale)
        
        {
            Display = display
            Rectangle = rect
            Border = border
            Label = label
            EnableCheckBox = null
        }
    
    let createDisplayListView (displays: DisplayInfo list) (onDisplayToggle: DisplayId -> bool -> unit) =
        let colors = Theme.getCurrentColors()
        let stackPanel = StackPanel()
        stackPanel.Orientation <- Orientation.Vertical
        stackPanel.Margin <- Thickness(15.0)
        
        for display in displays do
            let displayCard = Border()
            displayCard.Background <- SolidColorBrush(if Theme.currentTheme = Theme.Light then Color.FromRgb(249uy, 250uy, 251uy) else colors.Surface) :> IBrush
            displayCard.BorderBrush <- SolidColorBrush(colors.Border) :> IBrush
            displayCard.BorderThickness <- Thickness(1.0)
            displayCard.CornerRadius <- CornerRadius(6.0)
            displayCard.Margin <- Thickness(0.0, 0.0, 0.0, 8.0)
            displayCard.Padding <- Thickness(12.0, 10.0, 12.0, 10.0)
            displayCard.Cursor <- new Cursor(StandardCursorType.Hand)
            
            let cardContent = Grid()
            cardContent.ColumnDefinitions.Add(ColumnDefinition())
            cardContent.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
            
            let displayContent = StackPanel()
            displayContent.Orientation <- Orientation.Vertical
            Grid.SetColumn(displayContent, 0)
            
            let nameText = TextBlock()
            nameText.Text <- display.Name
            nameText.FontWeight <- FontWeight.SemiBold
            nameText.FontSize <- 13.0
            nameText.Foreground <- SolidColorBrush(colors.Text) :> IBrush
            nameText.Margin <- Thickness(0.0, 0.0, 0.0, 2.0)
            displayContent.Children.Add(nameText)
            
            let resolutionText = TextBlock()
            resolutionText.Text <- sprintf "%dx%d" display.Resolution.Width display.Resolution.Height
            resolutionText.FontSize <- 11.0
            resolutionText.Foreground <- SolidColorBrush(colors.TextSecondary) :> IBrush
            displayContent.Children.Add(resolutionText)
            
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
            Grid.SetColumn(toggleButton, 1)
            
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