RUNTIME-ADJUSTABLE WAVE LAYERS

During Play Mode, expand each generated object in the Hierarchy:

- Water Simulation Layer 1
- Water Simulation Layer 2
- Water Simulation Layer 3

On each PixelWaterGPU component, use the new Runtime Layer Position section.

Runtime Layer Position
- X moves that simulation left or right.
- Y moves that simulation onto the exact horizontal line you want.
- Changes move the actual GPU particles, emitter, tank boundaries, seabed and
  render bounds together while the simulation continues running.

Runtime Layer Render Depth
- Separates the layer visually in Z.
- Change this live without moving the physical simulation.

Runtime Layer Wave Delay
- Changes that individual layer's wave timing live.

The values can be edited while the game is running. Unity will normally revert
Play Mode changes when Play Mode ends, so copy the final values before stopping
if you want to enter them as defaults later.
