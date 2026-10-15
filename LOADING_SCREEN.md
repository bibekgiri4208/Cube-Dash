# Loading screen

Map selection and in-game Quit (pause or game over) now use `SceneLoader.Load` instead of directly loading a scene. All six maps share `Assets/Resources/Loading Screen.prefab`; no map scenes, world prefabs, build settings or image-import settings are changed.

- Uses the original `Assets/2D Images/Loading Screen.png`, filling the display without stretching. Portrait screens crop the sides.
- Pink cut-corner paper, Anton **LOADING...** text, destination, percentage and a red progress slider match the existing UI theme. The panel respects the safe area; the image covers the whole screen.
- The slider is a read-only indicator, not a focusable control. A full-screen blocker and temporary navigation lock prevent mouse/gamepad input from reaching either scene during transition.
- Progress follows Unity's asynchronous scene loading, reserves the final portion for scene activation, and reaches 100% only after activation completes. A short minimum display time prevents a fast load from flashing the image for a single frame. Timing and the fade use unscaled time.
- The overlay persists across scene replacement and destroys itself afterward. Duplicate requests cannot create multiple overlays or queue another scene.
- Map countdown/gameplay stays frozen until the overlay clears. The main-menu entrance animation waits too, so its stagger is visible after returning. Normal Retry/Restart stays in the existing map and uses its normal countdown.

Edit the prefab to customize the layout, colors or timings. **Tools > Cube Dash > Create Themed Loading Screen** (`CubeDash.Editor.LoadingScreenAuthoring.ApplyFromCommandLine`) creates it only when absent and preserves existing prefab edits.

Validation: all loading-screen checks passed, including every map and return path, monotonically increasing progress, 100% completion, frozen countdowns, duplicate requests, missing scenes, restored music/navigation, and landscape/portrait previews. Combined runs passed 42 checks; one unrelated existing menu-lighting test expects contrast at most 0 while the unchanged lighting asset is configured to 5.
