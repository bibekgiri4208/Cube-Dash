# Main menu

Open `Assets/Scenes/Main Menu.unity` and press Play. It is the first enabled build scene; **Start** loads the existing `Level` scene.

The reference-inspired scene contains editable 3D parking bays, weathered asphalt, concrete/graffiti wall, warm sconces, layered foliage, neighborhood buildings, overhead wires, a classic sedan, safety props, and a red cube with black eyes. Its low camera and left-hand cut-corner ribbons use the supplied image's composition. This is a recreation, not the image's original models or a pixel-identical reproduction.

- **Shop:** a clearly labeled, non-purchasing color preview. Preview colors are not persisted or applied to gameplay.
- **Options:** persistent master volume and fullscreen toggle.
- **Quit:** closes the application, or stops Play mode in the Editor.
- Mouse, keyboard/UI navigation and gamepad are supported. Escape/back closes a panel.

## Editing

Environment and canvas objects are saved in the scene under `Cube Dash Main Menu`. Camera and sun are separate scene objects. Materials, mesh assets, shader and licensed fonts are in `Assets/MainMenu`.

`Tools > Cube Dash > Build Reference Main Menu` regenerates **only the generated menu root**, resets the menu camera/sun/environment settings, and sets build order. It preserves unrelated root objects and does not rebuild `Level`. Save scene edits before running it. Regeneration replaces manual edits within the generated root.

`MainMenuSceneBuilder` holds the camera, prop and ribbon layout. `MainMenuController` holds navigation and panel behavior. The menu does not use gameplay's road-deformation shader.

## Post-processing

Select **Cube Dash Main Menu > Menu daylight grading** in the Hierarchy. Its global Volume uses `Assets/MainMenu/Menu Lighting.asset`; the Main Camera has post-processing enabled and includes the Volume's Default layer. Inspect the result in **Game view**. For Scene view, also enable **Post Processing** in its effects menu.

The menu uses bright, airy arcade daylight: neutral tonemapping, lifted exposure, soft contrast, vibrant colors, broad bloom and no dark vignette. Brighter ambient sky fill and gentler sun shadows keep the shaded faces light. Gentle far-only Gaussian depth of field keeps the red mascot and graffiti wall in focus. The overlay UI is not blurred or color-graded. These settings do not modify the gameplay profile or shared renderers.

`Tools > Cube Dash > Tune Main Menu Post Processing` reapplies the menu profile defaults **without rebuilding or saving the scene**, then selects the profile for editing. Use this rather than the scene builder when only adjusting post-processing.

## Validation

`CubeDash.Tests.MainMenuTests` covers build order, authored object/material references, camera/Volume wiring and focus distances, a rendered post-processing on/off comparison, button wiring, runtime panels/preview/volume and navigation into `Level`. Set `CUBE_DASH_MENU_PLAY_PREVIEW` to a PNG path to capture the runtime view during the test. `MainMenuSceneBuilder.BuildAndCaptureFromCommandLine` authors the scene and captures an editor preview; `MainMenuGraphics.ApplyAndCaptureFromCommandLine` only tunes the profile and captures the existing scene. Use a separate validation project if the main project is open.

Fonts: Anton (SIL Open Font License) and Permanent Marker (Apache License 2.0), bundled with their respective license files.
