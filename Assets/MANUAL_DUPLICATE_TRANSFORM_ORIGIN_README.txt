MANUAL DUPLICATION NOW USES THE GAMEOBJECT TRANSFORM

Fixed:
- The missing TropicalSeabed.Configure arguments compile error.
- Every PixelWaterGPU instance now receives its own runtime material.
- Duplicated simulators no longer overwrite each other's particle buffer.
- The GameObject Transform is now the true world-space origin.

Workflow:
1. Duplicate the complete water simulator GameObject.
2. Move the duplicate with its Transform.
3. X moves the whole simulation horizontally.
4. Y moves the water, emitter, tank, horizontal sand and collision floor.
5. Z controls the visual draw depth.

Transform changes also work live during Play Mode.

Each duplicate owns:
- separate GPU particle buffers
- a separate runtime rendering material
- separate sand geometry
- separate compute simulation
- separate bounds and collision floor

Automatic simulation-layer generation remains disabled. Only manually duplicated
simulators are used.
