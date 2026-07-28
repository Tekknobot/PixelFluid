UFO SKY ROOT TRANSFORM FIX

Cause of the stuck/water-position bug:
AlienUfoController was calling spriteRenderer.transform.localPosition = Vector3.zero every frame.
Because the SpriteRenderer lives on the UFO root, this reset the entire UFO to the world/parent origin and defeated all movement logic.

Fixes:
- Never resets the root localPosition.
- Beam shake is applied temporarily to the computed world movement position.
- Default ship scale reduced to 0.30.
- Roaming band raised to viewport Y 0.82-0.94.
- The UFO's visible bottom edge is clamped above viewport Y 0.76.
- Roaming speed is selected per movement target instead of randomly changing every frame.
- Swoop depth reduced so swoops remain aerial.
