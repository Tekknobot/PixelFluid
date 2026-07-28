RESCUED SURFER EXIT PATCH

When a struggling swimmer is rescued:
- swimmer_saved SFX still plays.
- The rescued_surfer 16-frame sprite sheet spawns at the rescue location.
- The avatar inherits the swimmer's current inter-wave lane.
- It randomly rides left or right while following the wave height and slope.
- It continues beyond the water level bounds and destroys itself once safely off-level.
- It is intentionally separate from TinyWaveSurfer, so it does not affect player controls,
  health, sharks, speech bubbles, or cinematic camera cycling.

Main tuning values:
Assets/Scripts/RescuedSurferExit.cs
- horizontalSpeed
- exitDistance
- animationFramesPerSecond
- spriteScale
