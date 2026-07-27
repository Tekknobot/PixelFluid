SURFER DYNAMIC WAVE SORTING PATCH

Changed:
- Assets/Scripts/PixelWaterGPU.cs
- Assets/Scripts/TinyWaveSurfer.cs

Behaviour:
- Each surfer now derives its transparent render queue from the exact PixelWaterGPU layer it is riding.
- The surfer renders immediately above its own water layer.
- Inter-wave sharks remain between adjacent wave queues.
- A surfer riding a wave behind a shark is therefore covered by that shark.
- A surfer riding the wave in front of the shark remains visible in front.
- Sorting refreshes immediately whenever the surfer changes wave layers and is verified again in LateUpdate.

The SpriteRenderer local order defaults to -10 so a shark sharing the same inter-wave queue draws above the background surfer.
