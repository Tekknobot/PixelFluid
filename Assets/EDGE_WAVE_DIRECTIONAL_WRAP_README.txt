EDGE WAVE DIRECTIONAL WRAP PATCH

Updated Assets/Scripts/TinyWaveSurfer.cs player layer movement:

- Up/W from the first edge wave wraps to the last edge wave.
- Down/S from the last edge wave wraps to the first edge wave.
- Wrapping only occurs when pressing outward from an edge.
- Pressing inward from either edge moves to the adjacent interior wave.
- Interior Up/Down presses still move exactly one wave per press.
