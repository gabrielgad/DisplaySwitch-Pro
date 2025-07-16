# Keyboard Shortcuts

## Overview

The Keyboard Shortcuts system provides instant access to DisplaySwitch-Pro functionality through customizable hotkeys. This system includes both application-level shortcuts (when window is focused) and global hotkeys (system-wide access), enabling efficient display management without mouse interaction.

## Application-Level Shortcuts

### Core Shortcuts
**Location**: `DisplayManagerGUI.cs:238-255`

| Shortcut | Function | Description |
|----------|----------|-------------|
| `Ctrl+1` | PC Mode | Switch to PC mode (all displays) |
| `Ctrl+2` | TV Mode | Switch to TV mode (single display) |
| `Ctrl+R` | Refresh | Refresh display information |
| `Escape` | Minimize | Minimize to system tray |

### Implementation Details

#### Keyboard Event Handling
```csharp
private void MainForm_KeyDown(object sender, KeyEventArgs e)
{
    if (e.Control)
    {
        switch (e.KeyCode)
        {
            case Keys.D1:
                SetPCMode();
                break;
            case Keys.D2:
                SetTVMode();
                break;
            case Keys.R:
                LoadDisplayInfo();
                break;
        }
    }
}
```

#### Form Setup for Keyboard Handling
```csharp
// In InitializeComponent()
this.KeyPreview = true;
this.KeyDown += MainForm_KeyDown;
```

**Key Properties**:
- **KeyPreview**: `true` - Form receives key events before child controls
- **KeyDown Event**: Handles key press events for the entire form
- **Modifier Keys**: Uses `e.Control` to detect Ctrl key combinations

## System Tray Menu Shortcuts

### Menu Item Shortcuts
**Location**: `DisplayManagerGUI.cs:203-209`

The system tray context menu displays keyboard shortcuts for quick reference:

```csharp
var pcModeItem = new ToolStripMenuItem("PC Mode (All Displays)", null, (s, e) => SetPCMode());
pcModeItem.ShortcutKeyDisplayString = "Ctrl+1";
trayMenu.Items.Add(pcModeItem);

var tvModeItem = new ToolStripMenuItem("TV Mode (TV Only)", null, (s, e) => SetTVMode());
tvModeItem.ShortcutKeyDisplayString = "Ctrl+2";
trayMenu.Items.Add(tvModeItem);
```

**Purpose**: Visual reminder of available shortcuts in the tray menu

## Global Hotkeys (Advanced)

### Global Hotkey Registration
While not implemented in the base version, global hotkeys can be added:

```csharp
[DllImport("user32.dll")]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

[DllImport("user32.dll")]
private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

// Modifier keys
private const uint MOD_CONTROL = 0x0002;
private const uint MOD_ALT = 0x0001;
private const uint MOD_SHIFT = 0x0004;
private const uint MOD_WIN = 0x0008;

// Virtual key codes
private const uint VK_F1 = 0x70;
private const uint VK_F2 = 0x71;
```

### Global Hotkey Implementation
```csharp
public partial class MainForm : Form
{
    private const int HOTKEY_ID_PC = 1;
    private const int HOTKEY_ID_TV = 2;
    
    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(value);
        
        if (value)
        {
            // Register global hotkeys when form becomes visible
            RegisterHotKey(this.Handle, HOTKEY_ID_PC, MOD_CONTROL | MOD_ALT, VK_F1);
            RegisterHotKey(this.Handle, HOTKEY_ID_TV, MOD_CONTROL | MOD_ALT, VK_F2);
        }
    }
    
    protected override void WndProc(ref Message m)
    {
        const int WM_HOTKEY = 0x0312;
        
        if (m.Msg == WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case HOTKEY_ID_PC:
                    SetPCMode();
                    break;
                case HOTKEY_ID_TV:
                    SetTVMode();
                    break;
            }
        }
        
        base.WndProc(ref m);
    }
    
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Unregister hotkeys when form closes
        UnregisterHotKey(this.Handle, HOTKEY_ID_PC);
        UnregisterHotKey(this.Handle, HOTKEY_ID_TV);
        base.OnFormClosed(e);
    }
}
```

## Help and Documentation

### In-Application Help
**Location**: `DisplayManagerGUI.cs:172-180`

```csharp
var lblHelp = new Label
{
    Text = "Shortcuts: Ctrl+1 (PC Mode), Ctrl+2 (TV Mode), Ctrl+R (Refresh)",
    Location = new Point(10, 290),
    Size = new Size(460, 20),
    ForeColor = Color.Gray,
    TextAlign = ContentAlignment.MiddleCenter
};
this.Controls.Add(lblHelp);
```

**Purpose**: Always-visible reminder of available shortcuts in the main window

### Tooltip Integration
```csharp
private void SetupTooltips()
{
    var tooltip = new ToolTip();
    tooltip.SetToolTip(btnPCMode, "Switch to PC Mode (Ctrl+1)");
    tooltip.SetToolTip(btnTVMode, "Switch to TV Mode (Ctrl+2)");
    tooltip.SetToolTip(btnRefresh, "Refresh Display Information (Ctrl+R)");
}
```

## Accessibility Features

### Screen Reader Support
```csharp
// Add accessibility descriptions
btnPCMode.AccessibleName = "PC Mode Button";
btnPCMode.AccessibleDescription = "Switch to PC mode using all displays. Keyboard shortcut: Control+1";

btnTVMode.AccessibleName = "TV Mode Button";
btnTVMode.AccessibleDescription = "Switch to TV mode using single display. Keyboard shortcut: Control+2";
```

### High Contrast Support
```csharp
private void ApplyHighContrastTheme()
{
    if (SystemInformation.HighContrast)
    {
        // Adjust colors for high contrast mode
        btnPCMode.BackColor = SystemColors.Control;
        btnTVMode.BackColor = SystemColors.Control;
        btnPCMode.ForeColor = SystemColors.ControlText;
        btnTVMode.ForeColor = SystemColors.ControlText;
    }
}
```

## Customizable Shortcuts

### Configuration Storage
```csharp
public class ShortcutConfig
{
    public string PCModeShortcut { get; set; } = "Ctrl+1";
    public string TVModeShortcut { get; set; } = "Ctrl+2";
    public string RefreshShortcut { get; set; } = "Ctrl+R";
    public string MinimizeShortcut { get; set; } = "Escape";
}
```

### Shortcut Parsing
```csharp
private KeyEventArgs ParseShortcut(string shortcut)
{
    var parts = shortcut.Split('+');
    var modifiers = Keys.None;
    var key = Keys.None;
    
    foreach (var part in parts)
    {
        switch (part.ToLower())
        {
            case "ctrl":
                modifiers |= Keys.Control;
                break;
            case "alt":
                modifiers |= Keys.Alt;
                break;
            case "shift":
                modifiers |= Keys.Shift;
                break;
            default:
                if (Enum.TryParse(part, true, out Keys parsedKey))
                    key = parsedKey;
                break;
        }
    }
    
    return new KeyEventArgs(modifiers | key);
}
```

### Dynamic Shortcut Registration
```csharp
private void UpdateShortcuts(ShortcutConfig config)
{
    // Update help text
    lblHelp.Text = $"Shortcuts: {config.PCModeShortcut} (PC Mode), " +
                   $"{config.TVModeShortcut} (TV Mode), " +
                   $"{config.RefreshShortcut} (Refresh)";
    
    // Update tray menu
    pcModeItem.ShortcutKeyDisplayString = config.PCModeShortcut;
    tvModeItem.ShortcutKeyDisplayString = config.TVModeShortcut;
}
```

## Error Handling

### Hotkey Conflicts
```csharp
private bool RegisterHotkeyWithFallback(int id, uint modifiers, uint vk)
{
    if (!RegisterHotKey(this.Handle, id, modifiers, vk))
    {
        // Try alternative shortcut
        var alternatives = GetAlternativeShortcuts(modifiers, vk);
        foreach (var alt in alternatives)
        {
            if (RegisterHotKey(this.Handle, id, alt.modifiers, alt.vk))
            {
                NotifyShortcutChanged(id, alt.modifiers, alt.vk);
                return true;
            }
        }
        return false;
    }
    return true;
}
```

### Key Event Validation
```csharp
private void MainForm_KeyDown(object sender, KeyEventArgs e)
{
    try
    {
        if (e.Control)
        {
            switch (e.KeyCode)
            {
                case Keys.D1:
                    SetPCMode();
                    e.Handled = true;
                    break;
                case Keys.D2:
                    SetTVMode();
                    e.Handled = true;
                    break;
                case Keys.R:
                    LoadDisplayInfo();
                    e.Handled = true;
                    break;
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error processing keyboard shortcut: {ex.Message}",
            "Keyboard Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
```

## Performance Considerations

### Event Handling Efficiency
- **KeyPreview**: Only enabled when necessary
- **Event Handlers**: Lightweight processing
- **Key Filtering**: Only process relevant key combinations

### Memory Usage
- **Static Shortcuts**: Minimal memory overhead
- **Global Hotkeys**: Small system resource usage
- **Event Cleanup**: Proper disposal of event handlers

## Platform Compatibility

### Windows Version Support
| Version | Application Shortcuts | Global Hotkeys | Notes |
|---------|----------------------|----------------|-------|
| Windows 7 | ✅ Full Support | ✅ Full Support | Complete functionality |
| Windows 8/8.1 | ✅ Full Support | ✅ Full Support | Complete functionality |
| Windows 10 | ✅ Full Support | ✅ Full Support | Enhanced with Win+K integration |
| Windows 11 | ✅ Full Support | ✅ Full Support | Modern shortcut styling |

### Keyboard Layout Support
- **QWERTY**: Primary testing layout
- **International**: Number keys work universally
- **Dvorak/Colemak**: Alternative layouts supported
- **Localized**: Works with localized Windows versions

## Testing and Validation

### Shortcut Testing
```csharp
[Test]
public void TestKeyboardShortcuts()
{
    var form = new MainForm();
    
    // Test PC Mode shortcut
    var pcModeKey = new KeyEventArgs(Keys.Control | Keys.D1);
    form.TestKeyDown(pcModeKey);
    Assert.IsTrue(form.IsInPCMode);
    
    // Test TV Mode shortcut
    var tvModeKey = new KeyEventArgs(Keys.Control | Keys.D2);
    form.TestKeyDown(tvModeKey);
    Assert.IsTrue(form.IsInTVMode);
}
```

### Accessibility Testing
```csharp
[Test]
public void TestAccessibilitySupport()
{
    var form = new MainForm();
    
    // Test screen reader support
    Assert.IsNotNull(form.btnPCMode.AccessibleName);
    Assert.IsNotNull(form.btnTVMode.AccessibleName);
    
    // Test high contrast support
    SystemInformation.HighContrast = true;
    form.ApplyHighContrastTheme();
    Assert.AreEqual(SystemColors.Control, form.btnPCMode.BackColor);
}
```

## Integration with Other Systems

### Game Mode Integration
```csharp
private void RegisterGameModeHotkeys()
{
    // Register game-specific shortcuts
    RegisterHotKey(this.Handle, 100, MOD_CONTROL | MOD_SHIFT, VK_F1);
    RegisterHotKey(this.Handle, 101, MOD_CONTROL | MOD_SHIFT, VK_F2);
}

private void HandleGameModeShortcuts(int hotkeyId)
{
    switch (hotkeyId)
    {
        case 100:
            SetGamingDisplayMode();
            break;
        case 101:
            SetStreamingDisplayMode();
            break;
    }
}
```

### Voice Control Integration
```csharp
private void SetupVoiceCommands()
{
    // Integration with Windows Speech Recognition
    var speechRecognizer = new SpeechRecognizer();
    speechRecognizer.LoadGrammar("PC Mode", () => SetPCMode());
    speechRecognizer.LoadGrammar("TV Mode", () => SetTVMode());
    speechRecognizer.LoadGrammar("Refresh Displays", () => LoadDisplayInfo());
}
```

## Future Enhancements

### Planned Features
- **Customizable Shortcuts**: User-defined key combinations
- **Macro Recording**: Record and replay shortcut sequences
- **Gesture Support**: Touch and mouse gesture integration
- **Voice Commands**: Speech recognition integration
- **Multi-Language**: Localized shortcut descriptions
- **Profile-Based**: Different shortcuts for different user profiles

### Advanced Shortcut Patterns
```csharp
// Chord shortcuts (two-key combinations)
private void HandleChordShortcuts(Keys firstKey, Keys secondKey)
{
    var chord = new KeyChord(firstKey, secondKey);
    
    if (chord.Equals(new KeyChord(Keys.D, Keys.P))) // D then P
        SetPCMode();
    else if (chord.Equals(new KeyChord(Keys.D, Keys.T))) // D then T
        SetTVMode();
}

// Sequence shortcuts (multiple key presses)
private void HandleSequenceShortcuts(List<Keys> sequence)
{
    if (sequence.SequenceEqual(new[] { Keys.D, Keys.I, Keys.S, Keys.P })) // "DISP"
        LoadDisplayInfo();
}
```

## Security Considerations

### Hotkey Hijacking Prevention
```csharp
private bool ValidateHotkeySource(Message m)
{
    // Ensure hotkey messages come from legitimate sources
    return m.LParam.ToInt32() == GetCurrentProcessId();
}
```

### Privilege Escalation Protection
```csharp
private void RegisterSecureHotkeys()
{
    // Only register hotkeys with appropriate permissions
    if (HasDisplayConfigurationPermission())
    {
        RegisterHotKey(this.Handle, HOTKEY_ID_PC, MOD_CONTROL, VK_F1);
        RegisterHotKey(this.Handle, HOTKEY_ID_TV, MOD_CONTROL, VK_F2);
    }
}
```

## Integration Points

### Related Components
- **[GUI Components](gui-components.md)**: Keyboard shortcuts complement button interactions
- **[System Tray](system-tray.md)**: Tray menu shows available shortcuts
- **[Core Features](core-features.md)**: Shortcuts trigger core display switching functions
- **[Configuration Management](config-management.md)**: Shortcut preferences saved with configuration

### User Experience Flow
1. **User Input** → Keyboard Shortcut → Core Function → Display Change
2. **System Tray** → Right-Click Menu → Shortcut Display → User Reference
3. **Main Window** → Help Label → Shortcut Information → User Learning
4. **Configuration** → Saved Shortcuts → Application Restart → Restored Preferences