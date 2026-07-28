GIANT SQUID INTER-WAVE PREDATOR

Added runtime giant squid spawning from:
Assets/Resources/Squid/giant_squid_move.png

Runtime components:
- GiantSquidLaneSpawner
- GiantSquidLaneSwimmer
- GiantSquidSpriteAnimation

Behaviour:
- Spawns in an inter-wave lane like the shark.
- Patrols, follows wave height/current/slope, changes adjacent lanes and stalks surfers.
- Uses the 16-frame giant_squid_move sheet continuously.
- Speeds the sheet up during a three-cycle combo attack.
- Multiple strike beats visually pressure the surfer, but TakeSharkHit is gated to one call per full combo.
- Soda cans target the nearest shark or squid.
- Soda cans trigger the same hit sound, red flash, recoil and temporary search reaction on the squid.

Primary tuning locations:
GiantSquidLaneSpawner.cs
- startingLane
- scale

GiantSquidSpriteAnimation.cs
- swimFramesPerSecond
- attackFramesPerSecond
- comboCycles
- attackSpeedMultiplier

GiantSquidLaneSwimmer.cs
- horizontalSpeed
- detectionRange
- attackRange
- hitRange
- attackRecovery
- laneDepthBias
