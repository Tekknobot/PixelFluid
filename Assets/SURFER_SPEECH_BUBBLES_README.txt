SURFER SPEECH BUBBLES PATCH

Added a runtime pixel-style speech bubble to TinyWaveSurfer.

Automatic triggers:
- A shark entering sharkSpeechRange triggers a short danger line.
- The player surfer idling past idleSpeechDelay triggers a quieter thought.
- Shark dialogue takes priority and resets the idle-dialogue cooldown.
- Speech is hidden immediately on death.

Inspector settings are under TinyWaveSurfer > Speech Bubbles.
Edit idleSpeechLines and sharkSpeechLines directly to shape Chuck's voice and narrative.

No TextMeshPro package or prefab setup is required. SurferSpeechBubble creates its own bubble sprite and TextMesh at runtime.
