# Third-party notices

The released `UPS Power Saver.exe` embeds unmodified binaries from these projects (LibreHardwareMonitor and PresentMon). This repository does not
contain them; `source/Get-Libs.ps1` downloads them from the upstream release (SHA-256 pinned).

## LibreHardwareMonitor (and its bundled libraries)

- Project: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor (v0.9.6)
- License: Mozilla Public License 2.0 - https://www.mozilla.org/MPL/2.0/
- The source code for the embedded, unmodified `LibreHardwareMonitorLib.dll` is available at the project link above.
- Also embedded (from the same release): HidSharp, BlackSharp.Core, DiskInfoToolkit, RAMSPDToolkit-NDD,
  Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.HashCode and the System.* support libraries. Each is covered by its own
  license (MIT / Apache-2.0 / similar); see the LibreHardwareMonitor release for details.

## PresentMon

- Project: https://github.com/GameTechDev/PresentMon (v2.6.0), console build `PresentMon-2.6.0-x64.exe`, unmodified.
- License: MIT, Copyright (c) Intel Corporation - https://github.com/GameTechDev/PresentMon/blob/main/LICENSE.txt
- Embedded in the released exe and used to read frame-present events for the FPS counter.

## PawnIO (not bundled)

LibreHardwareMonitor reads CPU power and board voltages through the PawnIO driver (https://pawnio.eu/,
winget id `namazso.PawnIO`). It is installed separately by the user and is not part of this repository or release.
