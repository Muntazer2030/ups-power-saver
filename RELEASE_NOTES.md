## UPS Power Saver v1.0.0

First release. One key to cut CPU + GPU power on a UPS, with a live power overlay and stats window.

**Highlights**
- Global shortcut (default `Ctrl+Alt+U`, rebindable) toggles a low-power mode: lower max CPU speed, Turbo Boost off, GPU clock/power limit (NVIDIA), shorter screen timeout.
- Live overlay: *Simple* (total watts) or *Advanced* (CPU/GPU watts, volts, temperature, clock, load, peak).
- Stats window (`Ctrl+Alt+W`) with current/min/max for all sensors.
- Settings window, auto-start with Windows (tray), single `.exe`.

**Before you run it:** install the PawnIO driver once (`winget install --id namazso.PawnIO`) for CPU watts and board voltages, and accept the administrator prompt.

Tested on Windows 11, Intel i5-14400F + NVIDIA RTX 5060. GPU limits need an NVIDIA GPU; use at your own risk.

Download `UPS-Power-Saver-v1.0.0.zip` below, unzip to a permanent folder, run `UPS Power Saver.exe`.
