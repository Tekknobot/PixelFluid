WHALE INTER-WAVE SWIMMER

- whale_move.png is imported as sixteen 128 x 128 pixel-animation frames under Resources/Whales.
- SectionPopulationSpawner now includes one whale in every horizontal ocean section.
- WhaleLaneSpawner creates the whale at runtime like the giant squid spawner.
- WhaleLaneSwimmer gives whales slow independent patrol movement, occasional lane changes, wave-following tilt, and randomized breaches.
- Whales currently have no attack or player-damage behaviour.

Useful inspector defaults in WhaleLaneSwimmer:
- Horizontal Speed: 0.34
- Breach Delay Range: 12 to 25 seconds
- Breach Duration: 2.1 seconds
- Breach Height: 1.15

Whale size is controlled by WhaleLaneSpawner.scale, currently 0.72.
