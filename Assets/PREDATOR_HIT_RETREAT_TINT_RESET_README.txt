PREDATOR HIT RETREAT + TINT RESET PATCH

Updated:
- Assets/Scripts/SharkLaneSwimmer.cs
- Assets/Scripts/GiantSquidLaneSwimmer.cs

Behaviour:
- A thrown ocean item hit now makes the shark or giant squid immediately turn away and retreat.
- Retreat duration, speed multiplier, and recovery are editable under Hit Retreat.
- Predators cannot stalk, change lanes toward the surfer, or immediately attack during retreat.
- Their original SpriteRenderer tint is cached and restored after every hit flash.
- Tint and rotation are also reset when the object is enabled or disabled, preventing pooled/recycled predators from remaining blood red.

Default tuning:
- Hit Retreat Duration: 2.25 seconds
- Hit Retreat Speed Multiplier: 2.4
- Hit Retreat Recovery: 1.1 seconds
