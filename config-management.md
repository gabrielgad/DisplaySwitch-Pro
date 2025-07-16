# Configuration Management

## Overview

The Configuration Management system provides persistent storage and retrieval of display configurations, allowing users to save custom display setups and restore them later. This system uses JSON serialization for human-readable configuration files and handles both automatic and manual configuration management.

## Core Components

### Configuration Data Classes

#### DisplayConfig Class
**Location**: `DisplayManagerGUI.cs:645-650`
```csharp
public class DisplayConfig
{
    public List<DisplayInfo> Displays { get; set; } = new List<DisplayInfo>();
    public string ConfigName { get; set; }
    public DateTime SavedAt { get; set; }
}
```

**Purpose**: Container for complete display configuration including metadata

#### DisplayInfo Class
**Location**: `DisplayManagerGUI.cs:652-664`
```csharp
public class DisplayInfo
{
    public string DeviceName { get; set; }
    public string FriendlyName { get; set; }
    public bool IsActive { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public uint RefreshRate { get; set; }
    public uint TargetId { get; set; }
    public uint SourceId { get; set; }
}
```

**Purpose**: Individual display properties and settings

### Configuration Directory
**Location**: `DisplayManagerGUI.cs:74-83`
```csharp
configPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "DisplayManager"
);
Directory.CreateDirectory(configPath);
```

**Default Path**: `%APPDATA%\DisplayManager\`
**Purpose**: Centralized location for all configuration files

## Save Configuration

### Save Process Flow
**Location**: `DisplayManagerGUI.cs:353-379`

1. **User Initiated**: User clicks "Save Config" button
2. **File Dialog**: SaveFileDialog opens with default naming
3. **Current Configuration**: System captures current display state
4. **Metadata Addition**: Adds configuration name and timestamp
5. **Serialization**: Converts to JSON format
6. **File Writing**: Saves to selected location
7. **User Feedback**: Shows success/error message

### Save Implementation
```csharp
private void BtnSaveConfig_Click(object sender, EventArgs e)
{
    using (var dialog = new SaveFileDialog())
    {
        dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
        dialog.InitialDirectory = configPath;
        dialog.FileName = $"DisplayConfig_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var config = DisplayManager.GetCurrentConfiguration();
                config.ConfigName = Path.GetFileNameWithoutExtension(dialog.FileName);
                DisplayManager.SaveConfiguration(config, dialog.FileName);
                UpdateStatus($"Configuration saved: {config.ConfigName}");
                MessageBox.Show($"Configuration saved successfully:\n{config.ConfigName}", 
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
```

### Save Dialog Properties
- **Filter**: JSON files (*.json) and All files (*.*)
- **Initial Directory**: Application configuration directory
- **Default Filename**: `DisplayConfig_YYYYMMDD_HHMMSS.json`
- **File Format**: JSON with indented formatting

### JSON Serialization
**Location**: `DisplayManagerGUI.cs:760-768`
```csharp
public static void SaveConfiguration(DisplayConfig config, string filename)
{
    var options = new JsonSerializerOptions 
    { 
        WriteIndented = true 
    };
    var json = JsonSerializer.Serialize(config, options);
    File.WriteAllText(filename, json);
}
```

## Load Configuration

### Load Process Flow
**Location**: `DisplayManagerGUI.cs:381-417`

1. **User Initiated**: User clicks "Load Config" button
2. **File Dialog**: OpenFileDialog opens in config directory
3. **File Selection**: User selects configuration file
4. **Deserialization**: JSON file parsed into DisplayConfig object
5. **User Confirmation**: Confirmation dialog for applying changes
6. **Configuration Application**: Display settings applied if confirmed
7. **System Update**: Display information refreshed
8. **User Feedback**: Status update and notifications

### Load Implementation
```csharp
private void BtnLoadConfig_Click(object sender, EventArgs e)
{
    using (var dialog = new OpenFileDialog())
    {
        dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
        dialog.InitialDirectory = configPath;
        
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var config = DisplayManager.LoadConfiguration(dialog.FileName);
                
                var result = MessageBox.Show(
                    $"Load configuration '{config.ConfigName}'?\n\n" +
                    $"This will change your display settings.",
                    "Confirm Load", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    
                if (result == DialogResult.Yes)
                {
                    UpdateStatus($"Loading configuration: {config.ConfigName}...");
                    Application.DoEvents();
                    
                    DisplayManager.ApplyConfiguration(config);
                    System.Threading.Thread.Sleep(2000);
                    LoadDisplayInfo();
                    UpdateStatus($"Configuration loaded: {config.ConfigName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading configuration: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
```

### JSON Deserialization
**Location**: `DisplayManagerGUI.cs:770-777`
```csharp
public static DisplayConfig LoadConfiguration(string filename)
{
    if (!File.Exists(filename))
        throw new FileNotFoundException($"Configuration file {filename} not found");

    var json = File.ReadAllText(filename);
    return JsonSerializer.Deserialize<DisplayConfig>(json);
}
```

## Configuration Application

### Apply Configuration Logic
**Location**: `DisplayManagerGUI.cs:797-814`
```csharp
public static void ApplyConfiguration(DisplayConfig config)
{
    // Simplified implementation - uses topology switching
    bool hasTV = config.Displays.Any(d => 
        d.FriendlyName.ToLower().Contains("tv") || 
        d.FriendlyName.ToLower().Contains("hdmi") ||
        d.FriendlyName.ToLower().Contains("samsung") ||
        d.FriendlyName.ToLower().Contains("lg"));
        
    if (hasTV && config.Displays.Count(d => d.IsActive) == 1)
    {
        SetDisplayMode(DisplayMode.TVMode);
    }
    else
    {
        SetDisplayMode(DisplayMode.PCMode);
    }
}
```

### TV Detection Logic
The system uses heuristic detection to identify TV displays:
- **Brand Keywords**: "tv", "hdmi", "samsung", "lg"
- **Active Count**: Single active display suggests TV mode
- **Fallback**: Multi-display configurations default to PC mode

## File Format Structure

### Example Configuration File
```json
{
  "Displays": [
    {
      "DeviceName": "\\\\.\\DISPLAY1",
      "FriendlyName": "DELL U2415",
      "IsActive": true,
      "PositionX": 0,
      "PositionY": 0,
      "Width": 1920,
      "Height": 1200,
      "RefreshRate": 60,
      "TargetId": 1,
      "SourceId": 1
    },
    {
      "DeviceName": "\\\\.\\DISPLAY2",
      "FriendlyName": "Samsung TV",
      "IsActive": false,
      "PositionX": 1920,
      "PositionY": 0,
      "Width": 1920,
      "Height": 1080,
      "RefreshRate": 60,
      "TargetId": 2,
      "SourceId": 2
    }
  ],
  "ConfigName": "Work_Setup",
  "SavedAt": "2024-01-15T10:30:00"
}
```

### File Naming Convention
- **Default Format**: `DisplayConfig_YYYYMMDD_HHMMSS.json`
- **Example**: `DisplayConfig_20240115_103000.json`
- **User Override**: Users can specify custom names during save

## Configuration Validation

### File Existence Check
```csharp
if (!File.Exists(filename))
    throw new FileNotFoundException($"Configuration file {filename} not found");
```

### JSON Validation
- **Format Validation**: JSON deserialization handles format errors
- **Schema Validation**: DisplayConfig class structure enforces schema
- **Error Handling**: Exceptions thrown for invalid configurations

### Display Validation
- **Device Availability**: Checks if saved displays are still connected
- **Resolution Support**: Validates if saved resolutions are supported
- **Fallback Handling**: Graceful degradation for missing displays

## Error Handling

### Save Errors
**Common Scenarios**:
- **File Access Denied**: Insufficient permissions
- **Disk Space**: Not enough storage space
- **Path Invalid**: Invalid file path or name
- **Serialization Failure**: Object serialization issues

**Error Response**:
```csharp
catch (Exception ex)
{
    MessageBox.Show($"Error saving configuration: {ex.Message}", "Error",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
}
```

### Load Errors
**Common Scenarios**:
- **File Not Found**: Configuration file doesn't exist
- **Invalid JSON**: Corrupted or malformed JSON
- **Version Mismatch**: Incompatible configuration format
- **Missing Displays**: Saved displays no longer available

**Error Response**:
```csharp
catch (Exception ex)
{
    MessageBox.Show($"Error loading configuration: {ex.Message}", "Error",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
}
```

## Advanced Features

### Backup Management
```csharp
// Create backup before applying new configuration
private void CreateBackup()
{
    var currentConfig = DisplayManager.GetCurrentConfiguration();
    currentConfig.ConfigName = $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
    var backupPath = Path.Combine(configPath, "Backups");
    Directory.CreateDirectory(backupPath);
    var backupFile = Path.Combine(backupPath, $"{currentConfig.ConfigName}.json");
    DisplayManager.SaveConfiguration(currentConfig, backupFile);
}
```

### Auto-Save Current Configuration
```csharp
// Automatically save current configuration on startup
private void AutoSaveCurrentConfig()
{
    try
    {
        var config = DisplayManager.GetCurrentConfiguration();
        config.ConfigName = "Current_Startup";
        var autoSaveFile = Path.Combine(configPath, "AutoSave", "startup.json");
        Directory.CreateDirectory(Path.GetDirectoryName(autoSaveFile));
        DisplayManager.SaveConfiguration(config, autoSaveFile);
    }
    catch (Exception ex)
    {
        // Log error but don't interrupt startup
        LogError($"Auto-save failed: {ex.Message}");
    }
}
```

### Configuration Comparison
```csharp
public static bool ConfigurationsEqual(DisplayConfig config1, DisplayConfig config2)
{
    if (config1.Displays.Count != config2.Displays.Count)
        return false;
        
    for (int i = 0; i < config1.Displays.Count; i++)
    {
        var d1 = config1.Displays[i];
        var d2 = config2.Displays[i];
        
        if (d1.DeviceName != d2.DeviceName ||
            d1.IsActive != d2.IsActive ||
            d1.Width != d2.Width ||
            d1.Height != d2.Height ||
            d1.PositionX != d2.PositionX ||
            d1.PositionY != d2.PositionY)
            return false;
    }
    
    return true;
}
```

## Performance Considerations

### File I/O Optimization
- **Async Operations**: Use async file operations for large configurations
- **Memory Management**: Dispose of file streams properly
- **Path Validation**: Validate paths before file operations

### JSON Performance
- **Serialization Options**: Use optimized JsonSerializerOptions
- **Memory Usage**: Stream large configurations instead of loading entirely
- **Caching**: Cache frequently accessed configurations

## Security Considerations

### File Permissions
- **User Directory**: Configurations stored in user's AppData folder
- **Access Control**: Respects Windows file system permissions
- **No Elevation**: No administrative privileges required

### Data Protection
- **No Encryption**: Configuration files are plain text JSON
- **Local Storage**: All data stored locally, no network transmission
- **Privacy**: No personal data collection or storage

## Migration and Compatibility

### Version Compatibility
```csharp
public class DisplayConfigV2 : DisplayConfig
{
    public string Version { get; set; } = "2.0";
    public Dictionary<string, object> ExtendedProperties { get; set; } = new();
}

// Migration logic
private DisplayConfig MigrateConfiguration(string json)
{
    // Detect version and migrate if necessary
    var tempConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
    
    if (!tempConfig.ContainsKey("Version"))
    {
        // Migrate from v1.0 to v2.0
        return MigrateFromV1(json);
    }
    
    return JsonSerializer.Deserialize<DisplayConfig>(json);
}
```

### Settings Migration
```csharp
// Migrate old configuration files to new format
private void MigrateOldConfigurations()
{
    var oldConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "DisplayManager"
    );
    
    if (Directory.Exists(oldConfigPath))
    {
        foreach (var file in Directory.GetFiles(oldConfigPath, "*.json"))
        {
            try
            {
                var config = LoadConfiguration(file);
                var newPath = Path.Combine(configPath, Path.GetFileName(file));
                SaveConfiguration(config, newPath);
            }
            catch (Exception ex)
            {
                LogError($"Migration failed for {file}: {ex.Message}");
            }
        }
    }
}
```

## Usage Examples

### Programmatic Configuration Management
```csharp
// Save current configuration programmatically
var config = DisplayManager.GetCurrentConfiguration();
config.ConfigName = "Gaming_Setup";
var savePath = Path.Combine(configPath, "gaming_setup.json");
DisplayManager.SaveConfiguration(config, savePath);

// Load configuration programmatically
var loadedConfig = DisplayManager.LoadConfiguration(savePath);
DisplayManager.ApplyConfiguration(loadedConfig);

// List all saved configurations
var configFiles = Directory.GetFiles(configPath, "*.json");
foreach (var file in configFiles)
{
    var config = DisplayManager.LoadConfiguration(file);
    Console.WriteLine($"Configuration: {config.ConfigName}, Saved: {config.SavedAt}");
}
```

### Batch Configuration Operations
```csharp
// Export all configurations to a backup directory
private void ExportAllConfigurations(string backupDir)
{
    Directory.CreateDirectory(backupDir);
    
    foreach (var file in Directory.GetFiles(configPath, "*.json"))
    {
        var destFile = Path.Combine(backupDir, Path.GetFileName(file));
        File.Copy(file, destFile, true);
    }
}

// Import configurations from backup
private void ImportConfigurations(string backupDir)
{
    foreach (var file in Directory.GetFiles(backupDir, "*.json"))
    {
        var destFile = Path.Combine(configPath, Path.GetFileName(file));
        File.Copy(file, destFile, true);
    }
}
```

## Integration Points

### Related Components
- **[Core Features](core-features.md)**: Configuration data captures core display settings
- **[Display API](display-api.md)**: Raw display data converted to configuration format
- **[GUI Components](gui-components.md)**: Save/Load buttons trigger configuration operations
- **[System Tray](system-tray.md)**: Configuration changes trigger tray notifications

### Data Flow
1. **Current State** → Display API → Configuration Object → JSON File
2. **JSON File** → Configuration Object → Display API → Applied State
3. **User Interface** → Configuration Management → File System → User Feedback

## Future Enhancements

### Planned Features
- **Cloud Sync**: Synchronize configurations across devices
- **Configuration Profiles**: Named profiles with quick switching
- **Scheduled Configurations**: Time-based automatic configuration changes
- **Template System**: Pre-defined configuration templates
- **Import/Export**: Bulk configuration management
- **Configuration Validation**: Enhanced validation and error recovery
- **Version Control**: Track configuration changes over time
- **Compression**: Compress large configuration files
- **Encryption**: Optional encryption for sensitive configurations