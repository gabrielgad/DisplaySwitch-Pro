# Command Line Interface

## Overview

The Command Line Interface (CLI) provides console-based access to DisplaySwitch-Pro functionality, enabling automation, scripting, and remote management of display configurations. The CLI mode runs alongside the GUI application and supports headless operation for system administration and integration scenarios.

## Command Structure

### Basic Syntax
```bash
DisplayManager.exe [command] [options]
```

### Available Commands
- **pc**: Switch to PC mode (all displays active)
- **tv**: Switch to TV mode (single external display)
- **No arguments**: Launch GUI application

## Implementation Details

### CLI Entry Point
**Location**: `DisplayManagerGUI.cs:847-872`

```csharp
// Check command line arguments
if (args.Length > 0)
{
    // Run in console mode
    try
    {
        switch (args[0].ToLower())
        {
            case "pc":
                DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.PCMode);
                Console.WriteLine("PC Mode activated");
                break;
            case "tv":
                DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.TVMode);
                Console.WriteLine("TV Mode activated");
                break;
            default:
                Console.WriteLine("Usage: DisplayManager.exe [pc|tv]");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}
else
{
    // Run GUI
    Application.Run(new MainForm());
}
```

### Single Instance Management
**Location**: `DisplayManagerGUI.cs:834-844`

```csharp
// Check if already running
bool createdNew;
using (var mutex = new System.Threading.Mutex(true, "DisplayManagerGUI", out createdNew))
{
    if (!createdNew)
    {
        MessageBox.Show("Display Manager is already running!\n\n" +
                      "Check your system tray (near the clock).", 
                      "Already Running", 
                      MessageBoxButtons.OK, 
                      MessageBoxIcon.Information);
        return;
    }
    
    // Continue with application logic...
}
```

## Command Usage

### PC Mode Command
**Command**: `DisplayManager.exe pc`
**Action**: Activates all connected displays in extended desktop mode
**Output**: "PC Mode activated"

**Example**:
```bash
C:\> DisplayManager.exe pc
PC Mode activated
```

**Behavior**:
- Enables all available displays
- Configures extended desktop topology
- Returns exit code 0 on success
- Returns exit code 1 on failure

### TV Mode Command
**Command**: `DisplayManager.exe tv`
**Action**: Activates only the external display (TV)
**Output**: "TV Mode activated"

**Example**:
```bash
C:\> DisplayManager.exe tv
TV Mode activated
```

**Behavior**:
- Disables internal/laptop display
- Enables only external display
- Configures external display topology
- Returns exit code 0 on success
- Returns exit code 1 on failure

### GUI Mode (Default)
**Command**: `DisplayManager.exe`
**Action**: Launches the graphical user interface
**Output**: GUI window opens

**Example**:
```bash
C:\> DisplayManager.exe
# GUI application starts
```

### Help/Usage Information
**Command**: `DisplayManager.exe help` or invalid arguments
**Action**: Displays usage information
**Output**: "Usage: DisplayManager.exe [pc|tv]"

**Example**:
```bash
C:\> DisplayManager.exe help
Usage: DisplayManager.exe [pc|tv]

C:\> DisplayManager.exe invalid
Usage: DisplayManager.exe [pc|tv]
```

## Console Application Configuration

### Project Configuration for Console Output
To enable console output for Windows Forms applications, the project uses:
```xml
<OutputType>WinExe</OutputType>
```

### Console Attachment
For console output in Windows Forms applications:
```csharp
// Attach to parent console if available
[DllImport("kernel32.dll")]
static extern bool AttachConsole(int dwProcessId);

// In Main method
AttachConsole(-1); // Attach to parent console
```

## Error Handling

### Command Line Error Handling
```csharp
try
{
    DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.PCMode);
    Console.WriteLine("PC Mode activated");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}
```

### Error Scenarios
- **Invalid Command**: Unknown command argument
- **Display Configuration Failure**: Hardware or driver issues
- **Permission Denied**: Insufficient privileges
- **Already Running**: Application instance already active

### Exit Codes
- **0**: Success
- **1**: Error occurred
- **2**: Invalid arguments (potential future use)

## Automation and Scripting

### Batch Script Integration
**File**: `switch_to_pc.bat`
```batch
@echo off
echo Switching to PC mode...
DisplayManager.exe pc
if %errorlevel% equ 0 (
    echo Success: PC mode activated
) else (
    echo Error: Failed to switch to PC mode
)
```

**File**: `switch_to_tv.bat`
```batch
@echo off
echo Switching to TV mode...
DisplayManager.exe tv
if %errorlevel% equ 0 (
    echo Success: TV mode activated
) else (
    echo Error: Failed to switch to TV mode
)
```

### PowerShell Integration
**File**: `DisplayManager.ps1`
```powershell
function Switch-DisplayMode {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateSet("pc", "tv")]
        [string]$Mode
    )
    
    $exePath = "C:\Program Files\DisplayManager\DisplayManager.exe"
    
    if (-not (Test-Path $exePath)) {
        Write-Error "DisplayManager.exe not found at $exePath"
        return
    }
    
    try {
        $result = & $exePath $Mode
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Success: $result" -ForegroundColor Green
        } else {
            Write-Error "Failed to switch to $Mode mode"
        }
    }
    catch {
        Write-Error "Error executing DisplayManager: $_"
    }
}

# Usage examples
Switch-DisplayMode -Mode "pc"
Switch-DisplayMode -Mode "tv"
```

### Task Scheduler Integration
**Task**: Scheduled display mode switching
```xml
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <Triggers>
    <CalendarTrigger>
      <StartBoundary>2024-01-01T20:00:00</StartBoundary>
      <Enabled>true</Enabled>
      <ScheduleByDay>
        <DaysInterval>1</DaysInterval>
      </ScheduleByDay>
    </CalendarTrigger>
  </Triggers>
  <Actions>
    <Exec>
      <Command>C:\Program Files\DisplayManager\DisplayManager.exe</Command>
      <Arguments>tv</Arguments>
    </Exec>
  </Actions>
</Task>
```

## Remote Management

### Remote Execution via SSH
```bash
# Using SSH to remote Windows machine
ssh user@windows-machine "C:\Program Files\DisplayManager\DisplayManager.exe pc"
```

### Network Share Integration
```batch
# Execute from network share
\\server\share\DisplayManager.exe tv
```

### Group Policy Integration
```batch
# Login script via Group Policy
@echo off
if "%USERNAME%"=="admin" (
    DisplayManager.exe pc
) else (
    DisplayManager.exe tv
)
```

## Advanced CLI Features

### Verbose Output Mode
```csharp
// Enhanced CLI with verbose option
private static void ProcessCommandLine(string[] args)
{
    bool verbose = args.Contains("-v") || args.Contains("--verbose");
    string command = args.FirstOrDefault(a => !a.StartsWith("-"));
    
    if (verbose)
    {
        Console.WriteLine($"DisplayManager v1.0 - Command: {command}");
        Console.WriteLine($"Current time: {DateTime.Now}");
    }
    
    switch (command?.ToLower())
    {
        case "pc":
            if (verbose) Console.WriteLine("Switching to PC mode...");
            DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.PCMode);
            Console.WriteLine("PC Mode activated");
            break;
        case "tv":
            if (verbose) Console.WriteLine("Switching to TV mode...");
            DisplayManager.SetDisplayMode(DisplayManager.DisplayMode.TVMode);
            Console.WriteLine("TV Mode activated");
            break;
        default:
            ShowUsage();
            break;
    }
}
```

### Configuration File Support
```csharp
// CLI with configuration file support
private static void ProcessConfigCommand(string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("Usage: DisplayManager.exe config <config_file>");
        return;
    }
    
    string configFile = args[1];
    
    try
    {
        var config = DisplayManager.LoadConfiguration(configFile);
        Console.WriteLine($"Loading configuration: {config.ConfigName}");
        DisplayManager.ApplyConfiguration(config);
        Console.WriteLine("Configuration applied successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading configuration: {ex.Message}");
        Environment.Exit(1);
    }
}
```

### Status Query Command
```csharp
// Add status query functionality
case "status":
    var currentConfig = DisplayManager.GetCurrentConfiguration();
    Console.WriteLine("Current Display Configuration:");
    Console.WriteLine($"Active displays: {currentConfig.Displays.Count(d => d.IsActive)}");
    
    foreach (var display in currentConfig.Displays)
    {
        string status = display.IsActive ? "ACTIVE" : "INACTIVE";
        Console.WriteLine($"  {display.FriendlyName}: {status}");
        if (display.IsActive)
        {
            Console.WriteLine($"    Resolution: {display.Width}x{display.Height}@{display.RefreshRate}Hz");
            Console.WriteLine($"    Position: ({display.PositionX}, {display.PositionY})");
        }
    }
    break;
```

## Integration with System Services

### Windows Service Integration
```csharp
// Service that responds to CLI commands
public class DisplayManagerService : ServiceBase
{
    private NamedPipeServerStream pipeServer;
    
    protected override void OnStart(string[] args)
    {
        pipeServer = new NamedPipeServerStream("DisplayManagerPipe");
        Task.Run(() => ListenForCommands());
    }
    
    private void ListenForCommands()
    {
        while (true)
        {
            pipeServer.WaitForConnection();
            var command = ReadCommand(pipeServer);
            ProcessCommand(command);
            pipeServer.Disconnect();
        }
    }
}
```

### Registry Integration
```csharp
// Register CLI commands in Windows Registry
private static void RegisterURLProtocol()
{
    var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Classes\displaymanager");
    key.SetValue("", "URL:DisplayManager Protocol");
    key.SetValue("URL Protocol", "");
    
    var commandKey = key.CreateSubKey(@"shell\open\command");
    commandKey.SetValue("", $"\"{Application.ExecutablePath}\" \"%1\"");
}
```

## Testing and Validation

### CLI Testing Script
```batch
@echo off
echo Testing DisplayManager CLI...

echo.
echo Test 1: PC Mode
DisplayManager.exe pc
echo Exit code: %errorlevel%

echo.
echo Test 2: TV Mode
DisplayManager.exe tv
echo Exit code: %errorlevel%

echo.
echo Test 3: Invalid Command
DisplayManager.exe invalid
echo Exit code: %errorlevel%

echo.
echo Test 4: Help
DisplayManager.exe help
echo Exit code: %errorlevel%

echo.
echo CLI testing complete.
```

### Unit Tests for CLI
```csharp
[Test]
public void TestCLI_PCMode_Success()
{
    var args = new[] { "pc" };
    var exitCode = ProcessCommandLine(args);
    Assert.AreEqual(0, exitCode);
}

[Test]
public void TestCLI_TVMode_Success()
{
    var args = new[] { "tv" };
    var exitCode = ProcessCommandLine(args);
    Assert.AreEqual(0, exitCode);
}

[Test]
public void TestCLI_InvalidCommand_ShowsUsage()
{
    var args = new[] { "invalid" };
    var output = CaptureConsoleOutput(() => ProcessCommandLine(args));
    StringAssert.Contains("Usage:", output);
}
```

## Security Considerations

### Privilege Requirements
- **Standard User**: CLI commands work with standard user privileges
- **No Elevation**: No administrative rights required
- **Display Permissions**: Uses standard Windows display configuration APIs

### Command Injection Prevention
```csharp
// Validate command arguments
private static bool IsValidCommand(string command)
{
    var validCommands = new[] { "pc", "tv", "status", "help" };
    return validCommands.Contains(command.ToLower());
}

private static void ProcessCommandLine(string[] args)
{
    if (args.Length == 0)
    {
        LaunchGUI();
        return;
    }
    
    string command = args[0].ToLower();
    
    if (!IsValidCommand(command))
    {
        Console.WriteLine("Invalid command. Usage: DisplayManager.exe [pc|tv|status|help]");
        Environment.Exit(1);
        return;
    }
    
    // Process valid command...
}
```

## Performance Considerations

### Startup Performance
- **Fast Initialization**: CLI mode skips GUI initialization
- **Minimal Memory**: Lower memory footprint than GUI mode
- **Quick Exit**: Immediate termination after command execution

### Resource Usage
- **CPU**: Minimal CPU usage during command execution
- **Memory**: ~10-20MB for CLI operations
- **Disk**: No temporary files created

## Future Enhancements

### Planned CLI Features
- **Configuration Management**: CLI-based config save/load
- **Batch Operations**: Multiple commands in single invocation
- **JSON Output**: Machine-readable status output
- **Watch Mode**: Monitor display changes
- **Logging**: Built-in logging for CLI operations
- **Plugin System**: Extensible command system

### Extended Command Set
```bash
# Future command possibilities
DisplayManager.exe status --json
DisplayManager.exe config save "work_setup"
DisplayManager.exe config load "gaming_setup"
DisplayManager.exe watch --interval 5
DisplayManager.exe backup --all
DisplayManager.exe restore --from backup.json
```

## Integration Points

### Related Components
- **[Core Features](core-features.md)**: CLI commands trigger core display switching
- **[Configuration Management](config-management.md)**: CLI access to saved configurations
- **[System Tray](system-tray.md)**: CLI and GUI can coexist
- **[Build System](build-system.md)**: CLI functionality built into main executable

### Workflow Integration
1. **System Startup** → CLI Command → Display Configuration
2. **User Login** → Batch Script → CLI Execution → Mode Switch
3. **Remote Management** → SSH/RDP → CLI Command → Status Report
4. **Automation** → Task Scheduler → CLI Execution → Scheduled Changes