# GUI Components

## Overview

The GUI components provide the main user interface for DisplaySwitch-Pro, featuring a Windows Forms-based application with intuitive controls for display management. The interface is designed for quick access to core functionality while providing detailed display information.

## Main Window Layout

### Window Properties
- **Size**: 500x400 pixels
- **Position**: Center screen on startup
- **Style**: Fixed single border (non-resizable)
- **Icon**: System application icon
- **Title**: "Display Manager"

### Component Hierarchy
```
MainForm
├── lstDisplays (ListBox)
├── btnPCMode (Button)
├── btnTVMode (Button)  
├── btnRefresh (Button)
├── btnSaveConfig (Button)
├── btnLoadConfig (Button)
├── lblHelp (Label)
└── lblStatus (Label)
```

## Core UI Components

### Display Information List
**Component**: `lstDisplays` (ListBox)
- **Location**: (10, 10)
- **Size**: 460x150 pixels
- **Font**: Consolas, 9pt (monospace for alignment)
- **Purpose**: Shows real-time display configuration information

**Content Format**:
```
=== CURRENT DISPLAY CONFIGURATION ===

Display 1: DELL U2415 [ACTIVE]
  Device: \\.\DISPLAY1
  Resolution: 1920x1200 @ 60Hz
  Position: (0, 0)

Display 2: Samsung TV [INACTIVE]
  Device: \\.\DISPLAY2
```

### Mode Switch Buttons

#### PC Mode Button
**Component**: `btnPCMode`
- **Location**: (10, 170)
- **Size**: 90x40 pixels
- **Text**: "PC Mode\n(All Displays)"
- **Color**: Light Blue background
- **Style**: Flat with bold Arial 9pt font
- **Action**: Calls `SetPCMode()` method

#### TV Mode Button
**Component**: `btnTVMode`
- **Location**: (110, 170)
- **Size**: 90x40 pixels
- **Text**: "TV Mode\n(TV Only)"
- **Color**: Light Green background
- **Style**: Flat with bold Arial 9pt font
- **Action**: Calls `SetTVMode()` method

#### Refresh Button
**Component**: `btnRefresh`
- **Location**: (380, 170)
- **Size**: 90x40 pixels
- **Text**: "Refresh"
- **Style**: Flat standard button
- **Action**: Calls `LoadDisplayInfo()` method

### Configuration Management

#### Save Configuration Button
**Component**: `btnSaveConfig`
- **Location**: (10, 220)
- **Size**: 90x30 pixels
- **Text**: "Save Config"
- **Action**: Opens save dialog for configuration export

#### Load Configuration Button
**Component**: `btnLoadConfig`
- **Location**: (110, 220)
- **Size**: 90x30 pixels
- **Text**: "Load Config"
- **Action**: Opens load dialog for configuration import

### Information Display

#### Help Label
**Component**: `lblHelp`
- **Location**: (10, 290)
- **Size**: 460x20 pixels
- **Text**: "Shortcuts: Ctrl+1 (PC Mode), Ctrl+2 (TV Mode), Ctrl+R (Refresh)"
- **Style**: Gray text, center aligned
- **Purpose**: Shows available keyboard shortcuts

#### Status Bar
**Component**: `lblStatus`
- **Location**: (10, 320)
- **Size**: 460x30 pixels
- **Style**: Fixed single border, white background, center aligned
- **Format**: "[HH:mm:ss] Status message"
- **Purpose**: Shows current operation status and timestamps

## Implementation Details

### Form Initialization
Located in `DisplayManagerGUI.cs:89-197`

```csharp
private void InitializeComponent()
{
    // Form settings
    this.Text = "Display Manager";
    this.Size = new Size(500, 400);
    this.StartPosition = FormStartPosition.CenterScreen;
    this.Icon = SystemIcons.Application;
    this.FormBorderStyle = FormBorderStyle.FixedSingle;
    this.MaximizeBox = false;
    
    // Component initialization...
}
```

### Display List Population
Located in `DisplayManagerGUI.cs:257-295`

```csharp
private void LoadDisplayInfo()
{
    try
    {
        lstDisplays.Items.Clear();
        var config = DisplayManager.GetCurrentConfiguration();
        
        lstDisplays.Items.Add("=== CURRENT DISPLAY CONFIGURATION ===");
        // ... format and display information
    }
    catch (Exception ex)
    {
        // Error handling
    }
}
```

### Button Event Handlers
Located in `DisplayManagerGUI.cs:349-351`

```csharp
private void BtnPCMode_Click(object sender, EventArgs e) => SetPCMode();
private void BtnTVMode_Click(object sender, EventArgs e) => SetTVMode();
private void BtnRefresh_Click(object sender, EventArgs e) => LoadDisplayInfo();
```

### Status Updates
Located in `DisplayManagerGUI.cs:419-423`

```csharp
private void UpdateStatus(string message)
{
    lblStatus.Text = $"[{DateTime.Now:HH:mm:ss}] {message}";
    Application.DoEvents();
}
```

## User Interactions

### Mode Switching Flow
1. User clicks PC Mode or TV Mode button
2. Status bar shows "Switching to [Mode]..."
3. Display configuration changes applied
4. 2-second wait for changes to settle
5. Display list refreshes automatically
6. Status bar shows completion message
7. System tray notification appears

### Configuration Management Flow
1. **Save**: User clicks Save Config → File dialog opens → User selects location → Configuration saved as JSON
2. **Load**: User clicks Load Config → File dialog opens → User selects file → Confirmation dialog → Configuration applied

### Error Handling
- **Display errors**: Message boxes with detailed error information
- **Status updates**: Error messages shown in status bar
- **Visual feedback**: Button states and colors indicate current mode

## Styling and Theming

### Color Scheme
- **PC Mode Button**: Light Blue (`Color.LightBlue`)
- **TV Mode Button**: Light Green (`Color.LightGreen`)
- **Background**: Default Windows form background
- **Status Bar**: White background with black text
- **Error Text**: Red for error messages

### Typography
- **Display List**: Consolas 9pt (monospace)
- **Buttons**: Arial 9pt Bold
- **Labels**: Default system font
- **Status**: Default system font with timestamps

### Visual States
- **Active Mode**: Corresponding button highlighted
- **Processing**: Status bar shows progress messages
- **Errors**: Red text in status bar, error dialogs

## Accessibility Features

### Keyboard Navigation
- **Tab Order**: Logical flow through controls
- **Enter Key**: Activates focused button
- **Escape Key**: Minimizes to system tray
- **Mnemonics**: Alt+key shortcuts for buttons

### Screen Reader Support
- **Labels**: Proper labeling for all controls
- **Status Updates**: Status bar accessible to screen readers
- **Button Descriptions**: Clear button text and tooltips

## Customization Options

### Layout Modifications
```csharp
// Change button positions
btnPCMode.Location = new Point(x, y);
btnTVMode.Location = new Point(x, y);

// Modify button colors
btnPCMode.BackColor = Color.FromArgb(100, 150, 255);
btnTVMode.BackColor = Color.FromArgb(100, 255, 150);
```

### Font Customization
```csharp
// Change display list font
lstDisplays.Font = new Font("Courier New", 10);

// Modify button fonts
btnPCMode.Font = new Font("Arial", 10, FontStyle.Bold);
```

### Window Behavior
```csharp
// Make window resizable
this.FormBorderStyle = FormBorderStyle.Sizable;
this.MaximizeBox = true;

// Change startup position
this.StartPosition = FormStartPosition.Manual;
this.Location = new Point(100, 100);
```

## Integration Points

### Related Components
- **[Core Features](core-features.md)**: Button actions trigger display mode changes
- **[System Tray](system-tray.md)**: Window can be minimized to tray
- **[Keyboard Shortcuts](keyboard-shortcuts.md)**: Form handles keyboard input
- **[Configuration Management](config-management.md)**: Save/Load buttons access config system

### Event Flow
1. **User Input** → GUI Components → Core Features → Display API
2. **Status Updates** → Core Features → GUI Components → User Display
3. **Configuration Changes** → Config Management → GUI Components → Display Refresh

## Performance Considerations

### UI Responsiveness
- **Async Operations**: Long-running tasks don't block UI
- **Progress Feedback**: Status bar shows operation progress
- **Minimal Redraws**: Efficient display list updates

### Memory Management
- **Resource Disposal**: Proper cleanup of graphics resources
- **Event Handling**: Prevent memory leaks from event handlers
- **Display List**: Clear and repopulate efficiently

## Future Enhancements

### Planned UI Improvements
- **Dark Mode**: Theme switching capability
- **Custom Layouts**: User-configurable button arrangements
- **Status Icons**: Visual indicators for display status
- **Animation**: Smooth transitions between modes
- **Tooltips**: Enhanced help text for all controls
- **Tabbed Interface**: Organize features into logical groups