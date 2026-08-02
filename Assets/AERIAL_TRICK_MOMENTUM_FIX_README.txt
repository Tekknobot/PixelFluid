AERIAL TRICK MOMENTUM FIX

Updated Assets/Scripts/TinyWaveSurfer.cs

- Replaced the rewind-prone normalized combo arc with continuous velocity/gravity motion.
- The first selected trick plays completely.
- A different trick can be buffered and begins after the current animation completes.
- Trick 2 restores upward velocity like a double jump.
- Trick 3 restores a slightly smaller upward velocity like a triple jump.
- Each chained jump preserves and increases forward momentum.
- Repeated tricks remain blocked during the same jump.
- Trick animation frames remain attached to the surfer's current world position.

Inspector tuning under Aerial Trick Chain:
- Second Trick Jump Strength
- Third Trick Jump Strength
- Chained Trick Forward Boost
- Trick Chain Distance Bonus (small secondary influence)
