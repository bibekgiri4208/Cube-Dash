# Main menu

Open `Assets/Scenes/Main Menu.unity` and press Play. It is the first enabled build scene; **Start** loads the existing `Level` scene.

The reference-inspired scene contains editable 3D parking bays, weathered asphalt, concrete/graffiti wall, warm sconces, layered foliage, neighborhood buildings, overhead wires, a classic sedan, safety props, and a red cube with black eyes. Its low camera and left-hand cut-corner ribbons use the supplied image's composition. This is a recreation, not the image's original models or a pixel-identical reproduction.

- **Shop:** empty **Common**, **Rare**, and **Epic** skin collections. No skins, purchase buttons or equipment actions are added yet.
- **Options:** independent saved master/music volume sliders with live percentage readouts, and a fullscreen toggle in separate Audio and Display sections.
- **Quit:** closes the application, or stops Play mode in the Editor.
- Mouse, keyboard/UI navigation and gamepad are supported. Escape/back closes a panel.
- Closing Shop or Options returns focus to its main-menu ribbon, not Start. Back, Escape and gamepad Back behave the same; Back/Escape with no panel open leaves the current selection unchanged.
- Master and Music Volume show a cream-and-red focus frame when selected with a gamepad, keyboard or pointer. Up/down changes the selected control; left/right adjusts its volume. The frame clears when focus moves away or Options closes, without resizing the slider.
- The Fullscreen checkbox uses the same focus frame, whether checked or unchecked. Gamepad confirm/keyboard submit toggles the tick; moving to Back or closing Options clears its focus frame. Its display-mode callback and checkbox/tick graphics are unchanged.

## Player preview and idle

The menu mascot is `Assets/Prefab/CubeDash/PlayerCubeMenuPreview.prefab`, a **variant of the actual PlayerCube prefab**, not an independently modeled cube. It inherits the player's beveled mesh, material and two black rectangular eyes. The variant removes the collider, gameplay/power-up components and vehicle/effect models; its only script is mesh presentation. Keyboard/gamepad input continues to navigate the UI but cannot steer or transform this preview.

`PlayerCubeVisual` gives the menu preview a grounded, volume-preserving jelly squash/rebound with a small secondary jiggle, now about 19% faster. In the **Main Menu only**, the cube occasionally crouches, hops into a full backflip and lands with a soft squash (first at six seconds, repeating every 8.5 seconds). Gameplay's Ready state uses only the faster jelly idle, never the backflip. Only the visual child animates: the player's collider, root scale and movement rules are unchanged. Starting a run restores its normal scale; pause freezes animation. Eyes follow the cube's visibility and disappear during plane/truck/supercar presentation.

Both the preview and gameplay cube blink: the two eye meshes briefly squash vertically, hold closed, and smoothly reopen over 0.18 seconds, first after 3.1 seconds and then every 4.3 seconds. Eye centers/width/depth, body pose, materials and colliders stay unchanged. Gameplay owns the blink clock so pause freezes it; disabling the presentation restores open eyes. Timing is deterministic and does not consume the obstacle/coin random stream. The saved Level turns only its **Cube Visual** child 180 degrees to face down the `+Z` road; the menu's camera-facing mascot, player root/collider and vehicle models are not rotated.

`PlayerBlinkAndFacingTests` covers the smooth blink curve, synchronized eye-only scaling, unchanged body/collider geometry, renderer visibility, disable restoration, the saved Level-only facing override, automatic menu blinking, gameplay pause/resume, right-lane banking and restart facing. The restart bounds assertion synchronizes physics after the player's teleport before comparing world-space collider bounds.

`Tools > Cube Dash > Update Player Eyes and Menu Preview` updates the player prefab and replaces only the old mascot, preserving the menu camera, environment, skybox and lighting/profile edits. It does not regenerate `Level.unity` or the menu environment.

## Editing

### Japanese garden foliage

The garden uses the downloaded tree, leafy bush and grass prefabs described below. Fallen petals and a low stone-edged planting bed finish the street-side tree. Original procedural Japanese pines, sakura, rounded azaleas and grasses remain saved with hidden renderers as recoverable backups. No gameplay vegetation is changed.

Editable, reusable prefabs, saved meshes and matte materials are under `Assets/MainMenu/JapaneseGarden`. In the scene, edit **Cube Dash Main Menu > Parking Lot Diorama > Japanese Garden Foliage**. The models use shared combined meshes and do not generate geometry, run scripts, or add colliders at runtime.

`Tools > Cube Dash > Improve Main Menu Japanese Foliage` upgrades only the original foliage and adds the sakura planting. Original trees, shrubs and weeds remain saved but inactive, so their edits can be recovered. The parking layout, cars/props, graffiti, player/backflip, camera, UI, skybox and lighting are preserved. Re-running the tool preserves an already-authored garden rather than rebuilding over manual edits. The full reference-menu builder includes the garden when regenerating a menu from scratch.

### Idyllic Fantasy Nature trees

The original imported tree arrangement used three distinct `BlossomTree` variants for the sakura, seven green `BroadleafTree` instances and four `Fir` instances from `Assets/Idyllic Fantasy Nature/Prefabs`. These linked instances retain the package's textures, trunk materials and three-level LODs; blossom shading uses the menu-only material below. Trees can be moved, added or removed when editing the layout. Only the old procedural tree renderers are hidden, preserving their planter children. Fallen petals and the rest of the parking lot are unchanged.

Edit **Japanese Garden Foliage > Idyllic Fantasy Nature Trees** in the scene. `Tools > Cube Dash > Use Idyllic Fantasy Nature Menu Trees` performs this tree-only replacement and preserves an already-authored replacement on subsequent runs. Source prefab, model and material assets are not edited; capsule colliders are removed through scene-instance overrides, and static batching is disabled for shader-driven foliage. No demo controllers are added. Full menu regeneration uses the imported tree package when it is installed.

Compatibility with URP 17: the package's `Shader/Vegetation.shadergraph` declares `_MAIN_LIGHT_SHADOWS` and `_MAIN_LIGHT_SHADOWS_CASCADE` as **Predefined** keywords rather than declaring them again. URP already supplies these keywords. This removes duplicate-declaration shader errors without changing textures, material colors, graph lighting/wind logic, pipeline assets, color space or post-processing.

### Brighter menu sakura blossoms

`Assets/MainMenu/Sakura/Menu Sakura Blossoms.mat` and its saved shader graph tune only the pink blossom slots on active imported sakura trees in the menu, including manually placed trees outside the generated group. The material keeps the package's blossom texture, alpha clipping and wind settings. HDR **Top Color** and **Bottom Color** use richer rose-pink tints; **Menu Blossom Glow** (default `0.95`) balances the existing textured emission without washing out petals. The menu shader smoothly blends shadow gradients and moderately lifts their darkest values, retaining canopy contrast and petal detail. Trunk materials, shadow casting, mesh geometry, positions, LODs, other foliage and the global Volume profile are unchanged.

`Tools > Cube Dash > Brighten Main Menu Sakura Blossoms` creates these menu-only assets if missing and assigns them only where the original package blossom material is still used. Subsequent runs preserve material/shader edits and unrelated custom overrides. Full menu regeneration includes this tuning. The downloaded material and shader are not modified by this tool. Adjust the menu material's HDR colors or **Menu Blossom Glow** for further visual tuning rather than raising global bloom intensity.

### Idyllic Fantasy Nature bushes and grass

The 51 round azalea meshes are replaced with six leafy `Bush` variants. The 64 susuki, roadside and sakura-planter grass tufts are replaced with the three `Grass` variants. Linked prefab instances keep the package materials and bush LODs, at the old plant anchors with uniform scaling fitted to the previous footprints. Old plant renderers are hidden without deleting their objects or user-added children.

Edit **Japanese Garden Foliage > Idyllic Fantasy Nature Bushes and Grass**. `Tools > Cube Dash > Use Idyllic Fantasy Nature Menu Bushes and Grass` replaces only these plants and preserves an existing replacement on subsequent runs. The package's bush demo `LeafTrigger` subtree (interaction script, collider and leaf particles) is removed only from scene instances, not source prefabs. Static batching is disabled for shader-driven foliage. The tree instances, planters, petals, camera, lighting, UI, vehicles, hero and gameplay remain unchanged. Full menu regeneration includes both imported tree and ground-cover replacements.

Environment and canvas objects are saved in the scene under `Cube Dash Main Menu`. Camera and sun are separate scene objects. Materials, mesh assets, shader and licensed fonts are in `Assets/MainMenu`.

`Tools > Cube Dash > Build Reference Main Menu` regenerates **only the generated menu root**, resets the menu camera/sun/environment settings, and sets build order. It preserves unrelated root objects and does not rebuild `Level`. Save scene edits before running it. Regeneration replaces manual edits within the generated root.

`MainMenuSceneBuilder` holds the camera, prop and ribbon layout. `MainMenuController` holds navigation and panel behavior. The menu does not use gameplay's road-deformation shader.

### Smoother UI edges and icons

The four ribbons and their vector icons use small, screen-pixel-scaled alpha fringes to smooth edges even on the overlay canvas, where camera anti-aliasing does not apply. Icons have refined curves, round-capped strokes, an outlined shopping basket and a smoother eight-tooth gear with a clean center hole. The Start button's diagonal bands are clipped against an inset of its inner polygon, including their antialias fringes, so they cannot cross the border at different button sizes.

Ribbon labels use `MenuLabelText`, a compatible `Text` subclass that samples the existing dynamic Anton font at twice the resolution and maps the vertices back to the original layout. Font size, text, colors, alignment, transforms, button actions and navigation remain unchanged. `Tools > Cube Dash > Improve Main Menu UI Edge Quality` upgrades existing ribbon labels without rebuilding the environment; full menu regeneration includes the sharper labels. The graphical edge/stripe fixes apply automatically through `MenuRibbonGraphic` and `MenuGlyphGraphic`, with no new textures, world shaders or post-processing settings.

### Gentle menu camera sway

The menu's **Main Camera > Main Menu Camera Sway** component adds slow, camera-relative motion around its authored pose: up to `0.06 m` sideways and `0.035 m` vertically, with `0.16°` pitch, `0.28°` yaw and `0.06°` roll. The base period is ten seconds, with slightly different axis speeds and a two-second fade-in. Adjust the amplitudes and period in the Inspector for a quieter or stronger effect.

Sway runs only in Play mode in a scene containing the menu controller, uses unscaled time, never accumulates positional drift and restores the original pose when disabled. It does not change field of view, animate the overlay UI or affect the gameplay camera. `Tools > Cube Dash > Add Gentle Main Menu Camera Sway` adds just this component without rebuilding the environment or resetting the camera/lighting; subsequent runs preserve existing sway settings. Full menu regeneration includes it.

### Shop and Options sections

`Tools > Cube Dash > Improve Main Menu Shop and Options` authors only the two modal panels. The saved panels match the main buttons with pink cut-corner cards, heavy near-black outlines and offset shadows, ribbon headers/rarity labels, Anton headings, and red primary-ribbon Back buttons. Start still loads `Level` directly and Quit keeps its existing behavior. The previous color-preview shop and options panel are retained as inactive backups. Re-running the section tool preserves already-authored sections and manual edits; full menu regeneration includes the themed panels.

`Tools > Cube Dash > Match Shop and Options to Menu Ribbons` applies the visual theme in place with Undo, without rebuilding scenery, replacing controls, changing callbacks/navigation/preferences/music, or deleting skin-grid content. Reapplying does not duplicate decorations. Rarity emblems keep their distinct colors.

Shop/Options instruction paragraphs and helper notes are hidden: panel descriptions, empty-collection explanations, availability/settings notes, and the fullscreen explanation. They remain inactive editable text objects; headings, rarity labels, "NO SKINS YET" status, percentages and controls remain visible. New panel generation also keeps this helper copy inactive.

Edit **Cube Dash Main Menu > Reference Menu Canvas > Shop - Skin Collections > Card** for the three rarity cards. Each has an explicitly empty state and an empty **Skin viewport > Skin slots** grid, with no invented skin products, prices or unlocks. Later, add actual skin UI to those grids and remove or hide the corresponding **Empty state**; no skin inventory, purchase system or equipment behavior is implemented yet.

**Options - Audio and Display > Card** holds outlined master/music volume sliders, dark live percentage badges and a cut-corner fullscreen checkbox. Square fill caps span the entire inner track width, removing the pale leading gap at partial/full volume; the unfilled rail at partial volume is intentional. Values remain clamped and saved through independent preferences. Back, Escape and gamepad Back close either panel; Options has an explicit keyboard/gamepad navigation loop through Back, master volume, music volume and fullscreen. Each full slider row is clickable and the modal backdrop blocks pointer input from reaching the main ribbons.

### Main menu background music

`Assets/Audio/Music/Main Menu Bg.mp3` loops on the menu-only **Menu Background Music** AudioSource. Playback starts after saved settings are restored, remains uninterrupted while Shop/Options are open or music is muted, and stops when Start loads gameplay. No persistent audio object or gameplay soundtrack changes are introduced.

Options > Audio has separate **Master Volume** and **Music Volume** sliders. Master controls the AudioListener; Music controls only this AudioSource, with the independent `CubeDash.MusicVolume` preference. Both include live percentages and keyboard/gamepad navigation. The music setting does not change gameplay sound-effect volume. `Tools > Cube Dash > Add Main Menu Music and Volume Control` upgrades an older menu without rebuilding scenery or overwriting an existing music row/source. Full menu regeneration includes the track when present.

## Post-processing

Select **Cube Dash Main Menu > Menu daylight grading** in the Hierarchy. Its global Volume uses `Assets/MainMenu/Menu Lighting.asset`; the Main Camera has post-processing enabled and includes the Volume's Default layer. Inspect the result in **Game view**. For Scene view, also enable **Post Processing** in its effects menu.

The menu uses bright, airy arcade daylight: neutral tonemapping, lifted exposure, soft contrast, vibrant colors, broad bloom and no dark vignette. Brighter ambient sky fill and gentler sun shadows keep the shaded faces light. Gentle far-only Gaussian depth of field keeps the red mascot and graffiti wall in focus. The overlay UI is not blurred or color-graded. These settings do not modify the gameplay profile or shared renderers.

`Tools > Cube Dash > Tune Main Menu Post Processing` reapplies the menu profile defaults **without rebuilding or saving the scene**, then selects the profile for editing. Use this rather than the scene builder when only adjusting post-processing.

## Quality-preserving optimization

`Tools > Cube Dash > Optimize Main Menu Without Quality Changes` marks eligible stationary diorama renderers **Batching Static**, keeping their meshes, materials, transforms, lighting and shadows unchanged. It excludes disabled backups, scripted/animated objects and their children, LOD vegetation, property-block renderers and unfamiliar shaders. Existing static flags are preserved. Source prefabs, imported foliage, shared pipelines and `Level` are not edited. Save scene edits and stop Play mode first; the command supports Undo and is safe to repeat.

The same optimization runs automatically on the transient Main Menu scene during player builds, without saving over the source scene. During builds only, if the known menu renderer and shader sources/dependencies pass the scene-color audit, the menu camera's inherited **Opaque Texture** setting is overridden to **Off** to skip an unused color copy. Explicit author overrides, new scene-color shaders, extra cameras or custom renderer features prevent this opt-out. The Editor command leaves the source camera setting untouched, so future shader edits cannot inherit a permanently disabled buffer. Depth textures, ambient occlusion, bloom, depth of field, anti-aliasing, resolution, LOD thresholds, foliage wind and camera sway are not reduced. GPU Resident Drawer/foliage instancing remain unchanged pending target-device profiling.

`ArcadeButton` retains its existing feedback curve, but snaps the imperceptible final animation tail to the exact target scale and stops writing the UI transform while idle. It continues checking interactability and responds normally to pointer/keyboard/gamepad events.

## Validation

Fullscreen feedback validation passed **19 fullscreen/slider/menu/music tests** and **3 rendered fullscreen checks**. Gamepad focus stays visible with the tick both on and off, submit toggles it once, and moving away, disabling the control or closing/reopening Options clears focus correctly. Checked/unchecked previews were inspected; the saved scene adds only the focus frame/component, preserving checkbox/tick geometry, display callback, navigation and artwork. The submit fixture verifies UI behavior without changing the test machine's display mode.

Slider feedback validation passed **16 slider/menu/music tests**, covering gamepad selection and volume adjustment, pointer selection, disabled controls, panel reopening, saved wiring and safe reapplication. Rendered focus previews were checked for both volume rows. The saved scene adds only two focus frames and their feedback components; track/fill/handle geometry, audio callbacks, navigation and background artwork are unchanged. Feedback is also retained when regenerating or reapplying the menu sections, music controls or panel theme.

Panel focus-return validation passed **13 menu/music tests**, including pointer and submit opening, Back/Escape/gamepad Back restoration, direct panel switching, and leaving selection unchanged when no panel is open. The headless input fixture temporarily routes keyboard input to Game view and restores the original input settings afterward. Menu scene artwork, controls, navigation links and music remain unchanged.

Blink/facing validation passed **42 focused tests**, including five new eye/facing checks and existing idle/backflip, desktop-input, plane/truck/supercar and menu-optimization regressions. Instruction-free Shop/Options validation passed **12 menu/music tests**, covering all eight hidden helper labels, preserved functional labels and empty status, panel regeneration, slider geometry/input/navigation, music playback and saved audio settings. Both runs used an isolated Unity project without disturbing the open Editor or concurrent menu scenery edits.

Ribbon-theme/slider validation passed **17 focused tests** in an isolated Unity project. Coverage includes both saved panel themes, live fill-edge alignment at 0/1/42/100 percent on both sliders, unchanged controls/navigation/music preferences, preserved skin-grid contents, repeatable upgrades, and existing menu optimization/render checks. Shop/Options render previews were inspected; scene-document comparison confirms all changed source objects belong to those two panels, leaving the main ribbons, environment, camera, lighting, audio source and gameplay unchanged.

Menu music validation passed **15 focused tests** in an isolated Unity project, covering the assigned looping 2D track, independent master/music controls, saved settings restored before playback, mute/clamping/readouts, pointer/navigation routing, idempotent upgrades, stopping/unloading music for gameplay, and the existing menu optimization/render comparison.

Optimization validation in Unity 6000.4.1f1 passed all **7 optimization tests**, identifying **644** stationary renderers for batching while preserving foliage/hero animation, authored transforms/materials/lighting, camera overrides and gameplay isolation. A deterministic GPU comparison (same camera pose, AO noise and dithering samples) measured mean RGB difference below `0.000001` and maximum channel difference `1/255` after real static batching and skipping the opaque color copy. The wider menu/player regression run passed **53 of 54 tests**; the existing lighting-default test expects non-positive contrast while the saved menu profile uses `+5`. That profile was deliberately not retuned, preserving the current visuals. No target-device FPS improvement is claimed without profiling.

Shop/Options validation passed **45 focused tests** in the temporary Unity project, including empty rarity grids, persistent button/control wiring, recoverable old panels, idempotent panel upgrades, visible headings, slider dimensions, pointer routing, live/clamped/saved volume, navigation and existing UI/camera/player/foliage regressions. The scene comparison changes only the menu controller's panel/control references, adds two canvas children and renames the two inactive backups. All existing objects are retained; environment, main ribbons, camera, lighting, sakura assets and gameplay are preserved.

UI quality validation passed **39 focused tests** in the temporary Unity project. Coverage includes stripe containment at four button sizes, valid antialiased icon meshes, the gear's open center, resolution-scaled fringes, preserved label size/layout, existing menu navigation and camera/player/foliage regressions. An isolated GPU render produced 297 intermediate edge pixels with camera anti-aliasing and post-processing disabled, confirming that the UI contours actually render smoothly. The saved-scene UI upgrade changes only the four label script references, preserving component identities, layout, event wiring and all non-UI scene settings.

Menu camera sway validation passed **26 focused tests** in the temporary Unity project, including bounded/drift-free sampling, no Edit-mode motion, unscaled-time animation, pose restoration, gameplay isolation, existing menu navigation, foliage and player animation. The scene change adds only one component to the existing Main Camera; its authored transform/lens and all other scene objects, lighting and materials remain unchanged.

Menu sakura color refinement passed **23 focused tests** in the temporary Unity project. All four currently placed sakura trees use the menu material across their 12 blossom LOD slots. The rendered comparison measured mean saturation `0.147` higher and grayscale brightness `0.162` lower than the pale, over-bright color tuning, while remaining `0.026` brighter than the original package shading (values on 0–1 scales). This refinement changes only the menu material/shader and their regeneration defaults; scene files, source materials/shaders, tree geometry/placement, lighting profile and gameplay were preserved.

Imported ground-cover validation passed **21 focused tests** in the temporary Unity project, including linked bushes/grass, hidden originals, package materials/LODs, absence of demo interactions, tree preservation, navigation and player animation. No shader errors or exceptions were reported. Scene comparison confirmed exactly 115 old plant renderers hidden and one replacement group added, with no other existing scene objects or settings changed.

Japanese foliage validation passed **17 focused tests** in the temporary Unity project: saved botanical geometry/materials, three sakura trees, foliage backups, menu wiring/navigation, and player idle/backflip isolation. Scene comparison confirmed that only 102 original foliage roots were hidden and one garden child added; no other existing scene objects or settings were modified. `Level.unity` and the menu lighting profile remained byte-for-byte unchanged.

`CubeDash.Tests.MainMenuTests` covers build order, authored object/material references, camera/Volume wiring and focus distances, a rendered post-processing on/off comparison, button wiring, runtime panels/preview/volume and navigation into `Level`. Set `CUBE_DASH_MENU_PLAY_PREVIEW` to a PNG path to capture the runtime view during the test. `MainMenuSceneBuilder.BuildAndCaptureFromCommandLine` authors the scene and captures an editor preview; `MainMenuGraphics.ApplyAndCaptureFromCommandLine` only tunes the profile and captures the existing scene. Use a separate validation project if the main project is open.

`PlayerCubeVisualTests` covers shared-player prefab inheritance, the preview's absence of gameplay components, grounded volume-preserving squash/rebound, keyboard/gamepad isolation, Ready-state idle, pause, unchanged collision bounds and eye visibility during all three vehicle power-ups. The player-presentation validation in Unity 6000.4.1f1 passed all five preview/idle/navigation checks after correcting EditMode wait timing and the old mascot's rendered-color expectation. Across that recheck and the initial 52-test regression run, 51 unique tests passed. The remaining existing audio test hard-codes a `0.045` pitch step while the authored Level uses `0.041`; neither the scene's audio setting nor that unrelated test was changed. Validation used `-burst-disable-compilation` in the temporary project only.

Fonts: Anton (SIL Open Font License) and Permanent Marker (Apache License 2.0), bundled with their respective license files.
