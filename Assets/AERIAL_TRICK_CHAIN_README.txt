AERIAL TRICK CHAIN PATCH

Forward surf jumps can now chain the three existing deterministic trick animations:
- Space / controller A: chuck_handstand
- E or B / controller B: chuck_rotation
- F or X / controller X: chuck_flip

Design rules:
- Each trick can be used only once per jump.
- Up to three unique tricks can be chained.
- Each successful new trick adds a small amount of airtime and forward travel.
- Bonuses diminish with each step, preventing unlimited flight.
- Inputs are briefly locked so each animation remains readable.
- Tricks cannot begin too close to landing.
- Existing AirTrickScoreSystem automatically awards its multi-trick combo bonus.

Tuning is under TinyWaveSurfer > Aerial Trick Chain:
- Maximum Tricks Per Chain
- Trick Chain Airtime Bonus
- Trick Chain Distance Bonus
- Trick Chain Input Lock
- Latest Trick Chain Time
