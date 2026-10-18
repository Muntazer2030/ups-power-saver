## UPS Power Saver v1.1.0

**New**
- **FPS counter** (frame rate and frame time of the foreground game) in the overlay and window, using the bundled PresentMon tool. Nothing is injected into games, so it is safe with anti-cheat.
- **Show the overlay any time** with `Ctrl+Alt+O` (even when saver mode is OFF) to check current usage, plus an "Always show overlay" setting.
- The overlay re-asserts its always-on-top position every 0.25 s, so games that grab the top-most slot don't hide it. Games in true exclusive fullscreen still hide all overlays; use Borderless or Windowed fullscreen.
- Settings: the overlay shortcut is rebindable, and FPS can be switched off.
- Shortcuts that are already used by another program are reported in the window.
- "Exit app" button; closing the window with X keeps the app in the tray.

**Fixes**
- Settings written with trailing comments were read wrongly.

**Before you run it:** install the PawnIO driver once (`winget install --id namazso.PawnIO`) for CPU watts and board voltages, and accept the administrator prompt.

Default shortcuts: `Ctrl+Alt+U` toggle saver mode, `Ctrl+Alt+O` show/hide overlay, `Ctrl+Alt+W` open window.

Tested on Windows 11 with an Intel i5-14400F and NVIDIA RTX 5060. GPU limits need an NVIDIA GPU; use at your own risk. The exe is unsigned, so Windows SmartScreen may warn on first run.

Download `UPS-Power-Saver-v1.1.0.zip` below, unzip to a permanent folder, and run `UPS Power Saver.exe`.
