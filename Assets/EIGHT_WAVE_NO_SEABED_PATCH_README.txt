EIGHT OFFSET WAVE SIMULATIONS — NO SEABED

Changes in this patch:
- TropicalSeabed is no longer created or drawn.
- Any existing TropicalSeabed child is removed when PixelWaterGPU enables.
- The independent simulation layer builder is now actually called at runtime.
- The default and scene setup now create 8 complete GPU water simulations.
- Layers begin at the foreground/bottom and rise by 0.34 world units toward the horizon.
- Each layer has a delayed wave start and alternating horizontal offset so the waves do not synchronize into one stack.
- Independent layer count now supports up to 12.

Main defaults:
- createIndependentWaveLayers: true
- independentLayerCount: 8
- independentLayerDelay: 0.22
- independentLayerBackOffset: 0.14
- independentLayerVerticalOffset: 0.34

Each layer remains a separate simulation with its own particle buffers and collision grid.
