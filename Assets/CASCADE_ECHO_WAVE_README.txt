CASCADE ECHO WAVE — 3D-LIKE LAYERED WATER

This replaces the previous "different force bands" approach with delayed copies
of the original wave pulse.

How it works
- The master wave remains unchanged.
- Echo 1 repeats the same pulse after a short delay and slightly behind it.
- Echo 2 repeats it again farther behind and slightly lower.
- Echo 3 repeats it again, producing a thick layered face.
- Each echo uses the same forward force, vertical lift and curl logic as the
  original simulation.
- Overlapping vertical bands merge the echoes into one thick water volume.

Use
1. Select the PixelWaterGPU component.
2. Open its component context menu.
3. Choose:
   Surf Preset > Cascade Echo Big Wave

Recommended starting values
- Cascade Mode: Quad Echo
- Cascade Echo Count: 3
- Cascade Delay: 0.18
- Cascade Back Offset: 0.42
- Cascade Vertical Offset: 0.12
- Cascade Amplitude Falloff: 0.88
- Cascade Speed Falloff: 0.94
- Cascade Curl Retention: 0.96
- Cascade Volume Force: 9.5
- Cascade Stack Lift: 7.5
- Cascade Band Thickness: 0.28
- Cascade Blend: 1.0

Tuning
- More visible layers:
  Raise Cascade Back Offset to 0.50-0.70.
- Thicker unified wave:
  Raise Cascade Band Thickness and Cascade Volume Force.
- More height:
  Raise Cascade Stack Lift.
- Longer spacing:
  Raise Cascade Delay.
- Stronger rear echoes:
  Raise Cascade Amplitude Falloff toward 0.95.
- Too chaotic:
  Lower Cascade Blend first.
