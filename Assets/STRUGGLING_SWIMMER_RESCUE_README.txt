STRUGGLING SWIMMER RESCUE PATCH

Added:
- StrugglingSwimmerSpawner.cs
- StrugglingSwimmerDrifter.cs
- StrugglingSwimmerAnimation.cs
- Runtime Resources copy of the 16-frame struggling_swimmer sprite sheet
- Automatic installation through BeachGameBootstrap

Behaviour:
- One swimmer spawns in a random inter-wave lane.
- The swimmer enters from beyond the left or right edge and fades naturally into view.
- It animates through all 16 supplied frames.
- It struggles with irregular bobbing, tilting, slow drift, wandering, and direction changes.
- A surfer on either bordering wave rescues the swimmer by moving within range.
- The swimmer flies toward the surfer, enlarges, turns green, fades out, and displays SAVED!.
- Another swimmer spawns after a short delay.
