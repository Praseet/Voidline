# VOIDLINE — Arcade Speed & Boost Feel

**Research note:** 12 October 2026  
**Design target:** Borrow the readability and sudden velocity hit of *Need for Speed: Most Wanted (2005)*, adapted for a space-flight runner rather than copied literally.

## What the references suggest

- **Need for Speed: Most Wanted (2005):** contemporary reviews call out heavy motion blur at high speed as a major contributor to perceived velocity. Reverse-engineered technical notes describe the signature image treatment as state-driven: motion blur, bloom, a warm/desaturated grade and edge vignette, rather than just a wide camera lens.
- **Dangerous Driving / Burnout lineage:** the developer interview describes using built-in motion blur to make very high vehicle speeds feel faster while keeping the image crisp enough to read.
- **Forza’s arcade-speed discussion:** players frequently cite the combination of a closer chase camera, rising FOV, vignette and some camera movement—not blur alone—as a strong sense-of-speed recipe.
- **Most Wanted’s Speedbreaker:** the short slow-time ability is a useful reference for boost ignition weight; it is not the same effect as sustained nitrous.

## Translation to VOIDLINE

The player should feel a distinct **ignition → acceleration → sustained rush → release** sequence.

1. **Ignition impact:** about 85 ms near-hit-stop, followed by about 240 ms of slow motion, then a guaranteed return to normal time. Drive this envelope from unscaled frame time so it cannot remain stuck at slow speed when the editor or player stalls.
2. **Chase framing:** start with a fairly tight view; pull the camera forward slightly as boost rises. Use an FOV ramp, brief recoil/pitch, and a very restrained high-frequency engine rumble. Do not pull the camera away from the ship as speed rises.
3. **Sustained rush:** boost-linked peripheral streaks and low-quality URP motion blur on moving world geometry. Keep the courier and the centre of the route readable. Use intensity changes rather than a permanent full-screen tint.
4. **Exhaust:** a bright, concentrated nozzle core plus a soft, wider plasma sheath and turbulent wisps. The plume must have volume; two thin straight lines are not sufficient.
5. **Environment feedback:** rocks, landmarks and later set pieces must approach with enough lead distance for the eye to read them. Use denser parallax and authored silhouettes to sell velocity, not an ever-thicker HUD or indiscriminate screen shake.

## Current implementation snapshot

- VoidlineGameManager.cs: scene-saved boost multiplier is now **2.1×** with **4.6** blend response; boost ignition uses a three-stage unscaled-time envelope.
- VoidlineCameraEffects.cs: boost FOV ramp **+25°**, dolly-in **0.92 world units**, stronger speed-linked rumble, a sharp focus-squeeze/release pulse and a separate recoil/pitch impulse.
- VoidlineSpeedEffects.cs: camera-relative radial streak mesh that expands outward during the rush; streak opacity **0.65**; URP low-quality CameraAndObjects motion blur intensity **0.38**; boost-linked vignette **0.05 → 0.28**, bloom, brief chromatic pulse and restrained desaturation/contrast grade. No full-screen flash plane is used.
- VoidlineFlightEffects.cs: kept the previously preferred soft particle exhaust. The boost plume is narrower by design: cone spread **5–5.5°**, emission radius **0.032–0.042**, compact particle cross-section, speed **12.5**, lifetime **0.46 s**, and high emission to add force without fanning outward.
- VoidlineGameManager.cs: authored courier scale **0.82**; runtime trigger collider widened with the ship.
- main.unity: asteroid backdrop prefabs are assigned, rocks spawn farther out and more frequently, and the first custom arch is scheduled earlier with a much longer lead distance.

## QA status / next check

An open-space screenshot shows the larger courier, starfield and asteroid-arch passage. A previous manual test attempted to activate boost only after waiting nine seconds; the sampled state was still BoostBlend = 0, speed 12, and FOV 58, so that comparison **did not prove the boost effects were active**. Retest by entering Play Mode and triggering boost within the first second; verify the blend rises, the time scale returns to 1 after roughly 0.325 seconds, FOV expands, and edge effects decay on release.

The Unity command bridge later started timing out, so the latest source edits are not marked as fully runtime-validated. Do not describe this feature as finished until the fresh early-run boost test succeeds.

## References

- GameSpot, *Need for Speed: Most Wanted* review: https://www.gamespot.com/reviews/need-for-speed-most-wanted-review/1900-6140037/
- MWEncyclopedia, visual treatment overview: https://github.com/TsyVM/MWEncyclopedia/blob/main/C51-Render-Pipeline/04-visual-treatment.md
- Unreal Engine, Three Fields Entertainment on the *Burnout* arcade-racing formula: https://www.unrealengine.com/developer-interviews/three-fields-entertainment-explains-how-they-evolved-burnout-arcade-racing-formula-dangerous-driving
- *Need for Speed: Most Wanted* (2005), Speedbreaker overview: https://en.wikipedia.org/wiki/Need_for_Speed:_Most_Wanted_(2005_video_game)
- Forza community thread on sense of speed and chase-camera/FOV choices: https://forums.forza.net/t/speed-sense/807111
