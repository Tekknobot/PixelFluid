BOSS DEATH CAMERA FOCUS

- Reaper and Rubber Duck now request a temporary focus override from TinySurferCinematicCamera as soon as defeat begins.
- The camera pans smoothly to the boss, follows its complete shake/sink death animation, and gently zooms in.
- Focus is released after the death duration and delay, immediately before progression cleanup and boss destruction.
- The camera then smoothly returns to Chuck using whichever normal/cinematic mode was active.

Inspector controls on TinySurferCinematicCamera:
- Boss Death Focus Zoom: 2.35
- Boss Death Focus Offset: (0, 0.15)
- Boss Death Focus Smooth Time: 0.28
- Boss Death Focus Maximum Speed: 32
