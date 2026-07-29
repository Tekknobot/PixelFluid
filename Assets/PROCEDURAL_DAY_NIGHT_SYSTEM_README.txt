PROCEDURAL DAY / NIGHT SYSTEM
=============================

The existing ProceduralStarryNight component now generates a complete cycling sky.
No replacement prefab or texture is required.

MAIN INSPECTOR CONTROLS
-----------------------
Run Cycle
    Enables or pauses automatic time progression.

Full Day Length Minutes
    Real-time duration of one complete day and night. Default: 15 minutes.

Starting Time Of Day
    0.00 = midnight
    0.25 = sunrise
    0.50 = noon
    0.75 = sunset

Editor Fast Forward Multiplier
    Increase this while testing. A value of 10 makes the cycle run ten times faster.

WHAT IS PROCEDURAL
------------------
- Night stars and twinkling
- Star fading at dawn and reappearing at dusk
- Dawn, daytime, sunset and night gradients
- Sun movement
- Crescent moon movement
- Slowly drifting pixel clouds
- Camera background colour synchronization

RUNTIME API
-----------
Find the component and call:

    sky.SetTimeOfDay(0.50f); // noon

Useful properties:

    sky.TimeOfDay
    sky.IsDay
    sky.IsNight

The component still follows the gameplay camera horizontally and retains the old
ProceduralStarryNight class name, so the existing StarryNight scene object continues
to work without scene rewiring.
