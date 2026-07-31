Surfer Slug unified HUD patch

- Added a crisp thin white rectangular border to every main panel, lives inset,
  chapter banner, and inventory slot.
- Switched runtime UI text to Unity's high-resolution Arial dynamic font, with
  LegacyRuntime.ttf as a fallback.
- The HUD starts hidden and only fades in after a valid player-controlled surfer
  has spawned.
- The HUD fades out while the player is dead, disabled, despawned, or absent.
- Inventory slot objects are cleared while hidden and rebuilt after respawn.
