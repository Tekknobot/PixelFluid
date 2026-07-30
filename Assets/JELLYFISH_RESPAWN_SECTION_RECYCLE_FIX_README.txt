JELLYFISH RESPAWN + SECTION RECYCLE FIX

- Every JellyfishSwimmer now explicitly reports its destruction to its owning JellyfishSchoolSpawner.
- When the final jellyfish is destroyed, the spawner starts one respawn timer and creates a fresh randomized school.
- The replacement school can choose a new lane, formation and travel style.
- JellyfishSchoolController now subscribes directly to EndlessWaveSections.SectionRecycled.
- A school holder, anchor and every living jellyfish are shifted with their exact owning water section.
- The holder also shifts while the school is defeated, so a pending respawn occurs inside the recycled section at its new position.
- Movement configuration is reset before each respawn so randomized speed multipliers do not compound over multiple generations.
