SURFER SLUG — SAVE/DEVELOPER TIME + BOSS ARENA SPEED PATCH

Changed scripts:
- SurfDayProgressionDirector.cs
- GodzillaLaneSwimmer.cs
- RubberDuckBossSwimmer.cs

Day/night synchronization:
- The procedural sky is now driven directly by the run timer.
- Every day begins at 6:00 AM.
- The end of the 12-minute day reaches midnight.
- Continue loading immediately restores the matching sky, sun, moon, stars, fog, and HUD clock.
- Developer Next Chapter, Spawn Boss, Reset Day, and Next Day paths now display the correct time.
- Boss-defeat accelerated sunset immediately updates the sky.

Boss arena movement:
- Reaper arena movement multiplier: 1.85x.
- Rubber Duck arena movement multiplier: 2.25x.
- Multipliers only apply while a BossArenaPrison encounter is active.
- Values are serialized and can be adjusted in each boss component Inspector.
