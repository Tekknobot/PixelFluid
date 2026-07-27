DEATH BLOOD URP RED PATCH

- Blood now uses Universal Render Pipeline/Particles/Unlit at runtime.
- Explicitly sets both _BaseColor and _Color to a dark blood red.
- Falls back to URP 2D Sprite Unlit or Sprites/Default if needed.
- Blood emits on the exact frame DieFromShark starts chuck_death.
- Existing directional edge-wave wrapping was preserved unchanged:
  * Up/W from wave 0 wraps to the last wave.
  * Down/S from the last wave wraps to wave 0.
  * Interior input moves one adjacent wave only.
  * Inward edge input does not wrap.
