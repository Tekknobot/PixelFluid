PROCEDURAL RAIN SYSTEM
======================

Added Assets/Scripts/ProceduralRainSystem.cs.

The system creates itself automatically at runtime. No prefab setup is required.

RAIN SITUATIONS
---------------
Clear
Drizzle
Light Rain
Steady Rain
Heavy Rain
Wind-Driven Rain
Tropical Downpour

PLAY MODE TESTING
-----------------
Press R to cycle through every rain situation.

MAIN INSPECTOR SETTINGS
-----------------------
Starting Situation:
Select the weather active when the scene begins.

Random Weather Changes:
Automatically chooses new situations after a random duration.

Minimum / Maximum Situation Duration:
Controls how long a weather situation remains active.

Transition Seconds:
Smoothly fades rain intensity, wind, splashes, and atmosphere between situations.

Splash Band Camera Offset:
Moves the broad splash layer vertically so it can be aligned with the visible ocean.

The rain follows the active camera, covers the entire camera width, and generates all
streak and splash textures procedurally. It does not require imported particle art.
