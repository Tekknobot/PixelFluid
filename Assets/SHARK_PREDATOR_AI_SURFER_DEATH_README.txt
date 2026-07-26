SHARK PREDATOR AI + SURFER DEATH RESPONSE

This patch replaces the shark's purely random attack behaviour with player-aware lane AI.

Shark behaviour:
- Patrols inside the visible camera and water bounds.
- Detects the nearest TinyWaveSurfer within Detection Range.
- Prefers and stalks the player-controlled surfer.
- Reads the surfer's CurrentWaveIndex and crosses adjacent inter-wave lanes until aligned.
- Faces and pursues the surfer horizontally.
- Starts the attack animation only when in the same lane and within Attack Range.
- Applies the hit during the second half of the attack animation when within Hit Range.
- Searches briefly after losing or hitting the target, then resumes patrol.

Surfer death response:
- Player input and normal surfing stop immediately.
- The surfer is knocked away, spins, sinks and fades.
- The hit collider is disabled during the response.
- By default the surfer respawns on the current wave after the death response and short delay.

Runtime tuning:
Select "Shark - Inter-Wave Swimmer" and edit SharkLaneSwimmer:
- Detection Range
- Lose Target Range
- Attack Range
- Hit Range
- Stalk Speed Multiplier
- Attack Recovery
- Search Duration

Select the surfer and edit TinyWaveSurfer > Shark Death Response:
- Death Duration
- Death Knock Up
- Death Sink Speed
- Death Spin Speed
- Respawn After Death
- Respawn Delay
