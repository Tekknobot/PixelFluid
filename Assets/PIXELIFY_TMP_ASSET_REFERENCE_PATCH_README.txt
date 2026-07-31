SURFER SLUG - PIXELIFY TMP FONT ASSET PATCH

The UI font system now loads the generated TextMesh Pro font assets directly from:
Assets/Resources/Fonts/

Weight mapping:
- PixelifySans-Regular SDF: supporting/body copy
- PixelifySans-Medium SDF: HUD and standard UI
- PixelifySans-SemiBold SDF: buttons and short labels
- PixelifySans-Bold SDF: headings, alerts, scores, and speech bubbles

The previous runtime TMP_FontAsset.CreateFontAsset calls were removed. This prevents Unity from silently creating new dynamic SDF atlases and ensures the font assets configured in the editor are the ones used at runtime.

Legacy Text and TextMesh components still use the matching TTF files from Assets/Resources/Fonts.
