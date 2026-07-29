AI PLAYER WHEN SINGLE PLAYER MODE IS DISABLED

Patched scripts:
- Assets/Scripts/TinyWaveSurfer.cs
- Assets/Scripts/TinySurferCinematicCamera.cs

Behaviour:
- When PixelWaterGPU > Single Player Mode Enabled is OFF, one AI Player Surfer is spawned automatically.
- The AI uses the same player-control execution path as the human player.
- It moves horizontally, boosts, performs normal trick jumps, changes wave layers, controls aerial rotation, charges/releases water skids, automatically collects ocean items through the existing pickup system, throws stored items at hazards, takes damage, dies, and respawns.
- The camera prioritizes the AI Player Surfer and continues following it through death and respawn.
- When Single Player Mode Enabled is ON, the existing human-player spawning behaviour remains active instead.
