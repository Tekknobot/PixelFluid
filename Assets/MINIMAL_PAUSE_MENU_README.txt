SURFER SLUG MINIMAL PAUSE MENU

No scene setup is required. SurferSlugPauseMenuBootstrap creates the menu automatically.

CONTROLS
- Controller Start: open/resume pause menu
- Keyboard Escape: open/resume pause menu
- Controller/keyboard UI navigation works through Unity's EventSystem

PAUSE BEHAVIOUR
- Time.timeScale is never changed.
- PixelOcean gameplay behaviours are temporarily disabled.
- Water simulation, water rendering, endless wave sections, weather, day/night,
  star field, horizon fog, seabed, scene fade, and procedural audio remain active.
- The waves therefore remain visible and moving behind the menu.

MENU
- Resume
- Controls
- Quit

PROJECT-SPECIFIC FIX
- Controller Start was removed from TinyWaveSurferSpawnListener's "any button"
  list, preventing Start from spawning the player while opening the pause menu.
