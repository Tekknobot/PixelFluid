REAL WAVE LAYER INTERLEAVING FIX

This patch changes inter-wave items to render between the actual independent
PixelWaterGPU simulation layers, not horizontal slices of one simulation.

Runtime order with 8 wave simulations:
- Background Water Layer 7
- Items between Layers 6 and 7
- Water Layer 6
- ...
- Items between Layers 0 and 1
- Foreground Water Layer 0

Items sample both neighbouring simulations and follow the midpoint of their
surface heights, velocities and slopes. This keeps each item visually and
physically inside its assigned wave gap.

The RandomInterWaveItemSpawner waits two frames for all runtime water clones to
exist, then discovers and sorts them by IndependentLayerIndex.
