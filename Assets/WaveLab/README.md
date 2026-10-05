# Shoreline Wave Lab

Open `Assets/WaveLab/Scenes/ShorelineWaveLab.unity`, then press Play. The scene is independent of Chess and is not registered as the project's startup/build scene.

## What to inspect

- Incoming breaker travels from the horizon to shore, thickens, then dissipates.
- The swash advances farther onto the sand, pauses briefly, and recedes more slowly.
- A separate foam field stretches, breaks up, and dissolves behind the leading edge.
- Objects transition gradually from fully submerged offshore, through partially exposed with contact foam, to fully exposed near the beach. This state follows their position on the fixed beach slope; a passing wave does not cycle them through immersion states.
- Each object has a complete underwater base and a softly blended exposed pass. A broad, curved, irregular transition avoids a straight alpha-cut seam. Water/foam render between the passes. Fully submerged rocks do not occlude surface foam.
- The incoming breaker and advancing swash push objects shoreward in retained steps, with small individual left/right drift. They settle while the water recedes. There is no offshore current, return spring, or independent vertical bobbing.
- Wet-sand memory remains briefly beyond the receding waterline.

This is an interactive reconstruction of the supplied reference, not a video background. All visual assets in this test are generated meshes or procedural shader patterns. No source-video frames are used in the scene.

## Controls

The config panel starts **closed**. Click **CONFIG** or press **F1** to toggle it; Escape closes it. Drag its header to move it. Only the visible panel intercepts pointer input, so props remain draggable in uncovered gameplay. The compact bottom bar retains Play/Pause, Restart, 0.5x/1x, and the cycle scrubber. Space pauses; R restarts. Keyboard gameplay shortcuts are ignored while editing a number.

Five scrollable tabs contain live sliders with editable numeric fields:

- **Wave:** period, playback speed, reach, relative approach/advance/hold/recede/rest times, breaker thickness and roughness.
- **Drift:** overall force, breaker/swash push, lateral drift, acceleration, settling, obstacle/object steering and rotation.
- **Depth:** beach slope, relative object height, waterline softness/curve/roughness, underwater tint and opacity.
- **Foam:** surface amount, shoreline width, pattern scale, flow, fade phase, sprite contact amount/width/breakup/speed.
- **View:** six layer/motion toggles, water/veil opacity, reflections, wet-sand trail and camera view size.

Values apply immediately, including while paused. **Reset tab** restores just that category; **Default** restores the reference tuning. **Save/Load** round-trips a versioned JSON preset at `Application.persistentDataPath/WaveLab/feel-config.json`. Save is explicit; stopping Play does not automatically save tweaks. **Restart** resets the object layout and clock while preserving tuning. Phase-duration multipliers share the chosen total wave period, and CPU transport and GPU waves use the same remapped phase.

`Beach Slope` maps distance beyond the resting shoreline to water depth. Each `ShoreObjectState` compares it against the unrotated mesh height and its `Height Scale`. The state changes gradually as the object advances toward shore. The upper pass blends through a curved, noisy meniscus over the complete underwater base. Contact foam peaks around half immersion and disappears at both endpoints. `Object Foam Amount` adjusts it independently. The `DEPTH` button makes comparison with the original whole-object tint possible.

Contact foam uses a shared **baked silhouette SDF atlas**, with 128x128 texels per unique shape. Identical silhouettes reuse a tile. The bake ignores translucent shadows and runs only in the Editor. At runtime the foam shader samples the atlas once and intersects the silhouette with the same water-height profile used by the object shader. The thin contact rim therefore follows scallops, arms, gaps, rotation, and the current immersion level. There is no ellipse ring, per-frame texture bake, or CPU contour extraction. The current mesh art supplies its visible alpha silhouette; sprite textures can supply their alpha through the same mask/SDF pipeline. Depth stays continuous because a scalar depth calculation is inexpensive; the baked silhouette handles the expensive shape work.

The reference preset starts at a 9.4-second period and a 2.45-unit run-up. These are tunable approximations from the provided recording, not a measured 99% similarity score.

## Code map

| File | Responsibility |
| --- | --- |
| `Scripts/SurfMath.cs` | CPU shoreline, phase envelope, and current field |
| `Shaders/SurfCommon.cginc` | Matching GPU phase/shore function, noise and cellular foam primitives |
| `Shaders/SurfLayers.shader` | Sand/sky, water, veil, foam, breaker and wet-sand passes |
| `Shaders/BeachObjects.shader` | Underwater base, soft emergence mask, tint and attenuation |
| `Scripts/ShoreObjectState.cs` | Per-object depth, continuous immersion, waterline and contact foam placement |
| `Shaders/ObjectContactFoam.shader` | Thin SDF contour at the sprite/water intersection |
| `Shaders/ObjectWater.cginc` | Shared water-height profile for exposed art and contact foam |
| `Editor/FoamSilhouetteBaker.cs` | Alpha silhouette rasterization, SDF bake, deduplication, R8 atlas |
| `Scripts/WaveLabController.cs` | Fixed-step simulation, layer state, deterministic seek, picking and drag |
| `Scripts/BeachArt.cs` | Procedural fish, starfish, shell, coral, rock and distant-coast meshes |
| `Scripts/WaveLabHUD.cs` | Runtime test controls |
| `Scripts/WaveLabConfigPanel.cs` | Collapsible, draggable live controls, tabs, scrolling and numeric input |
| `Scripts/WaveLabFeelConfig.cs` | Parameter bindings, defaults, validated JSON save/load |
| `Editor/WaveLabSceneBuilder.cs` | Unity API scene construction, asset persistence, phase captures and validation |
| `../../AgentScripts/WaveLabActions.cs` (project root) | CLI validation and recording entry points |
| `../../AgentScripts/FoamContourActions.cs` (project root) | Atlas upgrade, nine-view GPU contact-boundary check and depth contact sheet |

One normalized phase drives everything:

- 0.00–0.26: breaker approach.
- 0.26–0.47: swash advance.
- 0.47–0.54: peak run-up.
- 0.54–0.96: backwash.
- 0.96–1.00: settling.

If changing the shoreline formula, update both `SurfMath.Shore` and `shore` in `SurfCommon.cginc`. Small high-frequency edge ripples are visual only. Simulation uses a 1/60-second step and a seeded initial layout; scrubbing rebuilds the trajectory rather than teleporting every prop onto a sine wave.

## Implementation boundaries

The wave uses layered transparent quads and a procedural breaking-crest profile, not a Navier–Stokes fluid solver. Rocks provide approximate elliptical obstruction and props use gentle contact separation. The artwork, exact turbulence, and asset-specific interactions differ from the reference. The scene demonstrates the technology and exposes tuning controls; numerical or perceptual 99% equivalence has not been established.

The current scene prioritizes iteration and visual clarity. Profile transparent overdraw and procedural noise on target mobile hardware before production. Foam can be baked to tiled textures without changing the simulation contract. No additional rendering pipeline is installed or substituted.

## Runtime creation and Android APK

`Rebuild()` uses `Resources/WaveLab/ImmersionCatalog.asset`, which references the existing baked atlas, materials and foam quad. **Wave Lab > Upgrade Object Immersion** refreshes that catalog when artwork changes. Runtime creation performs no silhouette bake.

Dragging supports mouse and touch. A drag retains its initiating finger and UI blocks new picks. The cycle scrubber coalesces requests, processes at most eight simulation steps or approximately three milliseconds per frame, and reuses half-second checkpoints. Physics tuning changes invalidate the cached trajectory. `Seek()` remains synchronous for captures and validation.

With Unity **6000.6.0f1**, an activated Editor license and Android Build Support (including SDK, NDK and OpenJDK), run:

```bash
Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter WaveLab.Tests -testResults /tmp/wavelab-tests.xml -logFile /tmp/wavelab-tests.log
Unity -batchmode -nographics -quit -projectPath "$PWD" -buildTarget Android -executeMethod WaveLab.EditorTools.WaveLabBuild.BuildAndroid -waveLabOutput Build/WaveLab.apk -logFile /tmp/wavelab-build.log
```

`Unity` here is the actual Editor executable (use its absolute path if it is not on `PATH`). The build entry creates a debug-signed ARM64 APK that starts the WaveLab scene. It uses a separate application ID, `com.cuoit.wavelab`. Release signing is not configured by this entry point.

## Repeatable checks

Run inside the project with the Unity Editor open and Pipeline connected:

```powershell
unity command run_script --file AgentScripts/WaveLabActions.cs --entry WaveLabActions.Verify --caller plugin --skill unity-cli
unity command run_script --file AgentScripts/WaveLabActions.cs --entry WaveLabActions.VerifyImmersion --caller plugin --skill unity-cli
unity command run_script --file AgentScripts/WaveLabActions.cs --entry WaveLabActions.VerifyShorewardMotion --caller plugin --skill unity-cli
unity command run_script --file AgentScripts/FoamContourActions.cs --entry FoamContourActions.CaptureAndValidate --caller plugin --skill unity-cli
unity command run_script --file AgentScripts/PanelActions.cs --entry PanelActions.Verify --caller plugin --skill unity-cli
unity command run_script --file AgentScripts/WaveLabActions.cs --entry WaveLabActions.CapturePhases --caller plugin --skill unity-cli
```

`VerifyImmersion` checks all 93 objects, all three states, foam gating, layer ordering, and shore-based depth. `VerifyShorewardMotion` checks three waves frame by frame: no offshore steps, no late-backwash drift, persistent forward progress, both lateral directions, gradual immersion, and invariant depth for stationary objects. In Play Mode, `WaveLabActions.VerifyRuntime` also checks controls, a single EventSystem, and six simulated wave cycles. `WaveLabActions.Record` captures a deterministic 20-second sequence under `Screenshots/WaveLab/Frames-refined`. The Editor menu **Wave Lab > Create or Open Wave Test** opens the scene. **Wave Lab > Upgrade Object Immersion** installs the state components on an older version of this scene using Unity APIs.
