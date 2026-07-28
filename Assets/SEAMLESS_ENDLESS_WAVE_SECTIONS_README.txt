SEAMLESS ENDLESS WAVE SECTIONS PATCH

Changes:
- Keeps the existing 3-world-unit section overlap.
- Replaces reflected horizontal wall bounces with a soft outward-velocity slowdown and zero-velocity edge containment.
- Cross-fades particle alpha and foam over 1.5 world units at both horizontal edges.
- Uses one real-time emitter clock across centre, left and right sections so wave crests stay in phase.
- Disables the repeated left-to-right tropical colour ramp while seamless section colour is enabled, removing the dark/light vertical colour jump.

Inspector defaults in PixelWaterGPU:
- Section Edge Fade Width: 1.5
- Soft Horizontal Boundary Width: 1.5
- Soft Horizontal Boundary Strength: 1
- Synchronize Horizontal Section Phase: enabled
- Seamless Section Colour: enabled

The fade width is half the 3-unit overlap, allowing neighbouring sections to cross-fade without revealing the background.
