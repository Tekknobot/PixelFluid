Rain wave-layer update

- Replaced the single camera-height splash band with lightweight splash emitters for every active PixelWaterGPU layer.
- Splash positions sample GetGameplaySurfaceHeight(x), so impacts follow the actual moving crest of each wave.
- Per-wave splash materials use the wave render queue to preserve correct inter-layer depth.
- Rain droplets now use a tapered, softly diagonal procedural texture with explicit transparent pixels.
- Rain and splash materials explicitly use alpha blending, disable depth writes, and avoid black texture backgrounds.
- The old broad splash particle renderer is retained but disabled for compatibility.
- Wave lists refresh only once per second and impacts emit at a capped interval to stay inexpensive.
