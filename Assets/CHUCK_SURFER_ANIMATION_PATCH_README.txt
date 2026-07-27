CHUCK SURFER ANIMATION PATCH

Reorganized:
- Animator Controller: Assets/Resources/Animations/chuck.controller
- Idle clip remains: Assets/Animations/chuck_idle_long.anim
- Move clip remains: Assets/Animations/chuck_move.anim
- Removed duplicate controller from Assets/Resources/Surfers.

TinyWaveSurfer.cs now:
- Loads Resources/Animations/chuck.controller automatically.
- Plays Idle when the player has no horizontal input.
- Plays chuck_move when the player moves left or right.
- Keeps autonomous surfers in chuck_move.
- Uses direct Animator.Play calls, so no IsMoving parameter or transitions are required.
- Handles Multiple-mode resource sprites via Resources.LoadAll<Sprite>().
- Stops on Idle during death and restores Idle after respawn.

Open the project and allow Unity to reimport moved assets before entering Play mode.
