Horizontal Seabed Layer Offset Patch

When Horizontal Seabed is enabled, each independently spawned wave simulation
now offsets horizontalSeabedHeight by the same vertical world-space offset used
for that simulation's particles, tank bounds, emitter, and beach height.

This preserves the same local seabed depth for all eight foreground-to-horizon
wave rows instead of forcing every row to collide against the foreground
simulation's absolute seabed Y position.
