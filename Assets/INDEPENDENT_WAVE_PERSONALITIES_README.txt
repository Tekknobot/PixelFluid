INDEPENDENT WAVE PERSONALITIES

PixelWaterGPU now gives each vertical wave simulation a distinct wave profile.
The profiles vary wave height, horizontal push, frequency, pulse shape, emitter
width, body surge and crest lift. No extra grids, particles, simulations or
per-frame searches are added.

Inspector controls on the master PixelWaterGPU:
- Vary Independent Wave Profiles: enables/disables the system.
- Independent Profile Randomness: small per-run variation (default 0.06).

The authored profile pattern supports eight lanes and wraps for additional lanes.
It is intentionally non-linear so neighbouring lanes do not look like simple
scaled copies. The setup is idempotent and will not multiply forces repeatedly
when a component is re-enabled.
