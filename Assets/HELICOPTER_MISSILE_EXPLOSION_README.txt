HELICOPTER MISSILE EXPLOSION PATCH

- Added ExplosionBasicEffect.cs as a reusable one-shot sprite animation.
- Uses Assets/Resources/VFX/explosion_basic.png at 64x64 per frame.
- Plays at 22 FPS and destroys itself after the final frame.
- Spawns when a helicopter missile hits the surfer.
- Also spawns when a thrown item intercepts the missile.
- Explosion has no collider and causes no additional damage.
