SURFER / SHARK DEPTH QUEUE FIX

The previous queue spacing was 2:
  water 3000, shark lane 3001, next water 3002

That left no unique render queue for a surfer that must be above its own water
but below the shark lane in front. SpriteRenderer.sortingOrder could not solve
this reliably because the water is drawn procedurally.

This patch reserves four queue slots per wave depth:
  water 3000
  surfer 3001
  shark/inter-wave lane 3002
  next foreground water 3004
  next foreground surfer 3005

PixelWaterGPU now enforces an effective interleaved queue step of at least 4.
TinyWaveSurfer uses its current wave queue + 1.
InterWaveRenderItem uses the midpoint of the queue gap for sharks and items.

Result:
- A surfer is always visible above the water it rides.
- A shark in the lane immediately in front covers that surfer.
- A surfer on the next foreground wave covers that shark.
