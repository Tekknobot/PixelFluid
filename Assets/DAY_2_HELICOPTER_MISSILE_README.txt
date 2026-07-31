DAY 2 HELICOPTER + TRACKING MISSILE

- The helicopter replaces the Day 1 UFO during Day 2 Strange Tide.
- Uses helicopter_move while patrolling and helicopter_attack while aiming/firing.
- Fires helicopter_missile at a deliberately fast animation rate.
- Missile turns gradually toward the player rather than snapping directly at them.
- Up + Action prioritizes an incoming missile, then the helicopter, then the Day 1 UFO.
- Hitting the missile destroys it and ricochets the thrown item.
- Hitting the helicopter before or after launch cancels its attack, removes its active missile, and forces a retreat/cooldown.
- Both helicopter and missile are cleaned up on day transitions and run resets.
