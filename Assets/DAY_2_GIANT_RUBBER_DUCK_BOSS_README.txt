DAY 2 GIANT RUBBER DUCK BOSS

- Replaces the Day 2 final Blood Shark/Transparent Squid pair.
- Uses rubber_duck_move and rubber_duck_attack from Resources/RubberDuck.
- Boss health is 16 hits, exactly double Godzilla's default 8.
- Periodically spawns scaled-down homing duckling swimmers.
- Ducklings damage the player on contact and use explosion_basic.
- Thrown ocean items prioritize ducklings, then the giant duck boss.
- Destroyed ducklings explode harmlessly.


DUCKLING SPRITE + LANE LAYERING UPDATE
- Ducklings now use Resources/RubberDuck/duckling_move (32 px frames).
- Ducklings spawn at normal scale; the boss sprite is no longer scaled down.
- Each duckling owns an InterWaveRenderItem and updates it to the nearest active water lane while seeking the player.
