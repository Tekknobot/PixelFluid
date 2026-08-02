SURFER SLUG — PERSISTENT LESSONS + SAVE PROGRESSION PATCH

HUD
- The objective panel now permanently shows the active mission and the current mechanic lesson.
- Temporary chapter banners still announce unlocks, but missing a banner no longer hides the control instruction.
- Lessons change with the current day/chapter and are rebuilt correctly after Continue.

SAVE / CONTINUE
- Saves now include exact unlocked SurfAbility flags.
- Saves now include Higher Launch, Faster Water Slash, and Stronger Skid upgrade levels.
- Upgrade selections save immediately.
- Ability unlocks save immediately.
- Continue restores day, chapter, time, rescues, boss state, lives, abilities, upgrades, current lesson, and encounter population.
- Older save files remain supported by deriving abilities from their saved day/chapter.

UPGRADE APPLICATION
- Upgrade levels are applied idempotently to the active player.
- They are reapplied when a player is configured/spawned and after a saved run is restored.
- Repeated restoration will not multiply the same upgrade more than once on the same surfer instance.
