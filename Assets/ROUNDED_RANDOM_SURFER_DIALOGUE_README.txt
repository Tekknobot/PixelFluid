ROUNDED RANDOM SURFER DIALOGUE PATCH

Updated Assets/Scripts/SurferSpeechBubble.cs
- Added Corner Radius Pixels to the Dynamic Layout Inspector group.
- Bubble bodies now use a procedurally generated pixel-rounded rectangle.
- The outline follows the rounded corners while the existing dynamic text fitting remains intact.

Updated Assets/Scripts/TinyWaveSurfer.cs
- Expanded idle dialogue to 40 atmospheric lines.
- Expanded shark-proximity dialogue to 30 reactive lines.
- Existing uppercase/bold rendering and no-dialogue-during-jumps/wave-crossings behaviour remain active.

Inspector tuning:
TinyWaveSurfer > Speech Bubbles controls timing and dialogue arrays.
SurferSpeechBubble > Dynamic Layout > Corner Radius Pixels controls roundness.
