BACKGROUND DAY/NIGHT TINT PATCH

ProceduralStarryNight now automatically discovers scenery SpriteRenderers whose object or parent names contain:
background, backdrop, island, horizon, scenery, landscape, shore, or coast.

Their original colors and alpha are preserved, then multiplied by cycle-aware dawn/day/sunset/night tints.
The list refreshes once per second, so replacement or newly spawned background objects are included automatically.

The current tropical_island_0 object is detected through the "island" token without requiring scene setup.
Tint colors and discovery interval are exposed in the ProceduralStarryNight inspector.
