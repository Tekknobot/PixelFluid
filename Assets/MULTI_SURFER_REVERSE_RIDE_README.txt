MULTIPLE SURFERS + EDGE TURN TRICKS + CLAMPED CINEMATIC CAMERA

This patch creates six different 8x8 surfers automatically.

Each surfer:
- starts on a wave simulation
- rides toward one edge
- performs an aerial spin/flip turn at the edge
- reverses direction
- rides the wave back toward the opposite edge
- repeats the back-and-forth ride
- eventually cycles to another simulation

The surfers have different:
- shirt colours
- board colours
- starting directions
- speeds
- sorting orders
- starting wave assignments

CINEMATIC CAMERA
Press Z to toggle the cinematic camera.

The camera now clamps its viewport inside the current surfer's PixelWaterGPU
tank bounds. It should no longer reveal empty space beyond the left, right,
top, or bottom edges of the active simulation.

Inspector controls:
- Clamp To Current Simulation
- Clamp Inset
- Orthographic Zoom
- Framing Offset
