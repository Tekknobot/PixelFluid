PROCEDURAL HORIZON FOG PATCH

Added:
  Assets/Scripts/ProceduralHorizonFog.cs

The fog is created automatically after the scene loads. No prefab or scene setup is required.

What it does:
- Creates an opaque ocean-coloured mask below the horizon so mountains, islands,
  clouds, and other background layers cannot show through the water.
- Generates a pixelated, softly broken mist edge procedurally.
- Follows the gameplay camera horizontally and resizes to cover the full view.
- Reads ProceduralStarryNight.TimeOfDay and changes colour through day and night.
- Renders at sorting order 2500, above background scenery and below the water layers.

Main Inspector controls on the generated "Procedural Horizon Fog" object:
- Horizon Y: move the fog edge up/down.
- Sorting Order: use a value below the first water layer but above background art.
- Soft Band Fraction: height of the mist transition.
- Mist Strength: opacity of the soft top edge.
- Night/Day colours: match the ocean palette.

Recommended first adjustment:
Set Horizon Y so the mist begins just behind the farthest/top wave row.
