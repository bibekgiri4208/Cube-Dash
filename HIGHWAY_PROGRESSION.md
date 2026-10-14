# Highway encounters

Open `Assets/Scenes/Level.unity` and press Play. No scene rebuild or authoring menu is needed.

| Distance | Stage | Encounter palette |
| --- | --- | --- |
| 0–179 m | 1 | Traffic drums, weighted cones and compact barricades with open rows |
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

## Detailed traffic variety and side guards

Barriers are molded, chamfered concrete road blocks with broad color-match paint panels, clipped diagonal reflective markings, metal fasteners, recessed end grips, lifting slots and raised amber warning lamps. Panel vans have sculpted/chamfered bodywork, sloping smoked windscreens, separate side/rear glazing, weather seals, wipers, mirrors, recessed door seams, alloy handles, roof ribs, open wheel arches, round treaded tires, five-spoke rims, a modeled grille, bumpers and separate front/rear lamp lenses. Their fronts still face the approaching runner.

The legacy box-built traffic fallback and visible cube obstacles have been removed. Normal progression now cycles deterministically through **eleven distinct baked models** without consuming gameplay RNG: traffic drum, weighted cone, compact road barricade, concrete road block, ribbed water-filled barrier, folding work-zone barricade, panel van, glazed passenger shuttle, service van with roof-mounted ladder, delivery box truck with ribbed cargo body/locking rear doors, and long-wheelbase delivery van. The three compact props replace every opening-stage cube and every cube in the unpatterned/random mode, retaining the original 1.15 × 1.15 × 1.15 m collision envelope and color cues. `HighwayHazard.Cube` remains only as the serialized name of this compact collision category; it no longer selects a cube mesh. The three larger road blocks share the existing Barrier rules; three vans share Van rules; the two large delivery vehicles share Truck rules. There are no new surprise collision types or jumping/sliding requirements. The player cube is unchanged.

Saved art lives under `Assets/Highway Hazards`; `Assets/Resources/HighwayHazards.asset` connects the meshes and nine material roles to the existing spawner on every map. Seven original tileable surface sets provide albedo, normal relief and packed occlusion/gloss/metal masks for paint, cast concrete, rubber, brushed alloy, smoked glass, reflector lenses and galvanized steel. `CubeDash/Highway Hazard` bends geometry, tangent-space normals, shadows and depth consistently with the road. Broad paint regions retain the source obstacle color, rather than hiding the gameplay cue under a fixed vehicle skin.

Meshes and paint variants are shared/cached across the existing slot pool; all eleven meshes are prewarmed and recycling adds no GameObjects, colliders or per-obstacle textures. The original compact obstacle (1.15 × 1.15 × 1.15 m), barrier (1.8 × 1.15 × 0.8 m), van (1.6 × 1.85 × 3.4 m) and truck (1.7 × 2.25 × 4.6 m) collision bounds, stage thresholds, encounter patterns and coin restoration are unchanged. No scene rebuilding is required: **Tools > Cube Dash > Build Detailed Road Blocks and Vans** (`HighwayHazardArtBuilder.ApplyFromCommandLine`) explicitly rebakes the traffic and guardrail art while preserving existing asset GUIDs.

Side guards now use actual folded, double-skinned **W-beam steel**, rolled edges, channel-section posts, spacer blocks, base plates, bolts and front/back reflector tabs, rather than two uninterrupted square bars. Galvanized albedo/normal/surface textures add restrained zinc mottling and distinct metal highlights. The existing `Assets/3D Models/Environments/Highway Guardrails.asset` and `Assets/Material/Environments/Highway Guardrail.mat` retain their GUIDs and one-material renderer interface: saved scenes automatically inherit the upgrade, segment ends still meet at 0/42 m, and the rails remain outside the playable lanes. `CubeDashHighway` reuses the same authoring path so a future highway rebuild cannot restore the old bars.

Tests: `HighwayProgressionTests` covers deterministic/reachable patterns, recovery, safe lane changes with long vehicles, curve continuity, mesh slicing and shader compilation. `HighwayProgressionPlayTests` covers all eleven variants spawning through normal progression, detailed truck replacement, coin restoration, recycling, pause and restart. `HighwayHazardArtTests` checks every baked mesh, normal/tangent channels, collision envelopes, PBR texture import settings, paint-color preservation, mesh/material reuse, guardrail cross-section/textures and rendered Jungle gameplay plus every obstacle variant and a guardrail close-up. It also checks patterned opening stages and unpatterned/random runs in Level and Jungle for complete cube removal, three compact variants, unchanged colors/colliders and stable object counts.

## Saved Edit-mode obstacle models

The pooled `Assets/Prefab/CubeDash/ObstacleCube.prefab` and `TrackSegment.prefab`, plus Level, City, Jungle, Mountain, Desert, Beach and Country Side, now persist the new compact obstacle meshes/materials instead of showing cube placeholders before Play. Their saved rows use drums, weighted cones and compact barricades; coin-reward slots retain their original gold visuals and visibility. `Assets/Highway Hazards/Saved Previews` holds three local-space meshes centered on the unchanged prefab collider and color-matched PBR paint materials. Runtime encounter selection remains independent and still uses all eleven world-space obstacle models.

Reapply with **Tools > Cube Dash > Apply Saved Obstacle Models (Edit Mode)** (`HighwayObstacleEditorVisuals.ApplyFromCommandLine`). The tool refuses Play mode or unsaved scene edits and changes only obstacle mesh/material references. A full before/after audit matched **48,057 object/component snapshots** after excluding those two permitted fields: transforms, colliders, coin visuals, player, camera, UI, gameplay settings, all unrelated art references and object hierarchies were preserved. Existing scene/prefab GUIDs remain unchanged; Jungle planting, player assets, coin assets and downloaded model packages are untouched.

Saved-model validation covered **58 selected checks** across the broad regression run and focused repeat. The repeat passed **10/10**, including all seven Edit-mode scenes, both prefabs, an actual Edit-mode Jungle render and normal traffic recycling. Variant coverage now uses a fixed seed and includes the initial warm-up rows, avoiding the previous random-seed-dependent assertion. All other broad checks passed, including coin behavior, all standalone maps and Country Side consistency. Installed scene/prefab/art files match the validation project byte-for-byte. Reopen an already-loaded scene to refresh its saved model references; no Play-mode spawner or scene rebuild is needed to see them.

Complete cube-obstacle replacement passed **46/46 Unity editor tests**, including all eleven models spawning through normal progression, both patterned and random-mode cube-removal checks in Level/Jungle, coin restoration, unchanged color/collision envelopes, guardrail checks, all five standalone maps' gameplay/recycling and Country Side consistency. Opening-stage and compact-prop close-ups were reviewed; all installed art and runtime/builder code were hash-verified against the validation project.
