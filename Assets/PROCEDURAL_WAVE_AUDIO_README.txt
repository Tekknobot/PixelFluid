PROCEDURAL WAVE AUDIO PATCH
===========================

Added Assets/Scripts/ProceduralWaveAudio.cs.

What it does
------------
- Synthesizes endless ocean wash entirely from filtered noise.
- Requires no imported WAV/MP3 files.
- Adds low rolling water, middle-frequency wash, foam hiss and breaking-wave bursts.
- Samples every active PixelWaterGPU layer and reacts to its gameplay velocity.
- Uses camera-relative stereo positioning.
- Automatically creates one "Procedural Wave Audio" object after scene load.

Inspector controls
------------------
Select the runtime Procedural Wave Audio object while playing to tune:
- Master Volume
- Deep Wash / Mid Wash / Foam Hiss
- Swell Rate / Swell Depth
- Crash Amount / Crash Decay
- Activity Sensitivity
- Samples Per Wave

Notes
-----
The AudioSource is created and started automatically. The generated sound comes
from OnAudioFilterRead, while a tiny silent clip only keeps the AudioSource active.
