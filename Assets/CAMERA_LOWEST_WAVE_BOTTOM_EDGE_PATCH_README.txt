CAMERA LOWEST-WAVE BOTTOM EDGE PATCH

The cinematic camera now calculates the combined bounds of every active
PixelWaterGPU simulation. Its lower viewport edge is clamped exactly to the
TankMinimum.y value of the lowest simulation, with no lower padding.

The final SmoothDamp camera position is clamped again after smoothing so a
brief overshoot can never reveal the star field underneath the ocean.
