SURF DAY: JELLYFISH, OCEAN ITEMS, GODZILLA PATCH

- SurfDayProgressionDirector now explicitly spawns:
  - one jellyfish school during Dawn
  - two schools during Strange Tide
  - three schools during the Storm
  - twelve distributed collectible OceanItems at the beginning of the run
- OceanItemSpawner now supports SpawnProgressionItems(count), avoiding all item sprites flooding the scene at once.
- Its old spawn-on-start behaviour defers to SurfDayProgressionDirector when the story run is active.
- Godzilla now detects from farther away, pursues and changes lanes faster, attacks with less wind-up and cooldown, and lunges faster.
- Godzilla periodically repairs stale/missing water-layer references after endless-section recycling, reducing edge sticking and broken movement.
