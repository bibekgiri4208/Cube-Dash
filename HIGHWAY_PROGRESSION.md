# Highway encounters

Open `Assets/Scenes/Level.unity` and press Play. No scene rebuild or authoring menu is needed.

| Distance | Stage | Encounter palette |
| --- | --- | --- |
| 0–179 m | 1 | Warm-up cubes with open rows |
| 180–479 m | 2 | Striped barriers, weaving routes, two-lane gates |
| 480–899 m | 3 | Vans and box trucks mixed with barriers |
| 900–1499 m | 4 | More narrow chicanes and traffic combinations |
| 1500 m onward | 5 | Full difficulty, capped speed, repeating varied encounters |

Each encounter lasts six rows. Every fourth encounter is a three-row recovery stretch. Coin routes never jump directly between the two outside lanes; every previously open lane retains a reachable option. Vehicle length is reflected in swept collision bounds. Flight, shields, magnet, truck smashing and the cosmetic supercar retain their existing rules. There are no mandatory jumping/sliding hazards yet.

Difficulty and speed now ramp by distance, so avoiding coins does not keep the run easy. Coin score and best-score saving are unchanged. The HUD displays the current stage and distance.

## Curves

These are automatic arcade **render-space bends**, not a physical spline or manual steering system. All track surfaces, scenery, obstacles and pickups share the same bend. The nearest six metres remain straight so lane contacts stay accurate. Shadows, depth and normals use the bent geometry too. Long meshes are sliced once at startup; runtime meshes/materials are shared across the segment pool and cleaned up on scene destruction. Saved assets and scenes are not rewritten.

Inspector settings:

- **Track → Highway progression → Road Curve Strength:** 0 disables bends, 1 is the default.
- **Track → Patterned Encounters:** disable to restore the original random cube rows.
- **Game Manager → Distance Based Progression:** disable to restore score-based speed/difficulty.
- The existing **Fixed Seed** reproduces both encounters and the bend phase.

Stage thresholds and encounter rules live in `Assets/Scripts/CubeDash/LevelProgression.cs`; hazard sizes and simple traffic meshes live in `HighwayPresentation.cs`. Readable meshes are required for longitudinal subdivision. The curved Lit variant supports this project's base-color/base-map, metallic, smoothness and emission materials; custom textured materials needing other URP features require extending that shader.

Tests: `HighwayProgressionTests` covers deterministic/reachable patterns, recovery, safe lane changes with long vehicles, curve continuity, mesh slicing and shader compilation. `HighwayProgressionPlayTests` covers traffic visuals, coin restoration, recycling, pause and restart.
