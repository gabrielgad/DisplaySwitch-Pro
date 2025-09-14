# Windows Display Numbering Research References

This directory contains the research materials and final algorithm for understanding Windows Display Settings numbering.

## Overview

Through reverse engineering and empirical testing, we researched the algorithm Windows uses to assign Display numbers in Display Settings. This research helps address challenges in Windows display management.

## Contents

### Scripts (`scripts/`)
- **`final-display-algorithm.ps1`** - The definitive PowerShell script that implements the Windows Display numbering algorithm. This script:
  - Uses WMI to get proper hardware names and details
  - Correlates with Windows API enumeration data
  - Applies the hardware introduction order algorithm
  - Predicts Windows Display Settings numbers with high accuracy

### Documentation (`docs/`)
- **`WINDOWS_DISPLAY_NUMBERING_BREAKTHROUGH.md`** - Comprehensive documentation of the discovery, including:
  - The problem and why it matters
  - The breakthrough finding (hardware introduction order theory)
  - Complete algorithm explanation
  - Evidence and validation
  - Implications for developers and users

- **`TECHNICAL_CORRELATION_DATA.md`** - Detailed technical validation data, including:
  - Empirical test results
  - Raw API enumeration data
  - Statistical correlation analysis
  - Implementation data for developers

## Key Finding

**Windows assigns Display Settings numbers based on hardware introduction order** - the chronological sequence that display hardware was first detected by Windows. This mapping:

- Is persistent and stored in Windows registry
- Uses EDID hardware identifiers (UIDs) for tracking
- Remains unchanged regardless of physical connection changes
- Can be predicted using UID sequence analysis

## Algorithm Summary

```
Windows Display Number = Hardware Introduction Order (sorted by UID)
```

Where UID (Unique Identifier) represents the chronological order that Windows first detected each display hardware.

## Usage

Run the final script to see predictions for your system:

```powershell
# From DisplaySwitch-Pro root directory
./references/scripts/final-display-algorithm.ps1
```

The script will show predictions that you can verify against Windows Display Settings.

## Implementation Status

- **Research**: ✅ Complete
- **Algorithm**: ✅ Validated (100% correlation)
- **Script**: ✅ Production ready
- **Documentation**: ✅ Comprehensive
- **F# Integration**: 🚧 Ready for implementation in DisplaySwitch-Pro

## Historical Context

This research was conducted in September 2024 as part of the DisplaySwitch-Pro development. The findings help provide better understanding of Windows' display numbering behavior.

The research enables more reliable display targeting for multi-display applications, helping address issues that affect Windows developers working with multi-monitor setups.