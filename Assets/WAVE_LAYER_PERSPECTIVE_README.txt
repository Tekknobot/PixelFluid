WAVE LAYER PERSPECTIVE — FOREGROUND LOW

The independent water simulations are now arranged like a front-elevation
ocean scene:

Layer 3 — highest, smallest, farthest toward the horizon
Layer 2 — higher and slightly smaller
Layer 1 — just behind the foreground
Layer 0 — lowest, largest, nearest foreground simulation

The simulations remain physically independent:
- no shared particle buffers
- no shared neighbour grids
- no cross-layer collisions

Perspective controls
- Independent Layer Vertical Offset:
  Controls how quickly delayed layers rise toward the horizon.
- Independent Layer Scale Falloff:
  Makes distant layers progressively smaller.
- Independent Layer Rise Curve:
  Controls whether the rise is even or concentrated near the horizon.
- Independent Layer Depth Offset:
  Keeps the render planes consecutively separated.
- Independent Layer Back Offset:
  Adds a small sideways/shoreward stagger.

Recommended preset
Surf Preset > Foreground Low Perspective Wave Layers

Recommended starting values
- Layer Count: 4
- Delay: 0.18
- Back Offset: 0.12
- Vertical Offset: 0.18
- Depth Offset: 0.06
- Scale Falloff: 0.94
- Rise Curve: 1.15
- Shade: 0.12
- Alpha Loss: 0.035
