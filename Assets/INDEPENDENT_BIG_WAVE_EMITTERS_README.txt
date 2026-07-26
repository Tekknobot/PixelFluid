INDEPENDENT LAYER EMITTERS — REAL BIG WAVE PATCH

Every independent water simulation now uses the same complete large-wave
emitter.

Each simulation emits:
- the same deep surge
- the same body push
- the same crest lift
- the same pitching lip
- the same shoaling
- the same curl and break behaviour
- the same emitter width and pulse shape

Only the start time is delayed:
- Master: immediate
- Layer 1: +0.18 seconds
- Layer 2: +0.36 seconds
- Layer 3: +0.54 seconds

Because each layer has separate ComputeBuffers and spatial grids, particles
from different simulations still never collide.

New context-menu preset:
Surf Preset > Independent Layered Real Big Wave

Important defaults:
- 4 complete simulations
- full force on every layer
- 4 vertical emitter bands per simulation
- broad emitter width
- strong deep surge and crest lift
- progressive shaded depth rendering

This creates four complete large waves stacked in visual depth rather than one
wave with weak decorative echoes.
