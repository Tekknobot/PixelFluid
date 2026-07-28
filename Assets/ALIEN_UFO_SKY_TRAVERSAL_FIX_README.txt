ALIEN UFO SKY TRAVERSAL FIX

- Reduced the default UFO world scale to 0.42 so the 117x91 source frames read as a sky vehicle rather than a foreground boss.
- Raised normal roaming to the upper 76%-92% of the camera viewport.
- Added a permanent camera-relative sky band clamp.
- The sprite's visible bounds are included in the clamp, so its bottom edge cannot overlap the water.
- Swoops are shallower and remain aerial.
- Hunting and tractor-beam positioning now use the higher of player-relative altitude or the sky-band minimum.
- The UFO still enters from either side, traverses, banks, swoops, hunts, beams, and retreats.

Inspector controls on AlienUfoController:
- Ship Scale
- Sky Height Viewport
- Lowest Sky Viewport Y
- Highest Sky Viewport Y
- Swoop Depth
- Hover Above Player
