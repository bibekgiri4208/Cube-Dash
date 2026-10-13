# Japanese Country Side

Open **`Assets/Scenes/Country Side.unity`**, press **Play**, then **Space/Enter**, gamepad **A / Cross**, or **START RUN**. You can also select **Start > Country Side** in the Main Menu. Country Side is a playable endless runner using Level's existing player, HUD, controls, encounter generation, scoring, power-ups, vehicle presentations, sounds, pause, game over and retry. It is included as an enabled build scene. The original Level remains available as **Start > City**.

## Spring valley design

- A clear, 8.4-metre-wide rural road runs through the valley, with weathered cedar field fencing, stone-lined irrigation and three arched plank footbridges alongside it.
- Six planted flower fields transition from pink/white foreground blooms to mixed rose/purple, blue and distant pink meadows.
- Ten sakura trees frame the road; green broadleaf/fir woodland, grasses, bushes, rocks, reeds and individual wildflowers come from **`Assets/Idyllic Fantasy Nature/Prefabs`**. These are linked prefab instances with their original meshes, materials, wind and LODs. Demo leaf triggers/particles and decorative colliders are removed from instances only; package assets remain untouched.
- Six original Japanese rural buildings use cream plaster, dark timber framing, cedar verandas, shoji lattice windows and individually modeled curved **kawara** roof tiles/ridge caps. Minka farmhouses and kura storehouses are original Editor-baked mesh/prefab assets, not imported house models. A small roadside shrine area includes a vermilion torii and stone lanterns.
- Four flooded rice paddies have earthen dividers and regular seedling rows. Eleven layered, sculpted hills and soft distant cloud banks close the horizon.
- A warm spring sun, scene-specific sky, distance haze and a restrained ACES/bloom profile tie the village to the nature-pack palette. No depth-of-field blur hides the playable-road corridor.

## Editing

The scene's **Japanese Countryside - Spring Valley** root contains the retained landscape, distant woodland, sky and lighting, plus two saved **Countryside Route Tile** instances. The tiles link to `Assets/CountrySide/Prefabs/Playable Countryside Route.prefab` and contain the road, flower fields, village, paddies, sakura and roadside details. The original environment prefab remains available as a design template. Architecture meshes/materials and the scene lighting profile are under `Assets/CountrySide`.

**Country Side Gameplay** is a linked instance of `Assets/CountrySide/Prefabs/Country Side Gameplay.prefab`, containing the copied Level gameplay rig: Player Cube, Track with eight pooled encounter segments, Game Manager, Canvas, EventSystem, trail and obstacle fragments. Its nested player/vehicle/pickup prefabs remain linked rather than duplicating their particle-system data into the scene. The original Country Side camera is retained and framed like Level. All scene references and button events point to this scene's own objects. Highway road bending and biome transitions are disabled for this scene only; the countryside road, spring lighting and hills are not replaced by Level's city/jungle/desert/beach scenery.

`CountrySideRunnerScenery` scrolls two pre-authored 284-metre route tiles using `EndlessTrack.Travelled`. The tiles wrap continuously, freeze on pause/game over, and return to their authored positions when a run restarts. Hills and distant woodland stay as the backdrop. No scenery or encounter GameObjects are generated during play. Moving scenery is not marked static; nature-pack wind/materials are preserved.

**Tools > Cube Dash > Make Country Side Playable** adds the gameplay rig to the designed scene. Re-running it preserves an already-playable scene and manual tuning rather than duplicating objects. **Design Japanese Country Side Environment** also leaves the playable scene intact. Both tools refuse to author other scenes. Authoring code lives in `Assets/Scripts/CubeDash/Editor/CountrySideMeshes.cs`, `CountrySideSceneBuilder.cs` and `CountrySidePlayableBuilder.cs`.

Original camera/light objects and the scene GUID are retained. Only this scene receives the countryside gameplay configuration. No target-device frame-rate claim is made without profiling.

## Validation

All **69 focused regression tests passed** in an isolated Unity 6000.4.1f1 project, including countryside gameplay/environment checks and the existing player, desktop input, coin, power-up and vehicle suites. The countryside checks cover saved gameplay references, HUD wiring, input actions, encounter pools, a 1,500-metre recycle, tile continuity, pause/resume/restart, coin scoring, all power-up presentations, crashes/retry, scene safety and the retained environment. A final **6/6 gameplay check** also passed with Start and Retry invoked through their saved HUD button callbacks. Runner renders were visually reviewed. The open source Editor was not interrupted; target-device performance still requires profiling.
