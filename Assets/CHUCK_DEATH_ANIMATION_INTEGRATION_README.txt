CHUCK DEATH ANIMATION INTEGRATION

Patched files:
- Assets/Scripts/TinyWaveSurfer.cs
- Assets/Resources/Animations/chuck.controller
- Assets/Animations/chuck_death.anim

Behavior:
- Shark death now immediately plays the chuck_death Animator state.
- Idle and move updates cannot override the death animation.
- chuck_death is non-looping and holds its final frame until the existing respawn/deactivation logic completes.
- Respawning returns the surfer to Idle through the existing code.

The existing knock-up, spin, fade, and respawn behavior remains enabled.
