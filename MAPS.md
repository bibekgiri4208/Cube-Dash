# Standalone maps

**Main Menu > Start** now loads a separate scene for every map:

| Card | Scene | Retained Level scenery |
| --- | --- | --- |
| City | `Assets/Scenes/City.unity` | City neighborhoods, parks and flyovers |
| Jungle | `Assets/Scenes/Jungle.unity` | Jungle, river and landmarks |
| Mountain | `Assets/Scenes/Mountain.unity` | Mountains, snow and forests |
| Desert | `Assets/Scenes/Desert.unity` | Dunes, mesas and cacti |
| Beach | `Assets/Scenes/Beach.unity` | Coast, palms and islands |
| Country Side | `Assets/Scenes/Country Side.unity` | Linked Japanese countryside, now with the same highway/UI/sky system |

All scenes are enabled in Build Settings, with Main Menu still first. `Level.unity` is retained as the original mixed-biome runner, not replaced or renamed.

## What stays the same

The five new maps are copies of the saved Level rig, not new gameplay implementations. They retain the same player/collision geometry, camera, three-lane road, shoulders, guardrails, markings, road curves, pooled encounters, coins, power-ups, vehicle presentations, controls and sounds. Difficulty still increases with travelled distance through the same five stages; choosing Mountain or Beach does not start at a later difficulty or reset progression every scenery loop.

All six maps now use the Main Menu-themed Level HUD, compact distance ribbon, independent left-side power-up duration cards, countdown, pause, Game Over, retry and return-to-Main-Menu Quit behavior. Country Side's Japanese nature/architecture remain its distinct environment; see [COUNTRY_SIDE.md](COUNTRY_SIDE.md).

## What is separated

Each new scene retains only its chosen biome roots and ambience sources. Shared prefab/material/mesh assets are not modified. Track prefab instances are unpacked only within the new scenes so unused scenery can be removed without editing the original track prefab; nested player, pickup and retained environment prefabs remain linked where originally linked.

`EnvironmentDirector` opts into a saved fixed biome only in these five scenes. `EndlessTrack` retains its normal section indices and gameplay progression, but passes that chosen biome to scenery recycling. Lighting/sky/fog and ambience use that same biome at every distance. Saved map sky materials under `Assets/Maps/Skies` also give each scene its appropriate atmosphere before Play.

Scenery retains its varied interior layouts and reveal animations. Cross-biome edge dressing, fading terrain/river ends, City entrance/exit flyover ramps and Beach-to-City headlands no longer repeat every eight sections inside a standalone map. Main roads are unchanged. The original Level still uses all those transitions normally.

## Editing and safe reapplication

Open the individual scene to edit its layout. **Tools > Cube Dash > Separate Level into Five Maps** (`StandaloneMapScenes.ApplyFromCommandLine`) creates missing maps from Level, adds build entries and refreshes the six menu cards. It preserves already-separated scenes and their manual edits; it refuses to overwrite an unrecognized existing scene or orphaned sky asset. It copies Level before editing, so even interrupted authoring does not modify the original Level.

**Tools > Cube Dash > Add Main Menu Map Selection** refreshes the saved card targets, PLAY badges and navigation without rebuilding the submenu. Navigation follows the displayed three-column desktop/two-column portrait grid. Back/Escape/gamepad Back restore Start focus; selecting a map stops menu music, and in-game Quit returns to Main Menu.

## Jungle nature dressing

`Jungle.unity` uses five Idyllic Fantasy Nature broadleaf tree shapes, six bush shapes, three grass shapes and the added Sticks-and-stones rock models in all eight pooled layouts. Each section has 20 trees, 52 shrubs, 174 grass clumps and ten irregular rocks: broken roadside verges, clustered understory, hillside groves and planting on both riverbanks. Higher leaf-texture contrast, tighter directional lighting and stronger contact shadows restore canopy definition; a restrained two-sided ambient floor keeps shaded leaves green instead of black. The old round/slab boulders, separate green moss caps, banana plant and umbrella-shaped fan palms are removed. Bamboo has seven individually grounded, gently leaning culms, raised nodes, fine side branches and narrow folded lance leaves instead of oversized palm fronds. The original deep-green palette, hills, river, remaining landmarks, lighting and sky remain intact; roads, guardrails, gameplay and HUD are unchanged.

Scene-specific meshes and materials live under `Assets/Maps/Jungle Nature`. The pack's middle-LOD meshes are combined by material instead of adding hundreds of runtime GameObjects. Textured cutout foliage supports gentle wind, shadows, depth/ambient occlusion, road bending and the existing scenery-rise animation. Downloaded pack assets, the shared Jungle prefab, mixed-biome Level and other maps are untouched.

**Tools > Cube Dash > Beautify Jungle with Idyllic Nature** (`JungleNatureBuilder.ApplyFromCommandLine`) authors a previously unmodified standalone Jungle; reapplying preserves saved planting edits. It refuses partial upgrades rather than overwriting them. **Refine Jungle Leaves Rocks and Understory** (`JungleNatureBuilder.RefineFromCommandLine`) explicitly rebuilds these scene-specific assets while preserving their GUIDs. `JungleNatureTests` checks the saved palette/textures, soft leaf lighting, rock replacement, removal of moss caps/banana geometry, hill grounding, road/river clearance, retained terrain, pooled recycling, reapplication and rendered gameplay/river/curved-road views.

**Tools > Cube Dash > Fix Jungle Bamboo and Ground Stones** (`JungleSceneryRepair.ApplyFromCommandLine`) repairs the saved scene-local layouts without regenerating the trees, bushes or grass; the refinement tool also runs this repair. Rocks use all nine prefabs under `Assets/3D Models/Sticks-and-stones/source`, with their imported root orientation/unit conversion retained and demonstration-scene offsets removed. Placement checks low trunk/root footprints, other rocks, bamboo, the fallen log and section seams. Each rock's lower skirt is embedded in the actual hill triangles, rejecting steep footprints instead of balancing a rock on its pivot. The pack's AO texture is tinted in three restrained grey-green variants derived from the existing Jungle stone palette, darkened to compensate for the nearly-white AO map. Bamboo/rock geometry stays combined in the existing scenery pool, with no extra runtime objects or colliders. Source packs, shared biome assets and other maps remain unchanged.

The bamboo/stone repair was audited against the previous saved layouts: every unrelated terrain/plant vertex, normal, texture UV, wind value, reveal anchor and vertex color is unchanged, as are saved log/gateway geometry and existing mesh GUIDs. **40/40 Unity tests passed**, covering all eight layouts' rock skirts and tree clearance, bamboo grounding/nodes/leaf size, palm removal, palette, road/river clearance and rendering (including bamboo and stone close-ups), plus standalone-map gameplay, environment variety and scenery-rise regressions.

Foliage is deliberately rainforest-dark rather than neon: tree, bush and grass materials retain the original green hues with 20–24% lower saturation. Color value is reduced by 28% for trees, 40% for bushes and 42% for grass relative to the original jungle palette. Leaf illumination is capped before applying a 0.76 tree/bush or 0.72 grass light gain, so strong sunlight cannot wash the colors into pale mint. Grass has no extra brightness boost. **Tools > Cube Dash > Tone Down Jungle Greens** updates these seven scene-specific materials, retaining leaf detail, the corrected bush shading and all authored geometry.

Bushes have their own thin-leaf lighting profile: opposing card normals receive equivalent two-sided light, texture contrast is restrained, and self-shadow/contact-darkening cannot produce the pale-green/black checker pattern. Trees, grass, bark and stone retain their existing shading profiles. Validation includes a close-up bush render and a GPU check comparing opposite leaf normals, in addition to the gameplay/river/curved-road views.

Bushes also use two distinct greens: the existing deep rainforest base and a richer, slightly warmer upper-leaf green (78% of the original tip color value, still below the original brightness). Their baked per-plant height spans the full tint blend, with the leaf texture retaining darker green detail among the upper leaves, instead of leaving the two colors almost indistinguishable. **Tools > Cube Dash > Apply Two-Tone Jungle Bushes** updates only the three bush materials; rebuilding or toning down foliage retains this palette. The rendered leaf probe checks both opposing-normal consistency and visible upper/lower color separation, without adding another material slot or draw call.

## Original standalone-map validation

Passed **98/98** map/environment/ambience/road/progression/HUD/menu/music regressions and **2/2** GPU integration checks in an isolated Unity 6000.4.1f1 project. Rendered previews of all five maps and landscape/portrait map selection were inspected.

Saved-scene tests compare gameplay settings/references, player and vehicle hierarchy, camera, complete HUD hierarchy/callbacks, main road/Plane overrides, shoulders/guardrails/markings, coin/hazard colliders and power-up layouts against Level. Each new scene has one retained biome and a matching enabled build GUID. A seeded **3,780-metre** simulation of every map matches the original Level's hazard, coin, power-up and road-curve states, keeps the selected biome through recycling, and verifies pause/reset/countdown and menu return. Existing mixed-biome transitions and ambience tests still pass.

Reapplying the creation tool preserves scene/GUID/sky files byte-for-byte. During the initial five-map separation, the Main Menu audit changed only 15 existing UI documents, adding/removing none and preserving camera/scenery/artwork; original Level, the then-current Country Side and shared prefab/material assets were hash-verified unchanged. Scene/metas, map skies, menu, Build Settings and code were hash-verified against the tested assets before application; unrelated project settings were not transferred. Country Side's subsequent consistency upgrade and validation are documented in [COUNTRY_SIDE.md](COUNTRY_SIDE.md).
