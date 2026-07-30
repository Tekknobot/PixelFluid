SURFER SLUG ANIMATED FRONT END

The menu now uses the existing Surfer Slug logo sheet and the four UI button sprites.

At scene start:
- The ocean and visual simulations remain active.
- Gameplay scripts are paused.
- The logo moves in from the left.
- The button column moves in from the right.
- PLAY begins the game.

During gameplay:
- ESC or controller START opens the same menu.
- PLAY resumes the game.
- ESC/START also resumes from the pause version.

The menu never uses Time.timeScale = 0, so water, rain, stars, lighting, particles,
and other whitelisted visual systems continue.
