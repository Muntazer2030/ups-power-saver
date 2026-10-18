## UPS Power Saver v1.1.1

**New**
- The overlay now shows whether saver mode is on or off: green **SAVER ON** or amber **SAVER OFF**. It updates the moment you toggle.

**Since v1.1.0**
- FPS counter for the foreground game (frame rate and frame time), using the bundled PresentMon tool. It never shows browsers, chat apps, editors and similar programs, and the app name is not shown on the overlay. The ignore list is editable in `settings.ini`.
- `Ctrl+Alt+O` shows or hides the overlay at any time, even with saver mode OFF, and there is an "Always show overlay" setting.
- The overlay re-asserts its always-on-top position every 0.25 s. Games in true exclusive fullscreen still hide all overlays; use Borderless or Windowed fullscreen.

**Before you run it:** install the PawnIO driver once (`winget install --id namazso.PawnIO`) for CPU watts and board voltages, and accept the administrator prompt.

Default shortcuts: `Ctrl+Alt+U` toggle saver mode, `Ctrl+Alt+O` show/hide overlay, `Ctrl+Alt+W` open window.

Tested on Windows 11 with an Intel i5-14400F and NVIDIA RTX 5060. GPU limits need an NVIDIA GPU; use at your own risk. The exe is unsigned, so Windows SmartScreen may warn on first run.

Download `UPS-Power-Saver-v1.1.1.zip` below, unzip to a permanent folder, and run `UPS Power Saver.exe`.
