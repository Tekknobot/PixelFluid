CHARGED SKID AIR TRICK CORRECTION

- Jump can begin while the charged-water-skid push is active.
- Starting the jump immediately ends the skid state.
- The current skid speed is converted once into extra jump distance and takeoff velocity.
- The skid does not continue updating while airborne.
- Airborne trick input randomly selects chuck_handstand or chuck_flip.
- Controller state renamed to chuck_flip so Animator.Play uses the correct state.
- Both trick clips are one-shot and return to chuck_surf_jump while airtime remains.
