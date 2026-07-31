SURFER SLUG — UNIFIED PIXEL HUD

This patch redesigns the runtime gameplay HUD to match the supplied mock-up:
- Three separate dark pixel-style panels across the top.
- Objective and LIVES are now together in the left panel.
- DAY 1, current phase, clock, day progress, and 12A/6A/12P/6P labels are in the centre.
- Throwable inventory uses the real pickup sprites in individual inset slots with ×N counts.
- Using/throwing an item immediately reduces or removes its slot.
- The old standalone IMGUI lives box is removed; SurfRunLifeManager still uses OnGUI only for the death fade.
- Chapter banners remain centred below the HUD and use the same visual style.

Main file:
Assets/Scripts/SurferSlugMinimalHud.cs

Lives integration:
Assets/Scripts/SurfRunLifeManager.cs
