ALIEN UFO / ABDUCTION PATCH

Automatically creates one animated UFO after the player appears.

Features:
- Uses all sliced frames from Resources/Alien/alien_ship_idle.
- High-sky roaming with smooth banking and randomized movement.
- Curved low swoops across the screen.
- Periodically tracks the player and starts a tractor-beam attack.
- The player has three continuous seconds to escape horizontally.
- Remaining in the beam for three seconds triggers an abduction death and normal respawn.
- Layered LineRenderer beam edges, core, landing ellipse, pulsing, flicker, ship shake and scale pulses.

Main tuning values are serialized at the top of AlienUfoController.cs:
shipScale, skyHeightViewport, swoopDepth, trackingSpeed, beamHalfWidth,
abductionSeconds, hoverAbovePlayer, beam colours and widths.
