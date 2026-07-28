THREE-SECTION ENDLESS OCEAN V2

This patch starts from Assets(65).zip.

- Three complete horizontal water sections are created after the original vertical stack finishes.
- The player and camera never teleport.
- A section recycles only after its complete right/left edge is behind the camera.
- Runtime clones are configured before activation, preventing accidental extra vertical stacks.
- A small overlap hides the hard particle boundary between adjacent simulations.
- Normal camera follow retains its old offsets and smoothing, with dynamic clamping against the active three-section world.
- Cinematic camera horizontal clamping uses the same active world bounds.
- Procedural starry night follows the camera horizontally, so it cannot expose a cyan/empty background at section seams.
- Water-dependent movers and spawners select the nearest horizontal section instead of mixing 24 simulations into one vertical list.

Runtime object: Endless Three-Section Wave World
Inspector controls: Seam Overlap, Recycle Padding, Log Recycling.
