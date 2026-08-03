SURFER SLUG - PERSISTENT APPLICATION SAVE UPDATE

The existing stage checkpoint has been upgraded from PlayerPrefs to a versioned JSON file stored at:
Application.persistentDataPath/surfer_slug_save.json

The system now saves:
- Day, chapter, day time, rescues, and boss/sunset stage
- Lives and all ability/upgrade levels
- Total Stoke and current-day Stoke
- Chuck's horizontal position, wave index, health, facing direction
- Throwable inventory sprite names

Automatic saving occurs every 60 seconds during an active player run and when the application:
- loses focus
- is paused/backgrounded
- closes normally

The save writer uses a temporary file and a backup copy to reduce corruption risk. Existing PlayerPrefs stage saves are migrated automatically on first load.

Files added:
- Assets/Scripts/SurfPersistentSaveManager.cs

Files updated:
- Assets/Scripts/SurfStageSaveSystem.cs
- Assets/Scripts/SurfDayProgressionDirector.cs
- Assets/Scripts/SurferSlugPauseMenu.cs
- Assets/Scripts/AirTrickScoreSystem.cs
- Assets/Scripts/TinyWaveSurfer.cs
