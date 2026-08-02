AERIAL COMBO STOKE SCORING PATCH

Updated Assets/Scripts/AirTrickScoreSystem.cs and TinyWaveSurfer.cs.

Stoke now considers:
- Peak jump height
- Each unique trick's base value
- Explicit completed chain length (single, double, or triple)
- Total airtime
- Horizontal distance carried from the initial jump
- Double-chain and triple-chain milestone bonuses
- Chain multipliers
- Clean landing bonus

The surfer controller now reports chain length, total airborne time, and travel
from takeoff to landing. The old AwardJump signature remains as a compatibility
overload for other scripts.

Inspector tuning is under Air Trick Score System > Combo Chain Scoring.
