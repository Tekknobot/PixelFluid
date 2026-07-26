SHARK ATTACK BEHAVIOUR PATCH

The single inter-wave Shark prefab now contains SharkSpriteAnimation.

Behaviour:
- Loops the 9-frame swim strip at 9 FPS.
- Randomly attacks every 3.5-7.5 seconds.
- Plays the complete 9-frame attack strip once at 12 FPS.
- Lunges at 1.55x movement speed during the attack.
- Returns to swimming automatically.
- Clicking the shark requests an attack.
- Contact with a non-trigger Collider2D requests an attack.
- Lane changes wait until the active attack finishes.

The component does not depend on the Animator Controller; it assigns sprite frames directly.
The attack sheet has also been normalized to 32 pixels per unit, Point filtering, and no compression.
