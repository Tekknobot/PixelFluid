Spatial creature audio patch

Runtime clips copied into Assets/Resources/Audio/SFX so dynamically spawned actors can load them.
- alien_ship: looping spatial UFO movement audio
- helicopter: looping spatial helicopter movement audio
- missile_launch: spatial one-shot at missile launch
- duckling_quack: randomized spatial duckling quacks
- rubber_duck_quack: spatial giant rubber duck quack when launching ducklings
- reaper_horn: randomized spatial Godzilla horn

All sources use logarithmic 3D rolloff, distance attenuation, and light Doppler.
