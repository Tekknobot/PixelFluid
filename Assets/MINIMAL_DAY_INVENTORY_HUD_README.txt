SURFER SLUG - MINIMAL DAY + INVENTORY HUD

Added SurferSlugMinimalHud.cs.

The HUD is created automatically at runtime and displays:
- A thin top-centre day timeline with the exact in-game time and NIGHT/DAWN/DAY/DUSK phase.
- A top-left item row using each collected item's actual pickup sprite.
- Duplicate items grouped as one sprite with an xN counter.
- Immediate inventory removal/count reduction whenever an item is thrown.

TinyWaveSurfer now exposes a read-only inventory snapshot for the HUD. Gameplay inventory remains private and cannot be modified by UI code.
