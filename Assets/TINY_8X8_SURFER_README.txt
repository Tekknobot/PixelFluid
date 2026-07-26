TINY 8x8 AUTONOMOUS SURFER

The patch adds one automatically-created surfer called:

    Tiny 8x8 Wave Surfer

No prefab setup is required. Enter Play Mode and the surfer will:

- discover every active PixelWaterGPU simulator
- ride the particle surface of the current simulation
- follow the wave slope
- perform spins, flips and small aerial tricks
- move to the next manually duplicated simulation
- cycle continuously through all available waves

The actual sprite texture is exactly 8 by 8 pixels and uses point filtering.

Inspector controls on TinyWaveSurfer:
- Seconds Per Simulation
- Switch Duration
- Ride Position Across Wave
- Horizontal Ride Speed
- Surface Offset
- Minimum / Maximum Trick Interval
- Jump Height
- Trick Duration
- Spin Degrees
- Flip Chance
- Pixel World Size

Hierarchy order:
The simulations are sorted by Y, then Z, so the surfer cycles from the lowest
wave line toward the higher/back wave lines.

Context menu commands:
- Refresh Wave Simulations
- Ride Next Wave
