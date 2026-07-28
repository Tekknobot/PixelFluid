RANDOM PLAYER SURFER OCEAN SPAWN PATCH

Changed Assets/Scripts/TinyWaveSurfer.cs.

The single-player surfer now waits for EndlessWaveSections to finish creating all three horizontal ocean sections, then chooses:
- a random X position across the full ocean,
- a random vertical wave simulation layer,
- and a random starting direction.

The surfer is placed directly on the selected wave's live gameplay surface.

Inspector control:
TinyWaveSurfer > Random Initial Ocean Spawn > Random Spawn Edge Padding

Default padding is 0.08, which keeps the surfer away from the two far outside ocean edges.
Use the component context menu command "Spawn Randomly In Ocean" to test another random position while running.
