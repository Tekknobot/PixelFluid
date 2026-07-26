CASCADE ECHO WAVE — COMPUTE KERNEL FIX

Fixed the invalid compute kernels.

Cause
The cascade code referenced identifiers that do not exist in the project's
PixelWaterGPU.compute shader:
- emitterCentre01
- breakPoint01
- _SimulationSize
- _SimulationBoundsMin
- _SimulationBoundsMax

Because the compute shader failed to compile, Unity reported kernels 0, 1,
and 2 as invalid.

Fix
- Rebuilt the echo calculations using the shader's existing variables:
  _TankMin, _TankMax, tankWidth, distanceFromEmitter and _BreakPoint.
- Removed the dynamic loop break from the unrolled loop.
- Preserved delayed master-wave copies, spatial trailing, vertical layering,
  volume support and echo curl.

After replacing the files, allow Unity to reimport PixelWaterGPU.compute.
