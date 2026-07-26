STARTUP PRESWARM FOR ALL WATER SIMULATORS

Every PixelWaterGPU instance, including manually duplicated simulations, now
starts with its particles raised above their normal starting positions and
already moving downward.

This means the water is visibly falling and settling as soon as Play Mode starts,
instead of beginning as a motionless rectangular block.

New PixelWaterGPU Inspector controls:

- Preswarm On Start
- Startup Drop Height
- Startup Downward Speed
- Startup Drop Variation
- Startup Preswarm Steps
- Startup Preswarm Delta

Recommended defaults included in this patch:

- Startup Drop Height: 1.15
- Startup Downward Speed: 2.4
- Startup Drop Variation: 0.28
- Startup Preswarm Steps: 24
- Startup Preswarm Delta: 0.008333

Lower Preswarm Steps to make the initial fall more visible.
Raise Preswarm Steps to begin with a more settled, already-moving wave.
Each manually duplicated simulator calculates its own independent preswarm.
