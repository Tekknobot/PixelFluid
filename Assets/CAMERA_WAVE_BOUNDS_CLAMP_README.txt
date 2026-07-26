CAMERA WAVE BOUNDS CLAMP PATCH

TinySurferCinematicCamera now calculates the combined world-space tank bounds
of every active PixelWaterGPU simulation.

The camera:
- never shows below the lowest/foreground simulation;
- never reveals beyond the left or right simulation edges;
- never reveals above the highest simulation;
- reclamps after SmoothDamp so no transitional frame leaks an edge;
- uses the current rendered zoom when calculating the clamp;
- automatically limits orthographic zoom if the viewport would be larger than
  the complete wave field;
- remains clamped in Single Player Infinite Mode even when Z cinematic mode is
  switched off.

Inspector options are under Camera Edge Clamp on TinySurferCinematicCamera.
