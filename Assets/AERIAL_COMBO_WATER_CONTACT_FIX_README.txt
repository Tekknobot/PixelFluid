AERIAL COMBO WATER-CONTACT FIX

TinyWaveSurfer.cs now ends an aerial combo as soon as the descending surfer reaches the live water surface.

Changes:
- Trick animations and queued tricks can no longer hold the surfer above the water.
- Water contact immediately clears the active and queued trick states.
- TriggerAirTrick rejects late inputs when the surfer has already reached the surface.
- A new double/triple jump requires a completely new initial takeoff.
