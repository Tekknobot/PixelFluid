SURFER SLUG - SQUARE PREFAB PAUSE MENU

This version removes all rounded corners and rounded UI shaders.

STYLE
- hard 90-degree panel corners
- thin cyan rectangular outlines
- flat rectangular buttons
- transparent screen dim so waves remain visible
- no gradients, circles, rounded masks, or mobile-style cards

BEHAVIOUR
- Start / Escape opens and closes the pause menu
- gameplay scripts pause
- water, waves, weather, lighting, particles, and audio simulation continue
- Time.timeScale is not changed

PREFAB WORKFLOW
1. Enter Play Mode.
2. Press Start or Escape once so the menu is visible.
3. Choose: Surfer Slug > UI > Save Live Pause Menu As Prefab
4. Unity saves: Assets/Prefabs/SurferSlugPauseMenu.prefab
5. Stop Play Mode and drag that prefab into the scene.
6. Edit the visual hierarchy in Prefab Mode from then onward.

The automatic bootstrap detects an existing SurferSlugPauseMenu and will not create a duplicate.
