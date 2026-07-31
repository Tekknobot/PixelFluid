DAY 2 STINGRAY INTEGRATION

Added:
- StingrayLaneSpawner.cs
- StingrayLaneSwimmer.cs
- Resources/Stingray/stingray_move.png

Behaviour:
- Glides through inter-wave lanes and follows the simulated water.
- Hunts the nearest living surfer and changes lanes to pursue them.
- Compensates for the missing attack sheet with a readable telegraph: slowed animation, orange warning tint, body pulse and wobble.
- Performs a fast flattened charge using the move animation at increased speed.
- Only damages during the charge, once per attack.
- Thrown ocean items target the stingray, make it flash red, interrupt its charge, and force a fast retreat.

Progression:
- Day 2 Dangerous Water introduces the first stingray.
- Day 2 Storm adds another stingray alongside the heavier predator encounter.
- Stingrays are removed when the run/day is reset.
