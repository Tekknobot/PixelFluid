PATCH SUMMARY

TinyWaveSurfer no longer calls ShiftCompleteSimulation from player input and no
longer rebases wave rows. Instead, localRideX is moved directly and clamped by
ClampPlayerXToSandbox(), which intersects:

1. the current wave TankMinimum/TankMaximum with edge padding, and
2. the current camera viewport left/right edges with Player Camera Edge Padding.

This keeps single-player movement inside one fixed play area and prevents the
surfer from leaving the visible screen when the camera reaches the sandbox edge.
