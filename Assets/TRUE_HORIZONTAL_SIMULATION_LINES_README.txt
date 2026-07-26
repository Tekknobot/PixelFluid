TRUE HORIZONTAL SIMULATION OFFSET FIX

Problem fixed
The previous patch moved each layer's GameObject transform, but the GPU water
shader renders particle positions directly in world coordinates. As a result,
the transforms moved while the actual simulations remained visually stacked.

This patch offsets the real simulation coordinates before each layer is
initialised:

- Spawn Origin
- Tank Minimum
- Tank Maximum
- Beach Height / seabed reference
- Particle starting positions
- Compute-shader boundaries
- Wave emitter region
- Render bounds

The GameObject transform is no longer used to position the water.

Result
- Layer 0 is the lowest foreground simulation.
- Layer 1 occupies the next horizontal line.
- Layer 2 occupies the next horizontal line.
- Layer 3 occupies the highest background line.
- Each line has its own complete GPU particle simulation.
- Particles never collide across layers.
- Only the foreground/master simulation interacts with the surfboard.

Preset
Surf Preset > Horizontal Depth Wave Lines
