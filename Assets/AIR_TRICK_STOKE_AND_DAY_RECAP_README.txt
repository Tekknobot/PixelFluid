SURFER SLUG — AIR TRICK STOKE + DAY RECAP

Added:
- Stoke currency earned only from completed forward-air jumps that include a trick.
- Supported tricks: Handstand, Rotation, Flip.
- Height-based bonus scoring.
- Multi-trick jump combo bonus when more than one trick is completed before landing.
- Floating trick name and +STOKE indicator above the surfer on landing.
- Persistent STOKE total in the upper-right HUD.
- End-of-Day 1 recap before the Night Passes transition.
- End-of-Day 2 recap after the run completes.

Default scoring:
- Handstand: 100
- Rotation: 140
- Flip: 180
- Height: 120 points per world unit
- Additional trick in the same jump: +75

Main script:
Assets/Scripts/AirTrickScoreSystem.cs

Scoring integration:
Assets/Scripts/TinyWaveSurfer.cs

Day recap integration:
Assets/Scripts/SurfDayProgressionDirector.cs

No scene setup is required. The score system installs itself at runtime.
