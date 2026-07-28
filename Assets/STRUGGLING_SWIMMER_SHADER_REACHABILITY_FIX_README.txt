STRUGGLING SWIMMER SHADER + REACHABILITY FIX

- Added Assets/Resources/Materials/SwimmerWaterBlend.mat.
- The spawner now loads this material directly through Resources.Load, which retains the shader in builds.
- Shader.Find remains only as an editor fallback and logs a clear error if both paths fail.
- Swimmer horizontal bounds now intersect the shared water lane with the camera-visible/player-reachable area.
- Added cameraEdgeInset so the swimmer turns before reaching inaccessible screen edges.
- Removed random horizontal reversals; the swimmer now crosses the scene and only turns at reachable boundaries.
- Random speed, struggle motion, and wave-layer changes remain active.
