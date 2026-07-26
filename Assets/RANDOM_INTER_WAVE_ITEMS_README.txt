RANDOM INTER-WAVE ITEMS PATCH
=============================

The Beach Game bootstrap now automatically adds RandomInterWaveItemSpawner.
It creates sharks, whales, buoys, crates, bottles and treasure between every
pair of water render bands.

FEATURES
- Every item receives InterWaveRenderItem and the correct lane render queue.
- Items spawn near the boundary between adjacent water bands, so waves cover
  portions of them and produce a layered depth effect.
- Items drift, bob and wrap horizontally.
- Every item has a BoxCollider2D.
- Click an item to interact with it. Non-trigger 2D player collisions also call
  Interact(). Sharks need two interactions and whales need three by default.
- Removed objects automatically respawn when Maintain Population is enabled.

INSPECTOR
Select "Beach Game Prototype" during Play Mode and edit:
- Items Per Lane
- Maintain Population / Respawn Delay
- Drift, bob and scale ranges
- Lane Vertical Padding
- Trigger colliders and interaction logging

Use the component context menu "Rebuild Random Inter-Wave Items" to regenerate.
