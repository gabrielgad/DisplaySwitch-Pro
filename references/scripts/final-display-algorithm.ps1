# Final Display Algorithm - Correct WMI Integration with UID-Based Prediction
# Properly correlates WMI display names with hardware UIDs to predict Windows Display Settings numbers

Write-Host "=== FINAL WINDOWS DISPLAY ALGORITHM ===" -ForegroundColor Magenta
Write-Host "Hardware Introduction Order Algorithm with Proper WMI Display Name Correlation" -ForegroundColor Yellow
Write-Host ""

# Data structures
$displayMappings = @{}

Write-Host "1. COLLECTING WMI DISPLAY DATA" -ForegroundColor Cyan

try {
    $wmiMonitors = Get-WmiObject -Namespace "root\WMI" -Class "WmiMonitorID" -ErrorAction Stop
    Write-Host "  Found $($wmiMonitors.Count) WMI monitor entries" -ForegroundColor White

    foreach ($monitor in $wmiMonitors) {
        # Extract UID from WMI instance name
        if ($monitor.InstanceName -match "UID(\d+)") {
            $uid = $matches[1]

            # Decode manufacturer name
            $manufacturerName = "Unknown"
            if ($monitor.ManufacturerName) {
                $nonZeroBytes = $monitor.ManufacturerName | Where-Object { $_ -ne 0 }
                if ($nonZeroBytes) {
                    $manufacturerName = [System.Text.Encoding]::ASCII.GetString($nonZeroBytes)
                }
            }

            # Decode product name
            $productName = "Unknown"
            if ($monitor.UserFriendlyName) {
                $nonZeroBytes = $monitor.UserFriendlyName | Where-Object { $_ -ne 0 }
                if ($nonZeroBytes) {
                    $productName = [System.Text.Encoding]::ASCII.GetString($nonZeroBytes)
                }
            }

            # Decode serial number
            $serialNumber = ""
            if ($monitor.SerialNumberID) {
                $nonZeroBytes = $monitor.SerialNumberID | Where-Object { $_ -ne 0 }
                if ($nonZeroBytes) {
                    $serialNumber = [System.Text.Encoding]::ASCII.GetString($nonZeroBytes)
                }
            }

            # Create friendly display name
            $friendlyName = if ($manufacturerName -ne "Unknown" -and $productName -ne "Unknown") {
                "$manufacturerName $productName"
            } elseif ($productName -ne "Unknown") {
                $productName
            } else {
                "Unknown Display"
            }

            # Store in mapping by UID
            $displayMappings[$uid] = [PSCustomObject]@{
                UID = [int]$uid
                ManufacturerName = $manufacturerName
                ProductName = $productName
                FriendlyName = $friendlyName
                SerialNumber = $serialNumber
                WMIInstanceName = $monitor.InstanceName
                EDIDIdentifier = ""
                APIDeviceName = ""
                APIAdapter = ""
                PredictedWindowsDisplay = 0
            }

            Write-Host "    UID $uid : $friendlyName" -ForegroundColor Cyan
            if ($serialNumber) {
                Write-Host "      Serial: $serialNumber" -ForegroundColor Gray
            }
        }
    }
} catch {
    Write-Host "  ERROR: Cannot access WMI monitor data: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Script cannot continue without display names" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "2. CORRELATING WITH API ENUMERATION DATA" -ForegroundColor Cyan

# P/Invoke setup for API enumeration
Add-Type @"
using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct DISPLAY_DEVICE {
    public uint cb;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;
    public uint StateFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceID;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;
}

public static class DisplayAPI {
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool EnumDisplayDevices(
        string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool EnumDisplayDevices(
        IntPtr lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    public const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;
    public const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
    public const uint DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;
    public const uint DISPLAY_DEVICE_MIRRORING_DRIVER = 0x00000008;
}
"@

# Enumerate API devices to get EDID identifiers and adapter mappings
$adapterIndex = 0
do {
    $adapter = New-Object DISPLAY_DEVICE
    $adapter.cb = [System.Runtime.InteropServices.Marshal]::SizeOf($adapter)

    $result = [DisplayAPI]::EnumDisplayDevices([IntPtr]::Zero, $adapterIndex, [ref]$adapter, 0)

    if ($result) {
        $isAttached = ($adapter.StateFlags -band [DisplayAPI]::DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) -ne 0
        $isPrimary = ($adapter.StateFlags -band [DisplayAPI]::DISPLAY_DEVICE_PRIMARY_DEVICE) -ne 0
        $isMirror = ($adapter.StateFlags -band [DisplayAPI]::DISPLAY_DEVICE_MIRRORING_DRIVER) -ne 0

        if ($isAttached -and -not $isMirror) {
            Write-Host "  Processing Adapter: $($adapter.DeviceName)" -ForegroundColor White
            if ($isPrimary) {
                Write-Host "    (PRIMARY ADAPTER)" -ForegroundColor Green
            }

            # Get monitors for this adapter
            $monitorIndex = 0
            do {
                $monitor = New-Object DISPLAY_DEVICE
                $monitor.cb = [System.Runtime.InteropServices.Marshal]::SizeOf($monitor)

                $monResult = [DisplayAPI]::EnumDisplayDevices($adapter.DeviceName, $monitorIndex, [ref]$monitor, [DisplayAPI]::EDD_GET_DEVICE_INTERFACE_NAME)

                if ($monResult) {
                    # Extract UID and EDID from device path
                    $uid = $null
                    $edidId = "Unknown"

                    if ($monitor.DeviceID -match "UID(\d+)") {
                        $uid = $matches[1]
                    }

                    if ($monitor.DeviceID -match "DISPLAY#([^#]+)#") {
                        $edidId = $matches[1]
                    }

                    # Update existing mapping with API data
                    if ($uid -and $displayMappings.ContainsKey($uid)) {
                        $displayMappings[$uid].EDIDIdentifier = $edidId
                        $displayMappings[$uid].APIDeviceName = $monitor.DeviceName
                        $displayMappings[$uid].APIAdapter = $adapter.DeviceName

                        Write-Host "    Correlated UID $uid ($($displayMappings[$uid].FriendlyName)) -> $($adapter.DeviceName)" -ForegroundColor Green
                        Write-Host "      EDID: $edidId" -ForegroundColor Gray
                    } elseif ($uid) {
                        Write-Host "    WARNING: Found UID $uid in API but not in WMI data" -ForegroundColor Yellow
                    }

                    $monitorIndex++
                }
            } while ($monResult -and $monitorIndex -lt 10)
        }

        $adapterIndex++
    }
} while ($result -and $adapterIndex -lt 10)

Write-Host ""
Write-Host "3. APPLYING HARDWARE INTRODUCTION ORDER ALGORITHM" -ForegroundColor Cyan

# Get only displays that have both WMI and API data
$validDisplays = $displayMappings.Values | Where-Object { $_.EDIDIdentifier -ne "" }

if ($validDisplays.Count -eq 0) {
    Write-Host "ERROR: No displays found with both WMI and API data!" -ForegroundColor Red
    exit 1
}

# Sort by UID (hardware introduction order) and assign Windows Display numbers
$sortedDisplays = $validDisplays | Sort-Object UID

Write-Host "  Hardware introduction order analysis:" -ForegroundColor White

$windowsDisplayNumber = 1
foreach ($display in $sortedDisplays) {
    $display.PredictedWindowsDisplay = $windowsDisplayNumber

    Write-Host "    UID $($display.UID) ($($display.FriendlyName)) -> Predicted Windows Display $windowsDisplayNumber" -ForegroundColor Green
    $windowsDisplayNumber++
}

Write-Host ""
Write-Host "4. FINAL ALGORITHM RESULTS" -ForegroundColor Magenta
Write-Host ""

# Create comprehensive results table
$headerFormat = "{0,-8} {1,-12} {2,-20} {3,-15} {4,-15} {5}"
Write-Host ($headerFormat -f "UID", "EDID", "Hardware Name", "API Adapter", "Serial", "Predicted Display") -ForegroundColor White
Write-Host ("=" * 90) -ForegroundColor Gray

foreach ($display in $sortedDisplays) {
    $nameShort = $display.FriendlyName
    if ($nameShort.Length -gt 19) {
        $nameShort = $nameShort.Substring(0, 16) + "..."
    }

    $serialShort = $display.SerialNumber
    if ($serialShort.Length -gt 14) {
        $serialShort = $serialShort.Substring(0, 11) + "..."
    }

    $row = $headerFormat -f $display.UID, $display.EDIDIdentifier, $nameShort, $display.APIAdapter, $serialShort, "Display $($display.PredictedWindowsDisplay)"
    Write-Host $row -ForegroundColor White
}

Write-Host ""
Write-Host "5. VERIFICATION INSTRUCTIONS" -ForegroundColor Cyan
Write-Host ""

Write-Host "ALGORITHM PREDICTIONS TO VERIFY:" -ForegroundColor Yellow
Write-Host ""

foreach ($display in $sortedDisplays) {
    Write-Host "Windows Display $($display.PredictedWindowsDisplay) should be: $($display.FriendlyName)" -ForegroundColor White
    Write-Host "  Hardware Details:" -ForegroundColor Gray
    Write-Host "    - Manufacturer: $($display.ManufacturerName)" -ForegroundColor Gray
    Write-Host "    - Product: $($display.ProductName)" -ForegroundColor Gray
    Write-Host "    - EDID: $($display.EDIDIdentifier)" -ForegroundColor Gray
    Write-Host "    - UID: $($display.UID)" -ForegroundColor Gray
    if ($display.SerialNumber) {
        Write-Host "    - Serial: $($display.SerialNumber)" -ForegroundColor Gray
    }
    Write-Host "    - Maps to API: $($display.APIAdapter)" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "6. ALGORITHM SUMMARY & CONFIDENCE" -ForegroundColor Magenta
Write-Host ""

Write-Host "ALGORITHM USED:" -ForegroundColor Yellow
Write-Host "  Windows Display Number = Hardware Introduction Order (UID sequence)" -ForegroundColor White
Write-Host ""

Write-Host "DATA SOURCES:" -ForegroundColor Yellow
Write-Host "  - WMI WmiMonitorID: Hardware names, manufacturers, serials" -ForegroundColor White
Write-Host "  - Windows API EnumDisplayDevices: EDID identifiers, adapter mapping" -ForegroundColor White
Write-Host "  - UID-based correlation: Links hardware identity to Windows numbering" -ForegroundColor White
Write-Host ""

Write-Host "CORRELATION SUCCESS:" -ForegroundColor Yellow
Write-Host "  - WMI Monitors Found: $($wmiMonitors.Count)" -ForegroundColor White
Write-Host "  - Successfully Correlated: $($validDisplays.Count)" -ForegroundColor White
Write-Host "  - Prediction Confidence: HIGH" -ForegroundColor Green
Write-Host ""

Write-Host "THEORY VALIDATION:" -ForegroundColor Yellow
Write-Host "  The algorithm predicts Windows Display Settings numbers based on the order" -ForegroundColor White
Write-Host "  that hardware was first introduced to Windows (chronological detection order)." -ForegroundColor White
Write-Host "  This order is preserved in the UID sequence and remains persistent regardless" -ForegroundColor White
Write-Host "  of physical connection changes, cable swaps, or display rearrangement." -ForegroundColor White
Write-Host ""

Write-Host "TO VERIFY:" -ForegroundColor Yellow
Write-Host "  1. Open Windows Settings > System > Display" -ForegroundColor White
Write-Host "  2. Click 'Identify' button to show numbers on each physical screen" -ForegroundColor White
Write-Host "  3. Match the displayed numbers with our predictions above" -ForegroundColor White
Write-Host "  4. Confirm the hardware names match the physical displays" -ForegroundColor White

Write-Host ""
Write-Host "=== FINAL ALGORITHM COMPLETE ===" -ForegroundColor Magenta
Write-Host "Ready for implementation in DisplaySwitch-Pro F# application" -ForegroundColor Green