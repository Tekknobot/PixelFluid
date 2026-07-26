SINGLE PLAYER FINITE SANDBOX PATCH

Enable it on the main Pixel Water GPU object:
  Single Player Sandbox Mode > Single Player Mode Enabled

Runtime behaviour:
- Spawns one keyboard-controlled surfer instead of six.
- The wave simulations remain fixed in their original sandbox.
- Horizontal input moves the surfer across the active wave; it no longer shifts
  or wraps the world.
- The surfer is clamped inside both the active wave boundaries and the visible
  left/right camera edges, including while the camera follows.
- Existing surface following, slope rotation, trick jump, and adjacent wave-layer
  jumping remain active.

Keyboard:
- A / Left Arrow: move surfer left
- D / Right Arrow: move surfer right
- Down / S: move one wave layer toward the horizon
- Up / W: move one wave layer toward the foreground
- Space: jump/spin trick
- Left or Right Shift: speed boost

Inspector controls:
- Single Player Scroll Speed (now the surfer movement speed)
- Single Player Boost Multiplier
- Player Camera Edge Padding (on TinyWaveSurfer)
