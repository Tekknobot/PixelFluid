CHUCK LONG-IDLE PRONE PATCH

Patched Assets/Scripts/TinyWaveSurfer.cs.

Behavior:
- The player starts in chuck_idle while standing still.
- After 7 seconds without movement, jumping, or wave-layer input, chuck_prone plays.
- chuck_prone loops for as long as the player remains inactive.
- Any movement/jump/layer input immediately resets the timer and restores the correct animation.
- Death and respawn also reset the long-idle timer.

Inspector settings on TinyWaveSurfer:
- Play Prone After Long Idle
- Prone Idle Delay
