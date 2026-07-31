SURFER SLUG — SEMI-TRANSPARENT NEXT-THROW HUD

Updated Assets/Scripts/SurferSlugMinimalHud.cs

Changes
- Main, inset, item, and chapter panels now use a softer semi-transparent purple tint.
- Items panel is labelled "ITEMS • NEXT TO THROW".
- The panel displays at most four distinct throwable item types.
- Items are shown in the same FIFO order used by TinyWaveSurfer.ThrowSodaCan().
- The first slot is marked NEXT and receives a slightly stronger white border.
- Duplicate pickups remain grouped with an xN count.
- Additional hidden item types are summarized as +N at the right edge.
- Throwing an item immediately refreshes the visible order and counts.

The HUD still appears only while a living player-controlled surfer is active.
