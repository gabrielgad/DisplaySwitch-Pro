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
    
    // Refresh function reference - set by the main GUI module
    let mutable refreshMainWindowContentRef: (unit -> unit) option = None
    
    // Set the refresh function reference
    let setRefreshFunction refreshFunc =
        refreshMainWindowContentRef <- Some refreshFunc
    
    // Helper function to call refresh
    let private refreshMainWindowContent() =
        match refreshMainWindowContentRef with
        | Some refreshFunc -> refreshFunc()
        | None -> Logging.logErrorf "WARNING: UIComponents refresh function not set"
    
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
        rect.Opacity <- if display.IsEnabled then 1.0 else 0.5 // Make disabled displays more obviously different
        rect.Stroke <- SolidColorBrush(colors.Border) :> IBrush
        rect.StrokeThickness <- 1.5
        rect.RadiusX <- 8.0
        rect.RadiusY <- 8.0
        
        // Extract Windows Display Number from new ID format (e.g., "Display1" -> "1")
        let displayNumber =
            if display.Id.StartsWith("Display") then
                display.Id.Substring(7)  // Remove "Display" prefix
            else
                // Fallback for old format or unexpected format
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
        
        displays |> List.iter (fun display ->
            // Debug output
            printfn "DEBUG: Creating card for display %s - Enabled: %b, Has Capabilities: %b" 
                display.Name display.IsEnabled display.Capabilities.IsSome
            
            // Extract Windows Display Number from new ID format (e.g., "Display1" -> "1")
            let displayNumber =
                if display.Id.StartsWith("Display") then
                    display.Id.Substring(7)  // Remove "Display" prefix
                else
                    // Fallback for old format or unexpected format
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
            // Show different status based on whether it's actually inactive vs software disabled
            let statusDescription = 
                if display.Name.Contains("[Inactive]") then
                    "Hardware Inactive" // Actually disconnected/inactive
                else if display.IsEnabled then
                    "Active"
                else
                    "Software Disabled" // Disabled via application
            statusText.Text <- sprintf "%s • %s" (if display.IsPrimary then "Primary" else "Secondary") statusDescription
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
        )
        
        stackPanel

    // Creates just the content for the resolution picker dialog (for updating existing dialogs)
    let createResolutionPickerDialogContent (display: DisplayInfo) (onApply: DisplayId -> DisplayMode -> DisplayOrientation -> bool -> unit) (onClose: unit -> unit) =
        let colors = Theme.getCurrentColors()
        
        // Modal content
        let contentGrid = Grid()
        contentGrid.Margin <- Thickness(20.0)
        contentGrid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto)) // Header
        contentGrid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto)) // Current mode info
        contentGrid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto)) // Orientation & Primary controls
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
        
        let currentModeStack = StackPanel()
        currentModeStack.Orientation <- Orientation.Vertical
        currentModeStack.HorizontalAlignment <- HorizontalAlignment.Center
        
        let currentModeText = TextBlock()
        // Create a mutable reference to track current mode for updates
        let mutable currentDisplayMode = 
            match display.Capabilities with
            | Some caps -> caps.CurrentMode
            | None -> { Width = display.Resolution.Width; Height = display.Resolution.Height; RefreshRate = display.Resolution.RefreshRate; BitsPerPixel = 32 }
        
        let updateCurrentModeDisplay () =
            currentModeText.Text <- sprintf "Current: %dx%d @ %dHz" 
                currentDisplayMode.Width 
                currentDisplayMode.Height 
                currentDisplayMode.RefreshRate
        
        updateCurrentModeDisplay()
        currentModeText.FontSize <- 14.0
        currentModeText.Foreground <- SolidColorBrush(colors.Text)
        currentModeText.HorizontalAlignment <- HorizontalAlignment.Center
        currentModeStack.Children.Add(currentModeText)
        
        let statusText = TextBlock()
        let orientationStr = match display.Orientation with
                             | Landscape -> "Landscape"
                             | Portrait -> "Portrait" 
                             | LandscapeFlipped -> "Landscape (Flipped)"
                             | PortraitFlipped -> "Portrait (Flipped)"
        let primaryStr = if display.IsPrimary then " • Primary Display" else ""
        statusText.Text <- sprintf "%s%s" orientationStr primaryStr
        statusText.FontSize <- 12.0
        statusText.Foreground <- SolidColorBrush(colors.TextSecondary)
        statusText.HorizontalAlignment <- HorizontalAlignment.Center
        statusText.Margin <- Thickness(0.0, 5.0, 0.0, 0.0)
        currentModeStack.Children.Add(statusText)
        
        currentModePanel.Child <- currentModeStack
        
        contentGrid.Children.Add(currentModePanel)
        
        // Orientation and Primary controls section
        let controlsPanel = Border()
        controlsPanel.Background <- SolidColorBrush(colors.Surface)
        controlsPanel.CornerRadius <- CornerRadius(6.0)
        controlsPanel.Padding <- Thickness(15.0, 10.0)
        controlsPanel.Margin <- Thickness(0.0, 10.0, 0.0, 10.0)
        Grid.SetRow(controlsPanel, 2)
        
        let controlsGrid = Grid()
        controlsGrid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star)) // Orientation controls
        controlsGrid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(20.0, GridUnitType.Pixel))) // Spacing
        controlsGrid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star)) // Primary controls
        
        // Mutable state for orientation and primary tracking
        let mutable selectedOrientation = display.Orientation
        let mutable selectedIsPrimary = display.IsPrimary
        
        // Left: Orientation controls
        let orientationSection = StackPanel()
        orientationSection.Orientation <- Orientation.Vertical
        Grid.SetColumn(orientationSection, 0)
        
        let orientationTitle = TextBlock()
        orientationTitle.Text <- "🔄 Orientation"
        orientationTitle.FontSize <- 14.0
        orientationTitle.FontWeight <- FontWeight.SemiBold
        orientationTitle.Foreground <- SolidColorBrush(colors.Text)
        orientationTitle.Margin <- Thickness(0.0, 0.0, 0.0, 8.0)
        orientationSection.Children.Add(orientationTitle)
        
        let orientationButtons = StackPanel()
        orientationButtons.Orientation <- Orientation.Horizontal
        orientationButtons.HorizontalAlignment <- HorizontalAlignment.Center
        orientationButtons.Spacing <- 5.0
        
        // Create orientation buttons
        let orientationOptions = [
            (Landscape, "📺", "Landscape")
            (Portrait, "📱", "Portrait")
            (LandscapeFlipped, "🔄", "Landscape ↻")
            (PortraitFlipped, "🔃", "Portrait ↻")
        ]
        
        for (orientation, icon, tooltip) in orientationOptions do
            let orientationButton = Border()
            orientationButton.Width <- 50.0
            orientationButton.Height <- 40.0
            orientationButton.CornerRadius <- CornerRadius(6.0)
            orientationButton.BorderThickness <- Thickness(1.0)
            orientationButton.BorderBrush <- SolidColorBrush(colors.Border)
            orientationButton.Cursor <- new Cursor(StandardCursorType.Hand)
            
            let isSelected = orientation = selectedOrientation
            orientationButton.Background <- if isSelected then 
                                              SolidColorBrush(colors.Primary) 
                                            else 
                                              SolidColorBrush(Color.FromArgb(50uy, colors.Primary.R, colors.Primary.G, colors.Primary.B))
            
            let buttonText = TextBlock()
            buttonText.Text <- icon
            buttonText.FontSize <- 16.0
            buttonText.HorizontalAlignment <- HorizontalAlignment.Center
            buttonText.VerticalAlignment <- VerticalAlignment.Center
            buttonText.Foreground <- if isSelected then SolidColorBrush(Colors.White) else SolidColorBrush(colors.Text)
            orientationButton.Child <- buttonText
            
            ToolTip.SetTip(orientationButton, tooltip)
            
            orientationButton.PointerPressed.Add(fun _ ->
                selectedOrientation <- orientation
                
                // Update all orientation button styles
                for child in orientationButtons.Children do
                    if child :? Border then
                        let border = child :?> Border
                        border.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Primary.R, colors.Primary.G, colors.Primary.B))
                        if border.Child :? TextBlock then
                            let textBlock = border.Child :?> TextBlock
                            textBlock.Foreground <- SolidColorBrush(colors.Text)
                
                // Highlight selected orientation
                orientationButton.Background <- SolidColorBrush(colors.Primary)
                buttonText.Foreground <- SolidColorBrush(Colors.White)
                
                printfn "DEBUG: Selected orientation: %A" orientation
            )
            
            orientationButtons.Children.Add(orientationButton)
        
        orientationSection.Children.Add(orientationButtons)
        controlsGrid.Children.Add(orientationSection)
        
        // Right: Primary display toggle
        let primarySection = StackPanel()
        primarySection.Orientation <- Orientation.Vertical
        Grid.SetColumn(primarySection, 2)
        
        let primaryTitle = TextBlock()
        primaryTitle.Text <- "⭐ Primary Display"
        primaryTitle.FontSize <- 14.0
        primaryTitle.FontWeight <- FontWeight.SemiBold
        primaryTitle.Foreground <- SolidColorBrush(colors.Text)
        primaryTitle.Margin <- Thickness(0.0, 0.0, 0.0, 8.0)
        primarySection.Children.Add(primaryTitle)
        
        let primaryToggle = Border()
        primaryToggle.Height <- 40.0
        primaryToggle.CornerRadius <- CornerRadius(6.0)
        primaryToggle.BorderThickness <- Thickness(1.0)
        primaryToggle.BorderBrush <- SolidColorBrush(colors.Border)
        primaryToggle.Cursor <- new Cursor(StandardCursorType.Hand)
        primaryToggle.HorizontalAlignment <- HorizontalAlignment.Center
        primaryToggle.Padding <- Thickness(15.0, 0.0)
        
        primaryToggle.Background <- if selectedIsPrimary then 
                                      SolidColorBrush(colors.Secondary) 
                                    else 
                                      SolidColorBrush(Color.FromArgb(50uy, colors.Secondary.R, colors.Secondary.G, colors.Secondary.B))
        
        let primaryText = TextBlock()
        primaryText.Text <- if selectedIsPrimary then "✓ Primary" else "Set as Primary"
        primaryText.FontSize <- 14.0
        primaryText.HorizontalAlignment <- HorizontalAlignment.Center
        primaryText.VerticalAlignment <- VerticalAlignment.Center
        primaryText.Foreground <- if selectedIsPrimary then SolidColorBrush(Colors.White) else SolidColorBrush(colors.Text)
        primaryToggle.Child <- primaryText
        
        primaryToggle.PointerPressed.Add(fun _ ->
            // Only allow setting as primary, not unsetting (Windows always needs a primary display)
            if not selectedIsPrimary then
                selectedIsPrimary <- true
                
                Logging.logVerbosef "UIComponents: Setting %s as primary display immediately" display.Id
                
                // Call the Windows API to actually set as primary
                match DisplayControl.setPrimaryDisplay display.Id with
                | Ok () ->
                    Logging.logNormalf "SUCCESS: Successfully set %s as primary display" display.Id
                    
                    // Update UI state
                    primaryToggle.Background <- SolidColorBrush(colors.Secondary)
                    primaryText.Text <- "✓ Primary"
                    primaryText.Foreground <- SolidColorBrush(Colors.White)
                    
                    // Refresh the UI to reflect the primary display change
                    Logging.logNormalf " Primary display set successfully. Refreshing UI to show changes."
                    
                    // Call the refresh function to reload display state from Windows and update UI
                    refreshMainWindowContent()
                    
                | Error err ->
                    Logging.logErrorf " Failed to set %s as primary: %s" display.Id err
                    // Revert UI state on failure
                    selectedIsPrimary <- false
                    primaryToggle.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Secondary.R, colors.Secondary.G, colors.Secondary.B))
                    primaryText.Text <- "Set as Primary"
                    primaryText.Foreground <- SolidColorBrush(colors.Text)
            else
                Logging.logVerbosef "UIComponents: %s is already primary - no action needed" display.Id
        )
        
        primarySection.Children.Add(primaryToggle)
        controlsGrid.Children.Add(primarySection)
        
        controlsPanel.Child <- controlsGrid
        contentGrid.Children.Add(controlsPanel)
        
        // Scrollable content area for resolution panels
        let contentScrollViewer = ScrollViewer()
        contentScrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
        contentScrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Disabled
        Grid.SetRow(contentScrollViewer, 3)
        
        // Two-panel layout: resolutions (left) + refresh rates (right)
        let panelsGrid = Grid()
        panelsGrid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star)) // Resolutions
        panelsGrid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(10.0, GridUnitType.Pixel))) // Spacing
        panelsGrid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star)) // Refresh rates
        panelsGrid.Margin <- Thickness(10.0)
        
        // Mutable state for selection tracking
        let mutable selectedResolution: (int * int) option = None
        let mutable selectedRefreshRate: int option = None
        let mutable refreshRatePanel: StackPanel option = None
        
        // Define button state update function early (will be populated with actual buttons later)
        let mutable testButton: Border option = None
        let mutable applyButton: Border option = None
        let mutable testText: TextBlock option = None
        let mutable applyText: TextBlock option = None
        
        let updateButtonStates () =
            let isSelectionMade = selectedResolution.IsSome && selectedRefreshRate.IsSome
            Logging.logVerbosef "UIComponents: updateButtonStates called - Resolution: %A, RefreshRate: %A, SelectionMade: %b" 
                    selectedResolution selectedRefreshRate isSelectionMade
            
            match testButton, applyButton, testText, applyText with
            | Some tb, Some ab, Some tt, Some at ->
                if isSelectionMade then
                    Logging.logVerbosef "UIComponents: Enabling Apply and Test buttons"
                    tb.IsEnabled <- true
                    tb.Opacity <- 1.0
                    tb.Background <- SolidColorBrush(colors.Secondary)
                    tt.Foreground <- SolidColorBrush(Colors.White)
                    
                    ab.IsEnabled <- true
                    ab.Opacity <- 1.0
                    ab.Background <- SolidColorBrush(colors.Primary)
                    at.Foreground <- SolidColorBrush(Colors.White)
                else
                    Logging.logVerbosef "UIComponents: Disabling Apply and Test buttons"
                    tb.IsEnabled <- false
                    tb.Opacity <- 0.5
                    tb.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Secondary.R, colors.Secondary.G, colors.Secondary.B))
                    tt.Foreground <- SolidColorBrush(colors.Text)
                    
                    ab.IsEnabled <- false
                    ab.Opacity <- 0.5
                    ab.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Primary.R, colors.Primary.G, colors.Primary.B))
                    at.Foreground <- SolidColorBrush(colors.Text)
            | _ -> () // Buttons not yet created
        
        // Left panel: Resolution selection
        let resolutionPanel = Border()
        resolutionPanel.Background <- SolidColorBrush(colors.Surface)
        resolutionPanel.BorderBrush <- SolidColorBrush(colors.Border)
        resolutionPanel.BorderThickness <- Thickness(1.0)
        resolutionPanel.CornerRadius <- CornerRadius(6.0)
        resolutionPanel.Padding <- Thickness(10.0)
        Grid.SetColumn(resolutionPanel, 0)
        
        let resolutionContent = StackPanel()
        resolutionContent.Orientation <- Orientation.Vertical
        
        let resolutionTitle = TextBlock()
        resolutionTitle.Text <- "📐 Resolution"
        resolutionTitle.FontSize <- 14.0
        resolutionTitle.FontWeight <- FontWeight.SemiBold
        resolutionTitle.Foreground <- SolidColorBrush(colors.Text)
        resolutionTitle.Margin <- Thickness(0.0, 0.0, 0.0, 10.0)
        resolutionContent.Children.Add(resolutionTitle)
        
        let resolutionScrollViewer = ScrollViewer()
        resolutionScrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
        resolutionScrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Disabled
        resolutionScrollViewer.MaxHeight <- 300.0
        
        let resolutionList = StackPanel()
        resolutionList.Orientation <- Orientation.Vertical
        resolutionList.Spacing <- 2.0
        
        // Populate resolution list from display capabilities
        match display.Capabilities with
        | Some caps ->
            let sortedResolutions = 
                caps.GroupedResolutions 
                |> Map.toList 
                |> List.sortByDescending (fun ((w, h), _) -> w * h) // Sort by total pixels
            
            for ((width, height), refreshRates) in sortedResolutions do
                // Use Border with TextBlock instead of Button to avoid default hover styling
                let resolutionButton = Border()
                resolutionButton.HorizontalAlignment <- HorizontalAlignment.Stretch
                resolutionButton.Padding <- Thickness(10.0, 8.0)
                resolutionButton.Margin <- Thickness(0.0, 1.0)
                resolutionButton.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Primary.R, colors.Primary.G, colors.Primary.B))
                resolutionButton.BorderBrush <- SolidColorBrush(colors.Border)
                resolutionButton.BorderThickness <- Thickness(1.0)
                resolutionButton.CornerRadius <- CornerRadius(4.0)
                resolutionButton.Cursor <- new Cursor(StandardCursorType.Hand)
                
                // Add TextBlock child for the content
                let resolutionText = TextBlock()
                resolutionText.Text <- sprintf "%d × %d" width height
                resolutionText.HorizontalAlignment <- HorizontalAlignment.Left
                resolutionText.VerticalAlignment <- VerticalAlignment.Center
                resolutionText.Foreground <- SolidColorBrush(colors.Text)
                resolutionButton.Child <- resolutionText
                
                // Add hover effects for better visibility - FIXED VERSION
                let isCurrentResolution = width = caps.CurrentMode.Width && height = caps.CurrentMode.Height
                let normalBg = if isCurrentResolution then SolidColorBrush(colors.Primary) else SolidColorBrush(Color.FromArgb(50uy, colors.Primary.R, colors.Primary.G, colors.Primary.B))
                let normalFg = if isCurrentResolution then SolidColorBrush(colors.Surface) else SolidColorBrush(colors.Text)
                
                resolutionButton.PointerEntered.Add(fun _ ->
                    if not isCurrentResolution then
                        // Theme-aware blue hover colors matching refresh rate colors
                        let hoverBg, hoverFg = if Theme.currentTheme = Theme.Light then 
                                                 SolidColorBrush(Color.FromRgb(59uy, 130uy, 246uy)), SolidColorBrush(Colors.White) // Blue-500 for light theme
                                               else 
                                                 SolidColorBrush(Color.FromRgb(96uy, 165uy, 250uy)), SolidColorBrush(Colors.White) // Blue-400 for dark theme
                        resolutionButton.Background <- hoverBg
                        resolutionText.Foreground <- hoverFg  // Update TextBlock foreground instead of Border
                )
                
                resolutionButton.PointerExited.Add(fun _ ->
                    // Check if this resolution is currently selected - if so, keep selection styling
                    let isSelected = selectedResolution = Some (width, height)
                    if isSelected then
                        // Keep selected styling
                        resolutionButton.Background <- SolidColorBrush(colors.Primary)
                        resolutionText.Foreground <- SolidColorBrush(colors.Surface)
                    else
                        // Return to normal styling
                        resolutionButton.Background <- normalBg
                        resolutionText.Foreground <- normalFg  // Update TextBlock foreground instead of Border
                )
                
                // Only set initial selection to current resolution if none is selected yet
                // Don't do any visual highlighting here - let the click handler control all highlighting
                let isCurrentResolution = width = caps.CurrentMode.Width && height = caps.CurrentMode.Height
                if isCurrentResolution && selectedResolution.IsNone then
                    selectedResolution <- Some (width, height)
                    updateButtonStates()
                    Logging.logVerbosef "UIComponents: Initial resolution selection set to current: %dx%d" width height
                
                // Handle mouse click on the border
                resolutionButton.PointerPressed.Add(fun _ ->
                    Logging.logVerbosef "UIComponents: Resolution selected: %dx%d" width height
                    // Update selection
                    let previousResolution = selectedResolution
                    selectedResolution <- Some (width, height)
                    
                    // Only clear refresh rate if we're selecting a different resolution
                    if previousResolution <> Some (width, height) then
                        selectedRefreshRate <- None
                        Logging.logVerbosef "UIComponents: Different resolution selected - will auto-select refresh rate"
                    else
                        Logging.logVerbosef "UIComponents: Same resolution re-selected - keeping refresh rate: %A" selectedRefreshRate
                    
                    Logging.logVerbosef "UIComponents: selectedResolution set to: %A" selectedResolution
                    
                    // Reset ALL resolution button styles to normal (user has made a selection)
                    for child in resolutionList.Children do
                        if child :? Border then
                            let border = child :?> Border
                            // Reset to normal style for all buttons
                            border.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Primary.R, colors.Primary.G, colors.Primary.B))
                            if border.Child :? TextBlock then
                                let textBlock = border.Child :?> TextBlock
                                textBlock.Foreground <- SolidColorBrush(colors.Text)
                    
                    // Highlight selected resolution
                    resolutionButton.Background <- SolidColorBrush(colors.Primary)
                    resolutionText.Foreground <- SolidColorBrush(colors.Surface) // Use surface color for contrast
                    
                    Logging.logVerbosef "UIComponents: Highlighting selected resolution button: %dx%d" width height
                    updateButtonStates()
                    
                    // Update refresh rate panel
                    Logging.logVerbosef "UIComponents: Updating refresh rate panel for resolution %dx%d" width height
                    Logging.logVerbosef "UIComponents: Refresh rates available for this resolution: %A" refreshRates
                    match refreshRatePanel with
                    | Some panel -> 
                        panel.Children.Clear()
                        
                        let refreshTitle = TextBlock()
                        refreshTitle.Text <- "🔄 Refresh Rate"
                        refreshTitle.FontSize <- 14.0
                        refreshTitle.FontWeight <- FontWeight.SemiBold
                        refreshTitle.Foreground <- SolidColorBrush(colors.Text)
                        refreshTitle.Margin <- Thickness(0.0, 0.0, 0.0, 10.0)
                        panel.Children.Add(refreshTitle)
                        
                        let refreshScrollViewer = ScrollViewer()
                        refreshScrollViewer.VerticalScrollBarVisibility <- ScrollBarVisibility.Auto
                        refreshScrollViewer.HorizontalScrollBarVisibility <- ScrollBarVisibility.Disabled
                        refreshScrollViewer.MaxHeight <- 300.0
                        
                        let refreshList = StackPanel()
                        refreshList.Orientation <- Orientation.Vertical
                        refreshList.Spacing <- 2.0
                        
                        // Sort refresh rates from highest to lowest
                        let sortedRefreshRates = refreshRates |> List.sortByDescending id
                        Logging.logVerbosef "UIComponents: Available refresh rates for %dx%d: %A" width height sortedRefreshRates
                        
                        for refreshRate in sortedRefreshRates do
                            // Use Border with TextBlock instead of Button to avoid default hover styling
                            let refreshButton = Border()
                            refreshButton.HorizontalAlignment <- HorizontalAlignment.Stretch
                            refreshButton.Padding <- Thickness(10.0, 8.0)
                            refreshButton.Margin <- Thickness(0.0, 1.0)
                            refreshButton.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Secondary.R, colors.Secondary.G, colors.Secondary.B))
                            refreshButton.BorderBrush <- SolidColorBrush(colors.Border)
                            refreshButton.BorderThickness <- Thickness(1.0)
                            refreshButton.CornerRadius <- CornerRadius(4.0)
                            refreshButton.Cursor <- new Cursor(StandardCursorType.Hand)
                            
                            // Add TextBlock child for the content
                            let refreshText = TextBlock()
                            refreshText.Text <- sprintf "%d Hz" refreshRate
                            refreshText.HorizontalAlignment <- HorizontalAlignment.Left
                            refreshText.VerticalAlignment <- VerticalAlignment.Center
                            refreshText.Foreground <- SolidColorBrush(colors.Text)
                            refreshButton.Child <- refreshText
                            
                            // Add hover effects for better visibility - SELECTION AWARE VERSION
                            let isCurrentRefreshRate = width = caps.CurrentMode.Width && height = caps.CurrentMode.Height && refreshRate = caps.CurrentMode.RefreshRate
                            
                            refreshButton.PointerEntered.Add(fun _ ->
                                // Check if this refresh rate is currently selected (not just the current system refresh rate)
                                let isSelected = selectedRefreshRate = Some refreshRate
                                if not isSelected then
                                    // Theme-aware blue hover colors
                                    let hoverRefreshBg, hoverRefreshFg = if Theme.currentTheme = Theme.Light then 
                                                                            SolidColorBrush(Color.FromRgb(59uy, 130uy, 246uy)), SolidColorBrush(Colors.White) // Blue-500 for light theme
                                                                          else 
                                                                            SolidColorBrush(Color.FromRgb(96uy, 165uy, 250uy)), SolidColorBrush(Colors.White) // Blue-400 for dark theme
                                    refreshButton.Background <- hoverRefreshBg
                                    refreshText.Foreground <- hoverRefreshFg  // Update TextBlock foreground
                            )
                            
                            refreshButton.PointerExited.Add(fun _ ->
                                // Check if this refresh rate is currently selected when exiting hover
                                let isSelected = selectedRefreshRate = Some refreshRate
                                if isSelected then
                                    // Keep selected styling
                                    refreshButton.Background <- SolidColorBrush(colors.Secondary)
                                    refreshText.Foreground <- SolidColorBrush(colors.Surface)
                                else
                                    // Return to normal styling
                                    refreshButton.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Secondary.R, colors.Secondary.G, colors.Secondary.B))
                                    refreshText.Foreground <- SolidColorBrush(colors.Text)
                            )
                            
                            // Auto-select refresh rate logic - only for the currently selected resolution
                            let shouldAutoSelect = 
                                match selectedResolution with
                                | Some (selWidth, selHeight) when selWidth = width && selHeight = height ->
                                    Logging.logVerbosef "UIComponents: Checking auto-selection for %dx%d, current refresh rate: %d" width height refreshRate
                                    Logging.logVerbosef "UIComponents: Current display mode: %dx%d @ %dHz" caps.CurrentMode.Width caps.CurrentMode.Height caps.CurrentMode.RefreshRate
                                    Logging.logVerbosef "UIComponents: Available rates for this resolution: %A" sortedRefreshRates
                                    
                                    // This is the selected resolution - determine which refresh rate to auto-select
                                    if width = caps.CurrentMode.Width && height = caps.CurrentMode.Height then
                                        // For current resolution, select the current refresh rate
                                        let shouldSelect = refreshRate = caps.CurrentMode.RefreshRate
                                        Logging.logVerbosef "UIComponents: Current resolution - should select %dHz: %b" refreshRate shouldSelect
                                        shouldSelect
                                    else
                                        // For other resolutions, select the highest refresh rate available for THIS resolution
                                        let highestRate = sortedRefreshRates |> List.head
                                        let shouldSelect = refreshRate = highestRate
                                        Logging.logVerbosef "UIComponents: Other resolution - highest available: %dHz, should select %dHz: %b" highestRate refreshRate shouldSelect
                                        shouldSelect
                                | _ -> 
                                    false // Don't auto-select for non-selected resolutions
                            
                            // Highlight and select the appropriate refresh rate
                            if shouldAutoSelect then
                                refreshButton.Background <- SolidColorBrush(colors.Secondary)
                                refreshText.Foreground <- SolidColorBrush(colors.Surface) // Use surface color for contrast
                                selectedRefreshRate <- Some refreshRate
                                Logging.logVerbosef "UIComponents: Auto-selected refresh rate: %dHz for resolution %dx%d" refreshRate width height
                                updateButtonStates()
                            
                            // Handle mouse click on the border
                            refreshButton.PointerPressed.Add(fun _ ->
                                Logging.logVerbosef "UIComponents: Refresh rate selected: %dHz" refreshRate
                                selectedRefreshRate <- Some refreshRate
                                Logging.logVerbosef "UIComponents: selectedRefreshRate set to: %A" selectedRefreshRate
                                
                                // Reset all refresh rate button styles
                                for child in refreshList.Children do
                                    if child :? Border then
                                        let border = child :?> Border
                                        border.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Secondary.R, colors.Secondary.G, colors.Secondary.B))
                                        if border.Child :? TextBlock then
                                            let textBlock = border.Child :?> TextBlock
                                            textBlock.Foreground <- SolidColorBrush(colors.Text)
                                
                                // Highlight selected refresh rate
                                refreshButton.Background <- SolidColorBrush(colors.Secondary)
                                refreshText.Foreground <- SolidColorBrush(colors.Surface) // Use surface color for contrast
                                updateButtonStates()
                                
                                printfn "DEBUG: Selected %dx%d @ %dHz" width height refreshRate
                            )
                            
                            refreshList.Children.Add(refreshButton)
                        
                        refreshScrollViewer.Content <- refreshList
                        panel.Children.Add(refreshScrollViewer)
                    | None -> ()
                )
                
                resolutionList.Children.Add(resolutionButton)
        | None ->
            let noDataText = TextBlock()
            noDataText.Text <- "No resolution data available"
            noDataText.Foreground <- SolidColorBrush(colors.TextSecondary)
            noDataText.HorizontalAlignment <- HorizontalAlignment.Center
            noDataText.Margin <- Thickness(20.0)
            resolutionList.Children.Add(noDataText)
        
        resolutionScrollViewer.Content <- resolutionList
        
        // Apply initial highlighting to the selected resolution (should be current resolution)
        match selectedResolution with
        | Some (selWidth, selHeight) ->
            for child in resolutionList.Children do
                if child :? Border then
                    let border = child :?> Border
                    if border.Child :? TextBlock then
                        let textBlock = border.Child :?> TextBlock
                        let expectedText = sprintf "%d × %d" selWidth selHeight
                        if textBlock.Text = expectedText then
                            border.Background <- SolidColorBrush(colors.Primary)
                            textBlock.Foreground <- SolidColorBrush(colors.Surface)
                            Logging.logVerbosef "UIComponents: Applied initial highlighting to resolution: %dx%d" selWidth selHeight
        | None -> 
            Logging.logVerbosef "UIComponents: No initial resolution to highlight"
        
        resolutionContent.Children.Add(resolutionScrollViewer)
        resolutionPanel.Child <- resolutionContent
        panelsGrid.Children.Add(resolutionPanel)
        
        // Right panel: Refresh rate selection (initially empty)
        let refreshRateMainPanel = Border()
        refreshRateMainPanel.Background <- SolidColorBrush(colors.Surface)
        refreshRateMainPanel.BorderBrush <- SolidColorBrush(colors.Border)
        refreshRateMainPanel.BorderThickness <- Thickness(1.0)
        refreshRateMainPanel.CornerRadius <- CornerRadius(6.0)
        refreshRateMainPanel.Padding <- Thickness(10.0)
        Grid.SetColumn(refreshRateMainPanel, 2)
        
        let refreshRatePanelContent = StackPanel()
        refreshRatePanelContent.Orientation <- Orientation.Vertical
        refreshRatePanel <- Some refreshRatePanelContent
        
        let refreshPlaceholder = TextBlock()
        refreshPlaceholder.Text <- "Select a resolution to view available refresh rates"
        refreshPlaceholder.Foreground <- SolidColorBrush(colors.TextSecondary)
        refreshPlaceholder.HorizontalAlignment <- HorizontalAlignment.Center
        refreshPlaceholder.VerticalAlignment <- VerticalAlignment.Center
        refreshPlaceholder.TextAlignment <- TextAlignment.Center
        refreshPlaceholder.Margin <- Thickness(20.0)
        refreshRatePanelContent.Children.Add(refreshPlaceholder)
        
        refreshRateMainPanel.Child <- refreshRatePanelContent
        panelsGrid.Children.Add(refreshRateMainPanel)
        
        contentScrollViewer.Content <- panelsGrid
        contentGrid.Children.Add(contentScrollViewer)
        
        // Bottom buttons
        let buttonPanel = StackPanel()
        buttonPanel.Orientation <- Orientation.Horizontal
        buttonPanel.HorizontalAlignment <- HorizontalAlignment.Right
        buttonPanel.Margin <- Thickness(0.0, 10.0, 0.0, 0.0)
        Grid.SetRow(buttonPanel, 4)
        
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
        cancelButton.Click.Add(fun _ -> onClose())
        buttonPanel.Children.Add(cancelButton)
        
        let testButtonElement = Border()
        testButtonElement.Padding <- Thickness(20.0, 8.0, 20.0, 8.0)
        testButtonElement.Margin <- Thickness(0.0, 0.0, 10.0, 0.0)
        testButtonElement.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Secondary.R, colors.Secondary.G, colors.Secondary.B))
        testButtonElement.BorderBrush <- SolidColorBrush(colors.Border)
        testButtonElement.BorderThickness <- Thickness(1.0)
        testButtonElement.CornerRadius <- CornerRadius(6.0)
        testButtonElement.Cursor <- new Cursor(StandardCursorType.Hand)
        testButtonElement.IsEnabled <- false // Will enable when selection is made
        testButtonElement.Opacity <- 0.5 // Visual indication of disabled state
        
        let testTextElement = TextBlock()
        testTextElement.Text <- "Test 15s"
        testTextElement.HorizontalAlignment <- HorizontalAlignment.Center
        testTextElement.VerticalAlignment <- VerticalAlignment.Center
        testTextElement.Foreground <- SolidColorBrush(colors.Text)
        testButtonElement.Child <- testTextElement
        
        // Assign to mutable variables for updateButtonStates function
        testButton <- Some testButtonElement
        testText <- Some testTextElement
        
        buttonPanel.Children.Add(testButtonElement)
        
        let applyButtonElement = Border()
        applyButtonElement.Padding <- Thickness(20.0, 8.0, 20.0, 8.0)
        applyButtonElement.Background <- SolidColorBrush(Color.FromArgb(50uy, colors.Primary.R, colors.Primary.G, colors.Primary.B))
        applyButtonElement.BorderBrush <- SolidColorBrush(colors.Border)
        applyButtonElement.BorderThickness <- Thickness(1.0)
        applyButtonElement.CornerRadius <- CornerRadius(6.0)
        applyButtonElement.Cursor <- new Cursor(StandardCursorType.Hand)
        applyButtonElement.IsEnabled <- false // Will enable when selection is made
        applyButtonElement.Opacity <- 0.5 // Visual indication of disabled state
        
        let applyTextElement = TextBlock()
        applyTextElement.Text <- "Apply"
        applyTextElement.HorizontalAlignment <- HorizontalAlignment.Center
        applyTextElement.VerticalAlignment <- VerticalAlignment.Center
        applyTextElement.Foreground <- SolidColorBrush(colors.Text)
        applyButtonElement.Child <- applyTextElement
        
        // Assign to mutable variables for updateButtonStates function
        applyButton <- Some applyButtonElement
        applyText <- Some applyTextElement
        
        buttonPanel.Children.Add(applyButtonElement)
        
        // Add click handlers
        testButtonElement.PointerPressed.Add(fun _ ->
            if testButtonElement.IsEnabled && selectedResolution.IsSome && selectedRefreshRate.IsSome then
                match selectedResolution, selectedRefreshRate with
                | Some (width, height), Some refreshRate ->
                    let mode = { Width = width; Height = height; RefreshRate = refreshRate; BitsPerPixel = 32 }
                    Logging.logVerbosef "UIComponents: Starting 15-second test of %dx%d @ %dHz" width height refreshRate
                    
                    // Disable the test button during testing to prevent multiple tests
                    testButtonElement.IsEnabled <- false
                    testButtonElement.Opacity <- 0.5
                    testTextElement.Text <- "Testing..."
                    testTextElement.Foreground <- SolidColorBrush(colors.TextSecondary)
                    
                    // Start the async test
                    let testComplete (result: Result<string, string>) =
                        // Dispatch UI updates to the UI thread
                        Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                            // Re-enable the test button
                            testButtonElement.IsEnabled <- true
                            testButtonElement.Opacity <- 1.0
                            testTextElement.Text <- "Test 15s"
                            testTextElement.Foreground <- SolidColorBrush(Colors.White)
                            testButtonElement.Background <- SolidColorBrush(colors.Secondary)
                            
                            match result with
                            | Ok msg ->
                                Logging.logVerbosef "UIComponents: Test completed successfully: %s" msg
                            | Error err ->
                                Logging.logVerbosef "UIComponents: Test failed: %s" err
                        )
                    
                    // Execute the test asynchronously
                    Async.Start(WindowsDisplaySystem.testDisplayMode display.Id mode selectedOrientation testComplete)
                | _ -> ()
        )
        
        applyButtonElement.PointerPressed.Add(fun _ ->
            Logging.logVerbosef "UIComponents: Apply button clicked!"
            Logging.logVerbosef "UIComponents: Button enabled: %b" applyButtonElement.IsEnabled
            Logging.logVerbosef "UIComponents: Selected resolution: %A" selectedResolution
            Logging.logVerbosef "UIComponents: Selected refresh rate: %A" selectedRefreshRate
            
            if applyButtonElement.IsEnabled && selectedResolution.IsSome && selectedRefreshRate.IsSome then
                match selectedResolution, selectedRefreshRate with
                | Some (width, height), Some refreshRate ->
                    let mode = { Width = width; Height = height; RefreshRate = refreshRate; BitsPerPixel = 32 }
                    
                    // Create updated display info with new settings
                    let updatedDisplay = { 
                        display with 
                            Orientation = selectedOrientation
                            IsPrimary = selectedIsPrimary
                    }
                    
                    Logging.logVerbosef "UIComponents: ===== APPLYING DISPLAY SETTINGS ====="
                    Logging.logVerbosef "UIComponents: Display ID: %s" display.Id
                    Logging.logVerbosef "UIComponents: Resolution: %dx%d @ %dHz" width height refreshRate
                    Logging.logVerbosef "UIComponents: Orientation: %A" selectedOrientation
                    Logging.logVerbosef "UIComponents: Set as Primary: %b" selectedIsPrimary
                    Logging.logVerbosef "UIComponents: Current Primary: %b" display.IsPrimary
                    Logging.logVerbosef "UIComponents: Mode details: { Width=%d; Height=%d; RefreshRate=%d; BitsPerPixel=%d }" 
                            mode.Width mode.Height mode.RefreshRate mode.BitsPerPixel
                    
                    Logging.logVerbosef "UIComponents: Calling onApply callback..."
                    onApply display.Id mode selectedOrientation selectedIsPrimary
                    Logging.logVerbosef "UIComponents: onApply callback completed"
                    
                    // Update the current mode display to reflect the change
                    currentDisplayMode <- mode
                    updateCurrentModeDisplay()
                    Logging.logVerbosef "UIComponents: Updated current mode display to: %dx%d @ %dHz" mode.Width mode.Height mode.RefreshRate
                    
                    // Don't close the dialog - let user make more changes or close manually
                    Logging.logVerbosef "UIComponents: Keeping dialog open for additional changes"
                | _ -> 
                    Logging.logVerbosef "UIComponents: ERROR: Resolution or refresh rate not properly selected!"
            else
                Logging.logVerbosef "UIComponents: ERROR: Button not enabled or selections missing!"
        )
        
        contentGrid.Children.Add(buttonPanel)
        
        // Return just the content grid
        contentGrid

    // Resolution picker dialog window for selecting display modes
    let createResolutionPickerDialog (display: DisplayInfo) (onApply: DisplayId -> DisplayMode -> DisplayOrientation -> bool -> unit) (onClose: unit -> unit) =
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
        
        // Create content using the shared content creation function
        let contentGrid = createResolutionPickerDialogContent display onApply (fun () ->
            dialogWindow.Close()
            onClose())
        
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
        
        // Add keyboard shortcuts info
        let shortcutsInfo = Border()
        shortcutsInfo.Background <- SolidColorBrush(Color.FromArgb(30uy, colors.Primary.R, colors.Primary.G, colors.Primary.B))
        shortcutsInfo.BorderBrush <- SolidColorBrush(colors.Border)
        shortcutsInfo.BorderThickness <- Thickness(1.0)
        shortcutsInfo.CornerRadius <- CornerRadius(4.0)
        shortcutsInfo.Padding <- Thickness(8.0, 6.0)
        shortcutsInfo.Margin <- Thickness(0.0, 0.0, 0.0, 10.0)
        
        let shortcutsText = TextBlock()
        shortcutsText.Text <- "⌨️ Use Ctrl+Shift+1-9 to quickly switch presets"
        shortcutsText.FontSize <- 11.0
        shortcutsText.Foreground <- SolidColorBrush(colors.Text)
        shortcutsText.TextWrapping <- TextWrapping.Wrap
        shortcutsInfo.Child <- shortcutsText
        mainPanel.Children.Add(shortcutsInfo)
        
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
            for i, preset in presets |> List.mapi (fun i p -> (i, p)) do
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
                let shortcutText = if i < 9 then sprintf "[Ctrl+Shift+%d] %s" (i + 1) preset else preset
                presetButton.Content <- shortcutText
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