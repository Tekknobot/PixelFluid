INTERLEAVED WATER RENDERING PATCH
=================================

What changed
------------
PixelWaterGPU now renders one shared GPU particle simulation as multiple
horizontal transparent passes instead of one indivisible draw call.

Default queues with 4 water bands:
- Water band 0: 3000
- Object lane 0: 3001
- Water band 1: 3002
- Object lane 1: 3003
- Water band 2: 3004
- Object lane 2: 3005
- Water band 3: 3006

This allows sharks, whales, surfers, pickups, and other renderers to appear
between sections of the same wave without duplicating the simulation.

How to place an object between wave bands
-----------------------------------------
1. Select the object.
2. Add Component > Inter Wave Render Item.
3. Assign the PixelWaterGPU object, or leave it blank for automatic lookup.
4. Choose Lane Index 0, 1, or 2.

Lane 0 is the lowest/background gap. Higher lane numbers render farther toward
the foreground.

PixelWaterGPU inspector controls
--------------------------------
- Interleaved Rendering Enabled
- Interleaved Water Band Count
- Interleaved Base Render Queue
- Interleaved Queue Step
- Interleaved Band Overlap

The overlap prevents thin seams where adjacent bands meet.

Performance
-----------
The simulation still runs once. Only the render call is repeated per band.
With four bands this adds four procedural draw submissions, not four compute
simulations or four particle buffers.
