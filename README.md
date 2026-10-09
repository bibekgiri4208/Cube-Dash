# Cube Dash — Color Match

Open **`Assets/Scenes/Level.unity`**. The player, colored cubes, matte city, track, Canvas,
trail, lighting, and original **Main Camera** are real saved scene/prefab objects, visible before Play.

## Play

- Desktop-first: press **Play**, then **Space/Enter**, gamepad **A / Cross**, or click **START RUN**.
- Move with **A/D**, **Left/Right**, gamepad **left stick**, or **D-pad left/right**.
- **Collect cubes matching your player's color:** each cube disappears and awards **1 point**.
- **Avoid other colors:** any wrong-color contact ends the run.
- **P/Escape** or gamepad **Menu/Start** pauses/resumes; **B / Circle** also resumes a paused run.
- **R** restarts; **X / Square** restarts from pause or game over. **A / Cross** confirms the focused menu action.
- Navigate pause-menu buttons using **Up/Down**, **Tab**, **D-pad**, or the **left stick**. Mouse clicks still work.
- A stick tilt changes one lane, not every frame; return to neutral or tilt the opposite way to move again.
  Drift/dead-zone hysteresis prevents accidental steering, and stick + D-pad input cannot double-step a lane.
- Losing focus pauses. Disconnecting the controller during controller play also pauses and switches prompts
  back to keyboard; reconnecting lets you resume. An unused controller disconnect does not interrupt keyboard play.

Touch swipes and click-anywhere start/retry gestures are removed. The existing **Lane Controls** objects are
retained for editing but hidden both in the saved scene and during Play. Desktop controls are shown instead,
with prompts switching automatically when you use a keyboard/mouse or gamepad. Menu focus has a visible outline.

Game over uses an abstract, transparent overlay: a small geometric cube motif, one score,
and **TRY AGAIN**, without explanatory paragraphs or duplicate HUD stats. The city remains
visible behind it. Edit it under **Canvas > Safe Area > Game Over Overlay**.

The default player is **red**. Choose Red, Blue, or Green using **Player Cube Color** on
**Game Manager**. Gameplay uses explicit color identities rather than comparing shaded RGB values.
The player and target cubes share the same saved palette materials. The best collection score
is saved separately from the old distance record.

## Visual direction

The scene keeps the colorful blue track, beveled cubes, mint/stone city, and pale horizon,
with selective HDR bloom on the cubes and trail instead of washing out the entire city.
Warm soft sunlight, cool shadow-free sky fill, and three-color ambient illumination improve
shaded faces and contact lighting. This is an ambient/indirect-light approximation, not baked
GI: the endless city moves and recycles, so world-space baked lightmaps would not stay aligned.
The original fog and high-quality shadow presets are retained.

Lane changes ease in/out with a restrained bank and yaw; the camera follows smoothly. The ready
cube breathes gently with a synchronized glow. Matching pickups trigger a squash-then-rebound,
an emission flash, a score pop, floating **+1**, and an
original rising arcade chime. The red player gets this sound when collecting red cubes; choosing
blue or green gives the same feedback only for that matching color, never for a fatal contact.
Sound is a short, locally authored PCM clip, with a five-step pitch cycle and Inspector volume
and pitch step. Rapid pickups combine in the floating score popup rather than losing feedback.
Pausing freezes gameplay feedback and pauses audio; restarting clears the pulse and tail history.

The tail is a longer, soft-edged luminous ribbon, tapered and faded from head to tip. Its saved
pieces sample the actual driven path by distance, connect through lane changes, and lengthen with
speed. Pickup light ripples from head to tip, freezing when paused and clearing on restart.
It uses a fixed-size history and the existing twelve meshes, with no runtime object churn.
The HUD uses rounded navy panels, clear coral actions, responsive placement, animated menu entry,
and hover/press feedback. The city remains visible around the menu cards.

## Edit

- **Player Cube** is a `PlayerCube` prefab instance with a real mesh and BoxCollider. Its saved
  **Cube Visual** child banks and squash/stretches independently, keeping collision bounds stable.
- **Track > Segment 00–07** are reusable `TrackSegment` prefab instances.
  Their nine row cubes use the existing **`ObstacleCube.prefab`**, now a plain color cube
  with a `RunnerCube` component rather than a barricade.
- **City Surroundings** contains three editable `Skyscraper` prefab designs with matte architecture
  (podium bases, banded shafts, stepped crowns, roof plant, and masts). Sixteen towers are placed
  per segment across four depth rows per side, ending in a taller skyline row on the plaza slab.
- **Player Trail** contains twelve saved ribbon meshes animated by `CubeWake`; length is configurable.
- **Canvas** contains editable UI and persistent button events. The start overlay is transparent
  so the scene remains visible. **City Atmosphere** holds the bloom/color-grading profile.
- Palette assets: **`Assets/Material/CubeDashRed.mat`**, **`CubeDashBlue.mat`**, **`CubeDashGreen.mat`**.
- Sky: **`Assets/Material/CubeDashHorizon.mat`**. Atmospheric profile:
  **`Assets/Settings/CubeDashVisuals.asset`**; bloom uses a 1.05 HDR threshold and restrained intensity.
- Audio: **`Assets/Audio/ArcadeCollect.wav`**; source, clip, volume, and lane easing are on **Game Manager**.
  Base/pickup emission and rebound duration are also configurable there; pickup wave duration is
  on **Player Trail**. Restart restores the authored visual and popup positions and resets sound pitch.
- UI sprite: **`Assets/2D Images/ArcadePanel.png`**. **Sky Bounce Fill** is an editable scene light.
- **Tools > Cube Dash > Apply Arcade Polish** reapplies this pass without rebuilding the city or rules.
  The older **Apply Clean Color-Match Style** command intentionally restores the matte/no-bloom look.
- **Tools > Cube Dash > Apply Desktop and Gamepad Controls** updates only the desktop UI/input presentation.
  The retained mobile rendering preset is an optional lightweight preset, not a mobile build target.
- **Tools > Cube Dash > Refine HUD Layout** re-applies the current UI layout: score in the top-right,
  icon-only pause button in the top-left, bottom instruction and eyebrow hidden, a simplified start
  card (controls move into its footer), and a **QUIT** button under **TRY AGAIN** on Game Over.
- Speed and acceleration are on **Game Manager**. Lane spacing and the three palette material
  references are on **Track**. Use a nonzero **Fixed Seed** for reproducible generation.

## Difficulty

Speed now grows with collection score, from **14 m/s** to **42 m/s** over the first **120 points**,
with smooth acceleration rather than abrupt tier jumps. Rows arrive roughly every **1.0 → 0.33 s**.
Recycled rows increasingly demand lane changes: same-lane matches fall from **35%** to **5%**.
The opening remains safely delayed, and the matching route never jumps across two lanes at once.
Tune **Score For Maximum Difficulty**, **Maximum Speed**, and **Acceleration** on **Game Manager**.
Existing row cubes and road segments are still reused; difficulty does not add runtime objects.

Each active row contains one red, one blue, and one green cube. The matching-color lane changes
by at most one lane per row. The first row is safely delayed and begins with a center-lane match.
Existing segments/cubes are recycled and recolored; no geometry or UI is created at runtime.
Swept collider checks handle low-frame-rate contacts in chronological order, prevent duplicate
collection, and never award points for cubes after an earlier fatal touch.

The original Plane is retained as the starting section, and the original Main Camera remains
assigned to Game Manager. The clean-style authoring tools are Editor-only; the completed scene
is already saved, so no setup command is required.

## Shadows and render distance

The PC URP preset uses a **4096** main-light shadow atlas, **160 m** shadow distance, four
cascades (12/32/72/160 m), and high-quality soft filtering. The mobile preset uses **2048**,
**90 m**, two cascades, and medium-quality soft shadows. Reduced normal bias keeps cube
shadows closer to their feet; the final cascade fades toward the fog rather than ending abruptly.

The **Main Camera** far clip is **600 m** and linear fog runs **45 → 260 m**, so the skyline
stays visible well past the shadow distance without a visible clipping edge. Fog ends before
the far plane, and shadow-casting geometry inside the fog keeps its shadows.

These presets cost more GPU time than the original shadows. Tune them in
`Assets/Settings/PC_RPAsset.asset` or `Mobile_RPAsset.asset` if a target device struggles.

## Rendering quality

The PC pipeline renders HDR with **4x MSAA** on top of SMAA (High quality); the mobile preset
uses 2x MSAA at 0.9 render scale. Screen-space ambient occlusion runs at full resolution with
high-quality normals, 12 samples, and bilateral blur so contact points and podium bases read
clearly without muddying the matte look. Color grading uses the High Range HDR LUT, both quality
levels synchronize to the display to avoid tearing, and the fog color matches the skybox horizon
exactly so the fogged skyline dissolves into the sky without a seam.

## Tests

**Window > General > Test Runner > EditMode > CubeDash.EditorTests** checks authored scene
references, all-color/reachable procedural rows, reproducible seeds, swept contacts, matching
collection, wrong-color failure, scoring/restart, pause, the trail, shadow presets, and 50 km of
fixed-size recycling. `ArcadePolishTests` additionally checks selective bloom, emissive palette,
the non-silent/click-free pickup clip, saved audio/UI references, squash/rebound and traveling
trail light, rapid-pickup popups, all three colors' pitch cycles, and pickup/pause/restart feedback.
`DesktopInputTests` uses virtual gamepads to check drift/hold/reversal handling, steering,
menu navigation, single-confirm resume/retry, controller disconnect/reconnect, and the shared desktop fallback actions.
