DEATH SWIMMER PLAYER-DEATH RESPONSE PATCH

The Death swimmer uses GodzillaLaneSwimmer, so the response was added there.

Behaviour:
- Shark or UFO death records the player's exact world position.
- Every active Godzilla-based Death swimmer interrupts roaming/combat.
- Death changes to the closest water lane and swims to the death location.
- Death pauses there for 2.5 seconds by default.
- Death then resumes normal roaming.
- Player respawning remains independent.

Inspector settings on GodzillaLaneSwimmer:
- Death Approach Speed
- Death Arrival Distance
- Death Pause Duration

Reusable call:
GodzillaLaneSwimmer.NotifyPlayerDeath(worldPosition);
