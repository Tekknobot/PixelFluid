SAFE PLAYER SURFER SPAWN PATCH

The initial player spawn now avoids sharks and giant squids.

TinyWaveSurfer > Random Initial Ocean Spawn:
- Enemy Safe Spawn Radius: minimum desired clearance from predators. Default 3.5.
- Safe Spawn Attempts: number of random wave positions tested. Default 40.

The system waits two extra frames for section enemies to spawn, tests random X positions and wave layers, and accepts the first location outside the safe radius. If every tested location is crowded, it uses the candidate with the greatest predator clearance.
