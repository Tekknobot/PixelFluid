STRUGGLING SWIMMER WATER BLEND + LANE CHANGE PATCH

Changes:
- Added Assets/Shaders/SwimmerWaterBlend.shader.
- StrugglingSwimmerSpawner now creates a runtime material using that shader.
- Swimmer colours are blended toward the PixelWaterGPU deep/surface palette while preserving original sprite detail.
- Added subtle water shimmer to help the sprite sit inside the simulation visually.
- Swimmer still begins off-screen, fades in, and travels naturally into the lane.
- Added random adjacent wave-layer changes similar to SharkLaneSwimmer.
- InterWaveRenderItem lane sorting switches halfway through each transition.
- Swimmer continues horizontal struggling movement during transitions.
- Rescue detection works from either the current or destination adjacent wave while crossing.

Inspector controls on StrugglingSwimmerDrifter:
- Lane Change Delay Range
- Lane Change Duration
- Lane Change Chance

Shader tuning is in StrugglingSwimmerSpawner.ApplyWaterBlendMaterial().
