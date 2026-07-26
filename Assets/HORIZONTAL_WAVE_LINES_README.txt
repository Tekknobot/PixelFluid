HORIZONTAL DEPTH WAVE LINES

The water simulations now sit on separate, straight horizontal rows.

Layout:
- Layer 0: lowest foreground row
- Layer 1: one fixed step higher
- Layer 2: another equal step higher
- Layer 3: highest background row

There is:
- no perspective scaling
- no curved rise
- no shrinking toward the horizon
- no diagonal stacking

Every row remains parallel and horizontal.

The layers still:
- use separate particle buffers
- simulate independently
- never collide across simulations
- emit the same full big-wave profile
- use delayed timing
- render on separate depth planes
- darken progressively toward the back

Preset:
Surf Preset > Horizontal Depth Wave Lines

Recommended values:
- Vertical Offset: 0.34
- Back Offset: 0.08
- Depth Offset: 0.08
- Scale Falloff: 1.0
- Rise Curve: 1.0
