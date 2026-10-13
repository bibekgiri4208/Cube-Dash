# Japanese Country Side

Open **`Assets/Scenes/Country Side.unity`** and press **Play**, or select **Start > Country Side** in the Main Menu. Country Side now uses the same automatic **3, 2, 1** countdown, three-lane highway, road curves, distance progression, camera, themed HUD, controls, power-ups, vehicles, pause, Game Over and retry as the other maps. Both in-game Quit buttons return to Main Menu. The scene remains enabled with its original GUID.

## Spring valley design

- Level's exact 8.4-metre asphalt road, three-lane markings, shoulders and guardrails run through the valley, with weathered cedar field fencing, stone-lined irrigation and arched plank footbridges alongside them. The old custom road is hidden on the two scene route instances so there is no overlapping asphalt.
- Six planted flower fields transition from pink/white foreground blooms to mixed rose/purple, blue and distant pink meadows.
- Ten sakura trees frame the road; green broadleaf/fir woodland, grasses, bushes, rocks, reeds and individual wildflowers come from **`Assets/Idyllic Fantasy Nature/Prefabs`**. These are linked prefab instances with their original meshes, materials, wind and LODs. Demo leaf triggers/particles and decorative colliders are removed from instances only; package assets remain untouched.
- Six original Japanese rural buildings use cream plaster, dark timber framing, cedar verandas, shoji lattice windows and individually modeled curved **kawara** roof tiles/ridge caps. Minka farmhouses and kura storehouses are original Editor-baked mesh/prefab assets, not imported house models. A small roadside shrine area includes a vermilion torii and stone lanterns.
- Four flooded rice paddies have earthen dividers and regular seedling rows. Eleven layered, sculpted hills and soft distant cloud banks close the horizon.
- The other maps' **CubeDash/Horizon Skybox** shader provides drifting clouds and a soft spring-blue sky, with warm sunlight and green-valley bounce lighting. The fog range, camera rendering settings and post-processing profile match Level; the scene uses its own spring palette rather than cycling through unrelated biomes. The old procedural sky, sphere clouds and legacy spring-volume override are not used by the upgraded scene.

## Editing

The scene's **Japanese Countryside - Spring Valley** root contains the retained landscape and woodland plus two saved **Countryside Route Tile** instances. The tiles still link to `Assets/CountrySide/Prefabs/Playable Countryside Route.prefab` and retain the flower fields, village, paddies, sakura and roadside details. Original environment/route/architecture prefabs and materials remain unchanged as editable templates. New sky: `Assets/Maps/Skies/Country Side Sky.mat`.

**Country Side Gameplay** now links to `Assets/CountrySide/Prefabs/Country Side Highway Gameplay.prefab`, built from the latest saved Level rig. It contains the same player, eight encounter/road segments, Game Manager, Canvas, EventSystem, trail, obstacle fragments, bounce light and post-processing volume. The Canvas includes the distance-only ribbon, separate power-up duration bars, countdown, themed pause and Game Over menus, with this scene's own callbacks. The original camera and sun objects are retained with Level's camera/rendering settings and matching spring illumination. Other Level biome roots are removed; only the Japanese countryside is shown. The earlier gameplay prefab remains untouched as an archive.

`CountrySideRunnerScenery` scrolls two pre-authored 284-metre route tiles using `EndlessTrack.Travelled`. The tiles wrap continuously, freeze on pause/game over, and reset on restart. Hills and distant woodland remain the backdrop. Rigid terrain/infrastructure/house meshes follow the same render-space road bend using run-owned presentation meshes/materials. Native nature-prefab roots receive only the corresponding lateral curve offset: their meshes, shader-graph materials, wind data, LODs and shadows are not replaced. Decorative colliders remain removed from instances, leaving the gameplay contact space unchanged. No scenery/encounter GameObjects are generated in play; settled scenery stops receiving transform writes.

**Tools > Cube Dash > Match Country Side to Other Maps** (`CountrySideConsistentBuilder.ApplyFromCommandLine`) upgrades the recognized legacy rig without rebuilding its Japanese artwork. Reapplication preserves manual scene, UI and timing edits. It refuses other scenes, unknown rigs and orphaned upgraded assets. **Make Country Side Playable** and **Design Japanese Country Side Environment** also preserve the already-upgraded scene. Authoring code lives under `Assets/Scripts/CubeDash/Editor/`.

Original camera/light objects and the scene GUID are retained. Only this scene receives the countryside gameplay configuration. No target-device frame-rate claim is made without profiling.

## Validation

The original playable countryside passed **69** gameplay/environment regressions and **6** saved-HUD checks before this consistency upgrade. The upgrade passed **103/103** countryside/six-map/highway/progression/HUD/music regressions and **3/3** GPU consistency checks in an isolated Unity 6000.4.1f1 project.

The upgraded scene is compared against Level's saved player, camera, gameplay settings, complete Canvas/callbacks and road geometry. Native meshes/materials remain identical across a **3,780-metre** curved/recycled run, with pooling, pause/reset/countdown and menu return coverage. Startup, curve, countdown, duration-bar, settled pause and Game Over renders were inspected. Reapplication leaves the scene/GUID/new gameplay prefab/sky byte-for-byte unchanged.

Validated scene/prefab/sky/code transfers were hash-verified. Other maps, Build Settings, old countryside prefabs/materials and imported nature-package assets were preserved. Unrelated Main Menu scenery-position edits made during validation were left untouched. No target-device performance claim is made without profiling.
