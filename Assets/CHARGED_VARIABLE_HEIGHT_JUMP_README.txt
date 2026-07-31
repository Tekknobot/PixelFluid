CHARGED VARIABLE-HEIGHT FORWARD JUMP

Implemented in TinyWaveSurfer.cs.

CONTROLS
- Hold horizontal movement and hold Jump (Space / Xbox A) to charge.
- Release Jump to launch.
- A quick release makes a low jump.
- Holding for 0.65 seconds reaches the maximum jump height.
- Press Jump again while airborne to perform the handstand trick.
- Up/Down + Jump wave-layer changes remain immediate and are not charged.

DEFAULT TUNING
- minimumObstacleJumpHeight: 0.55
- maximumObstacleJumpHeight: 1.85
- fullJumpChargeTime: 0.65 seconds
- jumpChargeCurve: gentle early response, stronger upper charge range

SCORING
AirTrickScoreSystem.maximumScoringHeight is now 2.0 world units, leaving slight headroom above the 1.85 charged maximum for moving wave surfaces and gameplay variation.
Maximum height contribution remains 600 Stoke.

EXPECTED MAXIMUM TRICK SCORE
- Height: 600
- Handstand: 100
- Rotation: 140
- Flip: 180
- Triple combo bonus: 150
- Total: 1,170 Stoke

NOTES
This patch was edited outside Unity and was not compiled in the Unity Editor.
