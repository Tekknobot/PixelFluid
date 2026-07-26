LAYERED BIG WAVE — SURF SANDBOX STYLE PATCH

This patch keeps one GPU particle body but drives it as several vertical wave
layers. The result is a larger, thicker wave with a distinct surge, body,
crest and throwing lip.

Wave layers
1. Deep surge:
   Pushes the lower water first and supplies the wave's momentum.

2. Body push:
   Arrives slightly later and compresses the middle water into a thick wall.

3. Crest lift:
   Arrives after the body and lifts the surface water into a taller crest.

4. Lip throw:
   Pushes the top layer forward and then downward near the break point.

New PixelWaterGPU Inspector section: Layered Big Wave
- Layered Wave Enabled
- Wave Layer Count
- Wave Layer Phase Offset
- Deep Surge Force
- Body Push Force
- Crest Lift Force
- Crest Layer Thickness
- Layer Compression
- Layer Forward Stacking
- Lip Throw Boost
- Big Wave Scale

Quick setup
Open the PixelWaterGPU component menu and select:
Surf Preset > Layered Big Wave

Recommended starting values
Layer Count: 4
Phase Offset: 0.29
Deep Surge: 16
Body Push: 13
Crest Lift: 21
Crest Thickness: 0.22
Layer Compression: 0.88
Forward Stacking: 12
Lip Throw: 11
Big Wave Scale: 1.55
Wave Frequency: 0.18

Tuning
- Taller wave: raise Crest Lift and Big Wave Scale.
- Thicker wave: raise Deep Surge and Body Push.
- Longer period: lower Wave Frequency.
- More barrel: raise Lip Throw Boost and Curl Rotation Strength.
- Unstable/explosive water: lower Big Wave Scale before changing pressure.
