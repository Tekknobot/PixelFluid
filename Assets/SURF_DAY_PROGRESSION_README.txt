SURFER SLUG - SURF DAY PROGRESSION

Added Assets/Scripts/SurfDayProgressionDirector.cs.

The director installs automatically and converts the sandbox into a timed run:
- Dawn Patrol: hearts, cans and a first shark.
- Distress Call: introduces swimmer rescue.
- Dangerous Water: more rescues, sharks and giant squid.
- Strange Tide: whale, boombox surfer and UFO encounter.
- Storm Front: heavy rain and another squid.
- The Last Wave: unique Godzilla survival finale.
- Day Complete: the storm clears and the run ends.

A small objective display and chapter banners are drawn automatically.
No scene object or inspector setup is required.

Default full run time: 12 minutes.
All timing and rescue requirements can be changed on SurfDayProgressionDirector.

Also changed:
- SectionPopulationSpawner defers to progression mode.
- AlienUfoSpawner defers its automatic spawn to progression mode.
- BoomboxSurferSpawner defers its automatic spawn to progression mode.
- StrugglingSwimmerDrifter emits a rescue event for objective tracking.
