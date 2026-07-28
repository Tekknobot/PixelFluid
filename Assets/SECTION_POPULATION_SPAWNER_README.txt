SECTION POPULATION SPAWNER
==========================

SectionPopulationSpawner.cs is installed automatically when the scene loads.
No scene setup is required.

Behaviour:
- Starts as soon as the scene starts.
- Waits only for EndlessWaveSections to finish creating the three sections.
- Disables startup behaviour on all older item/enemy spawner components.
- Randomly chooses one enabled item/enemy type for each section.
- Creates exactly one populated object in each section (three total).
- Avoids duplicate types across the first three sections when enough types are enabled.

Default pool:
- Heart
- Soda can
- Struggling swimmer
- Shark
- Giant squid

The older individual spawner components can remain in the scene; their automatic
startup spawning is disabled by the new controller.
