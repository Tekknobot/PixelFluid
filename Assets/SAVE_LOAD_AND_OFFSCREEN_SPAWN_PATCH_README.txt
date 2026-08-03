SAVE / LOAD + OFFSCREEN SPAWN PATCH

- Developer chapter, boss, and reset actions now close the paused developer menu before advancing.
- Chapter transitions queue a checkpoint after deferred Unity spawns/destructions finish.
- Rescues and boss defeat now also queue stable checkpoints.
- Save JSON writes are explicitly flushed to disk before replacing the active save.
- Removed the stale static Godzilla spawn lock that prevented the boss from returning after an in-place Continue/load.
- Boss arena prison objects are cleared when rebuilding a saved run.
- Added CameraSafeSpawnUtility. Enemy entry positions are selected outside the live gameplay camera while staying inside the chosen water section.
- Sharks, squids, whales, blood sharks, transparent squids, stingrays, Godzilla, rubber-duck boss, jellyfish schools, and bloodfish schools use camera-safe entry placement.
