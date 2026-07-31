SURFER SLUG — SAVE / CONTINUE / SPAWN FADE PATCH

Added:
- Continue button uses Resources/SurferSlugUI/Buttons/continue_button.
- Continue is disabled/dimmed when no valid save exists.
- Play starts a new run and replaces the old checkpoint.
- Paused-game Play still resumes normally.
- Progress is saved whenever the day enters a new chapter/stage.
- Save contains day, chapter, stage time, rescues, boss state, and lives.
- Final game over opens the main menu instead of automatically restarting.
- Continue rebuilds the ocean population for the saved day/chapter and respawns the player.
- New ocean pickups, swimmers, sea creatures, special encounters, and bosses fade in.

New scripts:
- Scripts/SurfStageSaveSystem.cs
- Scripts/OceanSpawnFadeIn.cs

Patched:
- Scripts/SurfDayProgressionDirector.cs
- Scripts/SurfRunLifeManager.cs
- Scripts/SurferSlugPauseMenu.cs
