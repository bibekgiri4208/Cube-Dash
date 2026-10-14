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

Stage thresholds and encounter rules live in `Assets/Scripts/CubeDash/LevelProgression.cs`; hazard sizes and pooled visual selection live in `HighwayPresentation.cs`. Readable meshes are required for longitudinal subdivision. The curved Lit variant supports this project's base-color/base-map, metallic, smoothness and emission materials; detailed traffic uses its own normal-mapped curved PBR shader.

## Detailed road blocks and obstacle vans

Barriers are molded, chamfered concrete road blocks with broad color-match paint panels, clipped diagonal reflective markings, metal fasteners, recessed end grips, lifting slots and raised amber warning lamps. Panel vans have sculpted/chamfered bodywork, sloping smoked windscreens, separate side/rear glazing, weather seals, wipers, mirrors, recessed door seams, alloy handles, roof ribs, open wheel arches, round treaded tires, five-spoke rims, a modeled grille, bumpers and separate front/rear lamp lenses. Their fronts still face the approaching runner.

Saved art lives under `Assets/Highway Hazards`; `Assets/Resources/HighwayHazards.asset` connects the meshes and nine material roles to the existing spawner on every map. Six original tileable surface sets provide albedo, normal relief and packed occlusion/gloss/metal masks for paint, cast concrete, rubber, brushed alloy, smoked glass and reflector lenses. `CubeDash/Highway Hazard` bends geometry, tangent-space normals, shadows and depth consistently with the road. Broad paint regions retain the exact source obstacle color, rather than hiding the gameplay cue under a fixed vehicle skin.

Meshes and paint variants are shared/cached across the existing slot pool; recycling adds no GameObjects, colliders or per-obstacle textures. The original barrier (1.8 × 1.15 × 0.8 m) and van (1.6 × 1.85 × 3.4 m) collision bounds, stage thresholds, encounter patterns, coin restoration and box-truck visuals are unchanged. No scene rebuilding is required: **Tools > Cube Dash > Build Detailed Road Blocks and Vans** (`HighwayHazardArtBuilder.ApplyFromCommandLine`) explicitly rebakes only this art while preserving existing asset GUIDs.

Tests: `HighwayProgressionTests` covers deterministic/reachable patterns, recovery, safe lane changes with long vehicles, curve continuity, mesh slicing and shader compilation. `HighwayProgressionPlayTests` covers traffic visuals, coin restoration, recycling, pause and restart. `HighwayHazardArtTests` checks the baked meshes, normal/tangent channels, collision envelopes, PBR texture import settings, paint-color preservation, mesh/material reuse and rendered Jungle gameplay plus front/rear van and barrier close-ups.

The detailed traffic upgrade passed **43/43 Unity editor tests**, including those highway checks, all standalone maps' gameplay/recycling, Country Side consistency and the existing Jungle nature/rendering regressions. Saved scenes, prefabs, Jungle planting and downloaded model packages remain unchanged.
