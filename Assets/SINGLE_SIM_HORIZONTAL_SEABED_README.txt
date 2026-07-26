SINGLE SIMULATOR + PERFECTLY HORIZONTAL SANDBED

Automatic simulation layering is disabled.

To create additional layers:
1. Duplicate the water simulator GameObject manually.
2. Give each duplicate its own position, render depth and timing.
3. Each duplicate remains a separate GPU simulation.

New PixelWaterGPU Inspector controls
- Horizontal Seabed Enabled
- Horizontal Seabed Height

Horizontal Seabed Height can be adjusted while Play Mode is running. It changes:
- the visible GPU sand
- water/seabed collision height
- gameplay surface fallback
- board-related seabed queries

The sand is now perfectly flat from left to right. Shore ramps, reef ramps and
curved beach profiles are bypassed while Horizontal Seabed Enabled is checked.

Preset:
Surf Preset > Single Simulator Horizontal Seabed
