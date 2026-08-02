SURFER SLUG — DAY 1 STORYBOARD CUTSCENE

Runtime flow
------------
1. A new or restarted run reaches Day 1 Dawn.
2. Gameplay time freezes before enemies spawn.
3. The active ocean remains visible under a dark overlay.
4. Audio is ducked globally.
5. Three square storyboard boards appear with Silver typewriter text.
6. A / Space / Enter completes the current line or advances.
7. Gameplay and audio restore before the first encounters spawn.

Continue/loading a saved run does not replay the opening.

Artwork paths
-------------
Assets/Resources/Storyboards/Day1/board_1.png
Assets/Resources/Storyboards/Day1/board_2.png
Assets/Resources/Storyboards/Day1/board_3.png

Dialogue and presentation tuning
--------------------------------
Assets/Scripts/StoryboardCutsceneSystem.cs

Useful fields:
- dimOpacity
- duckedAudioVolume
- fadeDuration
- boardTransitionDuration
- charactersPerSecond
- boardDisplaySize

The system loads Assets/Resources/Fonts/Silver SDF.asset directly and falls
back to PixelFontLibrary.TmpMedium if Silver cannot be found.
