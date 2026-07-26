SINGLE ANIMATED SHARK PATCH
============================

This patch removes automatic random-item spawning from BeachGameBootstrap.
Only one Assets/Resources/Shark.prefab instance is spawned.

Runtime behaviour:
- Uses the existing Animator and Shark_Move animation.
- Remains inside the visible orthographic camera and the water tank.
- Reverses and flips at the left and right screen edges.
- Samples the two neighbouring GPU water layers for vertical motion and tilt.
- Periodically moves into an adjacent inter-wave lane.
- Changes its transparent render queue halfway through each lane crossing so it
  passes through the intervening wave layer instead of remaining on one layer.

Runtime hierarchy:
Pixel Water GPU V2
  Shark - Inter-Wave Swimmer

Tuning:
Select the runtime shark and edit SharkLaneSwimmer fields for speed, lane-change
frequency, lane-change duration, water following, current influence and tilt.
