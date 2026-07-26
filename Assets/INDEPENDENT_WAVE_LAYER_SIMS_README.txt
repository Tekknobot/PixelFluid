INDEPENDENT SHADED WAVE SIMULATION LAYERS

This patch creates multiple complete PixelWaterGPU simulations.

The layers do not share:
- particle buffers
- spatial grids
- neighbour lists
- density or pressure passes
- collision calculations

Therefore particles from different layers never collide.

Consecutive arrangement:
- Layer 0: master simulation
- Layer 1: delayed, behind, slightly darker
- Layer 2: delayed farther, darker
- Layer 3: delayed farthest, darkest

Default setup:
- 4 independent simulations
- 0.18 second delay per layer
- 0.34 world-space backward offset per layer
- 0.055 downward offset per layer
- 0.06 render-depth offset per layer
- 0.12 extra shade per layer
- 0.035 alpha loss per layer
- 0.94 force retention per layer

The old same-buffer cascade and layered-force systems are disabled by default.
