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

## Validation

`CubeDash.Tests.MainMenuTests` covers build order, authored object/material references, button wiring, runtime panels/preview/volume and navigation into `Level`. Set `CUBE_DASH_MENU_PLAY_PREVIEW` to a PNG path to capture the runtime view during the test. `MainMenuSceneBuilder.BuildAndCaptureFromCommandLine` authors the scene and captures an editor preview; use a separate validation project if the main project is open.

Fonts: Anton (SIL Open Font License) and Permanent Marker (Apache License 2.0), bundled with their respective license files.
