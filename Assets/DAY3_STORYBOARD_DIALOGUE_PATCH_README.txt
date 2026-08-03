DAY 3 STORYBOARD DIALOGUE PATCH

Changed files:
- Assets/Scripts/StoryboardCutsceneSystem.cs
- Assets/Scripts/SurfDayProgressionDirector.cs

Behavior:
- Uses Resources/Storyboards/Day3/board_1, board_2 and board_3.
- Plays during the real Day 2 -> Day 3 transition and Developer Next Day.
- Freezes gameplay and ducks audio using the existing storyboard system.
- Clears prior-day objects before the boards.
- Does not initialize Day 3, spawn the Shadow, pickups, or enemies until the boards finish.
- Loading an existing Day 3 save does not replay the opening.

Dialogue:
1. CHUCK: ...THERE'S SOMEONE OUT HERE.
2. CHUCK: HEY! WHY ARE YOU COPYING ME?
3. CHUCK: WHAT ARE YOU? / SHADOW: ...
