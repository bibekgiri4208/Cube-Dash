# Highway encounters

Open `Assets/Scenes/Level.unity` and press Play. No scene rebuild or authoring menu is needed.

| Distance | Stage | Encounter palette |
| --- | --- | --- |
| 0–119 m | 1 | Traffic drums, weighted cones and compact barricades with open rows |
| 120–319 m | 2 | Striped barriers, weaving routes, two-lane gates |
| 320–649 m | 3 | Vans and box trucks mixed with barriers |
| 650–1099 m | 4 | More narrow chicanes and traffic combinations |
| 1100 m onward | 5 | Full difficulty, capped speed, varied steering encounters |

After the introductory phrases, encounters last four to six rows. Every fourth encounter is a two-row recovery stretch. Slaloms, gates and traffic phrases avoid immediate repeats; later gates/traffic change lanes more often and narrow more frequently. When the marked route moves, a hazard blocks the old route instead of letting players ignore steering and only miss coins. Coin routes never jump directly between the two outside lanes; every previously open lane retains a reachable option. Vehicle length is reflected in swept collision bounds. Flight, shields, magnet, truck smashing and the cosmetic supercar retain their existing rules. There are no mandatory jumping/sliding hazards yet.

Difficulty and speed ramp by distance, reaching full difficulty at 1100 m rather than 1500 m, so avoiding coins does not keep the run easy. Starting speed (12 m/s), maximum speed (30 m/s), acceleration and 14 m row spacing are unchanged. Coin score and best-score saving are unchanged. The HUD displays distance.

## Power-up pacing

All maps use `PowerUpSpawns.cs`, a seeded scheduler working in absolute run distance, not scene coordinates or frame time:

- No bonus before 56 m; at least 56 m between any two bonuses. The track's existing bonus chance still applies to eligible rows; setting it to zero disables all bonuses.
- Truck and Flight cannot spawn before 350 m. They share a **750 m minimum gap**, including Truck-to-Flight and Flight-to-Truck. At the 30 m/s speed cap this is at least 25 seconds between their locations, leaving ground gameplay after the existing 10-second ability and 3-second exit shield.
- Eligible type weights are Shield 28, Double Points 32, Flight 8, Truck 8, Magnet 24. During the shared vehicle cooldown, only ordinary bonuses are selected; cooldowns reserve spawn locations even if a pickup is missed.
- Mid-row bonuses use lanes open in both adjacent obstacle rows, within one lane of the incoming marked route. A transition with no suitable lane simply has no bonus; pickups never ask players to dash through the previous vehicle's tail.
- Retry resets scheduling; fixed seeds reproduce it. Forward motion, frame splitting and world-origin shifts preserve spacing. Bonus selection/placement has its own RNG, separate from obstacle and coin generation.

Existing power-up durations, exit shields, scoring and collision envelopes are unchanged. The shared pickup/track pools are reused, with no new objects during recycling. Obstacle art cycles separately per category so the shorter opening still shows every compact prop without consuming gameplay RNG. No scene or prefab rebuild is needed.

Balance validation: **94/94 Unity checks passed**. Coverage includes 100 seeded spawn simulations, weighted rarity/shared cooldowns, all six maps through recycling and retry, disabled/partial bonus sets, actual forward travel with frame splitting and rebasing, deterministic/reachable patterns, later lane pressure/recovery, top-speed long-vehicle steering at 30/60/240 FPS with a short steering delay, all eleven traffic variants, stable pool sizes, existing abilities/collections, and map/UI regressions.

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

## Model-shaped Truck smash effects

Truck impacts now separate each obstacle's actual geometry instead of rescaling a beveled cube. All eleven variants have their own baked breakup set in `Assets/Highway Hazards/Breakup Parts`, linked by `Assets/Resources/ObstacleBreakups.asset`:

- Drums, cones and water-filled barriers release curved painted plastic/reflector shells with their original rubber and recessed surfaces.
- Compact/work-zone barricades release striped cross-rail sections and metal A-frame/foot assemblies.
- Concrete road blocks break into capped, slanted concrete wedges with their paint, markings and fasteners still attached.
- Vans and delivery vehicles separate into body/cargo panels, four round treaded tires with alloy hubs, and smoked-glass pieces. Service-van ladder/rack geometry stays with its associated roof panel.

Compact props/barriers use eight parts; vehicles use fourteen. Paint alone inherits the obstacle's blue/green tint; rubber, metal, concrete, glass and lamps retain their PBR textures/materials. Wheels spin/bounce longer, glass disperses and disappears sooner, and heavier concrete has a lower toss and weaker bounce. Parts tumble, settle above the road using rotated shape extents, follow the road bend and scroll, then shrink out. Pause and restart retain their existing behavior.

The existing **128-piece pool** is reused. Geometry is baked offline, shared materials/state are prepared before impact, and impacts create no meshes, materials, GameObjects or physics bodies. `RunnerCube.HazardSourceMesh` records the authored variant separately from its local-space presentation mesh; saved compact mesh aliases also resolve correctly. Coin collection, collision envelopes, score, Truck duration, exit shielding and smash audio are unchanged. Saved scenes/prefabs and player/scenery art are not rewritten by this effect upgrade.

Rebake only breakup art with **Tools > Cube Dash > Bake Model-shaped Truck Smash Parts** (`ObstacleBreakupBuilder.ApplyFromCommandLine`); the detailed-traffic baker now also refreshes these sets. Truck authoring uses a model fragment, not a cube, when preparing a new inactive pool.

Validation: **48 selected checks passed across broad/focused Unity runs**, with the final breakup suite passing **11/11**. Coverage includes actual swept Truck contacts for all eleven models in both obstacle colors, all seven saved maps, saved-model alias resolution, original material roles, unchanged hitboxes/scoring, coins, Truck pause/restart/expiry/shields, normal progression/recycling and bounded pool reuse. A warmed 100-impact test confirmed **zero managed allocations** and no new meshes, materials or objects. Before/after renders for all eleven variants were reviewed. All 239 installed art/catalog/metadata files and changed source match the validation project; 244 existing scene/prefab/obstacle-art files remained byte-identical.
