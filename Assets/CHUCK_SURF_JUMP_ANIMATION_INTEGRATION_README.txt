CHUCK SURF JUMP ANIMATION INTEGRATION

- Added chuck_surf_jump to the runtime Resources/Animations/chuck controller.
- Forward obstacle surf jumps now play chuck_surf_jump immediately at takeoff.
- Move/idle/prone animation updates cannot override it while airborne.
- The jump clip plays once and holds its landing frame until the motion finishes.
- On landing, TinyWaveSurfer restores chuck_move or Idle based on movement.
- Ordinary turn tricks and wave-switch jumps remain unchanged.
