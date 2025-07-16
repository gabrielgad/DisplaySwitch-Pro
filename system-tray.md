# System Tray Integration

## Overview

The System Tray Integration provides seamless background operation for DisplaySwitch-Pro, allowing users to access display switching functionality from the Windows system tray without keeping the main window open. This feature ensures the application is always available while maintaining a minimal desktop footprint.

## Core Components

### NotifyIcon Component
**Variable**: `trayIcon` (NotifyIcon)
**Purpose**: Manages the system tray icon and interaction

**Properties**:
- **Icon**: `SystemIcons.Application` (default Windows application icon)
- **Text**: "Display Manager - Double-click to open" (tooltip text)
- **Visible**: `true` (icon is visible in system tray)
- **ContextMenuStrip**: `trayMenu` (right-click context menu)

### Context Menu
**Variable**: `trayMenu` (ContextMenuStrip)
**Purpose**: Provides right-click menu functionality

**Menu Structure**:
```
Display Manager
├── PC Mode (All Displays)     [Ctrl+1]
├── TV Mode (TV Only)          [Ctrl+2]
├── ─────────────────────────
├── Show Window
├── ─────────────────────────
└── Exit
```

## Implementation Details

### Tray Icon Setup
**Location**: `DisplayManagerGUI.cs:199-236`

```csharp
private void SetupTrayIcon()
{
    trayMenu = new ContextMenuStrip();
    
    // PC Mode menu item
    var pcModeItem = new ToolStripMenuItem("PC Mode (All Displays)", null, (s, e) => SetPCMode());
    pcModeItem.ShortcutKeyDisplayString = "Ctrl+1";
    trayMenu.Items.Add(pcModeItem);
    
    // TV Mode menu item
    var tvModeItem = new ToolStripMenuItem("TV Mode (TV Only)", null, (s, e) => SetTVMode());
    tvModeItem.ShortcutKeyDisplayString = "Ctrl+2";
    trayMenu.Items.Add(tvModeItem);
    
    // Separator
    trayMenu.Items.Add(new ToolStripSeparator());
    
    // Show Window menu item
    trayMenu.Items.Add("Show Window", null, (s, e) => {
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.BringToFront();
    });
    
    // Exit menu item
    trayMenu.Items.Add(new ToolStripSeparator());
    trayMenu.Items.Add("Exit", null, (s, e) => {
        trayIcon.Visible = false;
        Application.Exit();
    });

    // Create tray icon
    trayIcon = new NotifyIcon
    {
        Icon = SystemIcons.Application,
        Text = "Display Manager - Double-click to open",
        ContextMenuStrip = trayMenu,
        Visible = true
    };
    
    // Double-click handler
    trayIcon.DoubleClick += (s, e) => {
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.BringToFront();
    };
}
```

### Window Minimize to Tray
**Location**: `DisplayManagerGUI.cs:425-434`

```csharp
protected override void OnFormClosing(FormClosingEventArgs e)
{
    if (e.CloseReason == CloseReason.UserClosing)
    {
        e.Cancel = true;
        this.Hide();
        trayIcon.ShowBalloonTip(2000, "Display Manager", 
            "Minimized to system tray. Double-click the icon to restore.", ToolTipIcon.Info);
    }
}
```

### Balloon Notifications
**Notification Triggers**:
- **Mode Changes**: When PC/TV mode is activated
- **Minimize to Tray**: When main window is closed
- **Configuration Changes**: When settings are saved/loaded

**Implementation Examples**:
```csharp
// PC Mode activation notification
trayIcon.ShowBalloonTip(2000, "Display Manager", 
    "PC Mode activated - All displays enabled", ToolTipIcon.Info);

// TV Mode activation notification
trayIcon.ShowBalloonTip(2000, "Display Manager", 
    "TV Mode activated - TV only", ToolTipIcon.Info);

// Minimize notification
trayIcon.ShowBalloonTip(2000, "Display Manager", 
    "Minimized to system tray. Double-click the icon to restore.", ToolTipIcon.Info);
```

## User Interactions

### Double-Click Behavior
**Action**: Restore main window
**Implementation**:
```csharp
trayIcon.DoubleClick += (s, e) => {
    this.Show();
    this.WindowState = FormWindowState.Normal;
    this.BringToFront();
};
```

**Behavior Details**:
- Shows hidden window
- Restores window to normal state (if minimized)
- Brings window to front of all other windows
- Activates window for user input

### Right-Click Context Menu

#### PC Mode (All Displays)
**Shortcut Display**: "Ctrl+1"
**Action**: Switches to PC mode (all displays active)
**Implementation**: Direct call to `SetPCMode()` method

#### TV Mode (TV Only)
**Shortcut Display**: "Ctrl+2"
**Action**: Switches to TV mode (single external display)
**Implementation**: Direct call to `SetTVMode()` method

#### Show Window
**Action**: Restores main application window
**Implementation**: Same as double-click behavior

#### Exit
**Action**: Completely closes the application
**Implementation**: 
```csharp
trayIcon.Visible = false;
Application.Exit();
```

## Notification System

### Balloon Tip Properties
- **Duration**: 2000ms (2 seconds)
- **Title**: "Display Manager"
- **Icon**: ToolTipIcon.Info (information icon)
- **Auto-hide**: Automatically disappears after timeout

### Notification Scenarios

#### Mode Switch Notifications
```csharp
// PC Mode
trayIcon.ShowBalloonTip(2000, "Display Manager", 
    "PC Mode activated - All displays enabled", ToolTipIcon.Info);

// TV Mode
trayIcon.ShowBalloonTip(2000, "Display Manager", 
    "TV Mode activated - TV only", ToolTipIcon.Info);
```

#### System Notifications
```csharp
// Application minimized
trayIcon.ShowBalloonTip(2000, "Display Manager", 
    "Minimized to system tray. Double-click the icon to restore.", ToolTipIcon.Info);

// Configuration saved
trayIcon.ShowBalloonTip(2000, "Display Manager", 
    "Configuration saved successfully", ToolTipIcon.Info);
```

## Window Management

### Hide to Tray Logic
**Trigger**: User clicks window close button (X)
**Behavior**: 
- Cancels actual close operation
- Hides window instead of closing
- Shows balloon notification
- Keeps application running in background

### Restore from Tray
**Triggers**:
- Double-click tray icon
- Right-click → Show Window
- Keyboard shortcut activation

**Restoration Process**:
1. Make window visible (`this.Show()`)
2. Restore window state (`WindowState = FormWindowState.Normal`)
3. Bring to foreground (`this.BringToFront()`)

## Resource Management

### Cleanup Implementation
**Location**: `DisplayManagerGUI.cs:436-443`

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        trayIcon?.Dispose();
    }
    base.Dispose(disposing);
}
```

**Cleanup Responsibilities**:
- Dispose of NotifyIcon resources
- Remove tray icon from system tray
- Clean up context menu resources
- Prevent memory leaks

### Application Exit Handling
```csharp
// Proper exit sequence
trayIcon.Visible = false;  // Remove from system tray
Application.Exit();        // Close application
```

## Advanced Features

### Custom Icons
```csharp
// Use custom icon file
trayIcon.Icon = new Icon("custom-icon.ico");

// Use embedded resource
trayIcon.Icon = Properties.Resources.CustomIcon;

// Dynamic icon based on mode
if (currentMode == DisplayMode.PCMode)
    trayIcon.Icon = Properties.Resources.PCModeIcon;
else
    trayIcon.Icon = Properties.Resources.TVModeIcon;
```

### Enhanced Tooltips
```csharp
// Dynamic tooltip based on current state
private void UpdateTrayTooltip()
{
    var config = DisplayManager.GetCurrentConfiguration();
    int activeCount = config.Displays.Count(d => d.IsActive);
    string mode = activeCount == 1 ? "TV Mode" : "PC Mode";
    
    trayIcon.Text = $"Display Manager - {mode} ({activeCount} displays)";
}
```

### Menu Item States
```csharp
// Enable/disable menu items based on state
private void UpdateMenuStates()
{
    var config = DisplayManager.GetCurrentConfiguration();
    bool hasMultipleDisplays = config.Displays.Count > 1;
    
    pcModeItem.Enabled = hasMultipleDisplays;
    tvModeItem.Enabled = hasMultipleDisplays;
}
```

## Error Handling

### Tray Icon Creation Failures
```csharp
try
{
    trayIcon = new NotifyIcon
    {
        Icon = SystemIcons.Application,
        Text = "Display Manager",
        Visible = true
    };
}
catch (Exception ex)
{
    MessageBox.Show($"Failed to create system tray icon: {ex.Message}", 
        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    // Fallback: continue without tray icon
}
```

### Context Menu Failures
```csharp
try
{
    SetPCMode();
}
catch (Exception ex)
{
    trayIcon.ShowBalloonTip(3000, "Display Manager", 
        $"Failed to switch to PC mode: {ex.Message}", ToolTipIcon.Error);
}
```

## Performance Considerations

### Resource Usage
- **Memory**: Minimal (~1-2MB for tray icon and menu)
- **CPU**: Negligible when idle
- **System Impact**: Single icon in system tray

### Optimization Tips
- **Lazy Loading**: Create menu items only when needed
- **Event Cleanup**: Properly dispose of event handlers
- **Icon Caching**: Reuse icon resources

## Platform Compatibility

### Windows Versions
- **Windows 7**: Full support
- **Windows 8/8.1**: Full support
- **Windows 10**: Full support with modern tray behavior
- **Windows 11**: Full support with updated tray styling

### System Tray Behavior
- **Hidden Icons**: May be hidden in system tray overflow
- **Notification Settings**: Respects Windows notification settings
- **DPI Awareness**: Scales appropriately with display DPI

## Security Considerations

### Permissions
- **No Special Permissions**: Standard user privileges sufficient
- **Network Access**: Not required for tray functionality
- **File System**: Read access for icon files only

### Privacy
- **No Data Collection**: Tray icon doesn't transmit data
- **Local Operation**: All functionality is local to machine
- **User Control**: User controls all tray interactions

## Customization Examples

### Custom Context Menu
```csharp
// Add additional menu items
trayMenu.Items.Add("Configuration", null, (s, e) => OpenConfigDialog());
trayMenu.Items.Add("About", null, (s, e) => ShowAboutDialog());

// Add submenu
var profilesMenu = new ToolStripMenuItem("Profiles");
profilesMenu.DropDownItems.Add("Gaming", null, (s, e) => LoadGamingProfile());
profilesMenu.DropDownItems.Add("Work", null, (s, e) => LoadWorkProfile());
trayMenu.Items.Add(profilesMenu);
```

### Animated Tray Icon
```csharp
private Timer animationTimer;
private int animationFrame = 0;
private Icon[] animationIcons;

private void StartIconAnimation()
{
    animationTimer = new Timer();
    animationTimer.Interval = 500; // 500ms per frame
    animationTimer.Tick += (s, e) => {
        trayIcon.Icon = animationIcons[animationFrame];
        animationFrame = (animationFrame + 1) % animationIcons.Length;
    };
    animationTimer.Start();
}
```

## Integration Points

### Related Components
- **[GUI Components](gui-components.md)**: Main window management and interaction
- **[Core Features](core-features.md)**: Display mode switching functionality
- **[Keyboard Shortcuts](keyboard-shortcuts.md)**: Hotkey integration with tray menu
- **[Configuration Management](config-management.md)**: Settings for tray behavior

### Event Flow
1. **User Interaction** → System Tray → Core Features → Display Changes
2. **Mode Changes** → Core Features → System Tray → Notification Display
3. **Window Management** → System Tray → GUI Components → Window State

## Future Enhancements

### Planned Features
- **Quick Settings**: Expandable tray menu with common settings
- **Display Profiles**: Quick access to saved display configurations
- **Status Indicators**: Visual indication of current display mode
- **Hotkey Customization**: User-configurable keyboard shortcuts
- **Auto-hide Options**: Configurable tray icon visibility
- **Theme Support**: Dark/light mode tray icon variants