# Cube Dash — Color Match

Open **`Assets/Scenes/Level.unity`**. The player, colored cubes, matte city, track, Canvas,
trail, lighting, and original **Main Camera** are real saved scene/prefab objects, visible before Play.

## Play

- Press **Play**, then tap/click, **Space/Enter**, or **TAP TO START**.
- Move with **A/D**, **Left/Right**, swipes, or the on-screen arrows.
- **Collect cubes matching your player's color:** each cube disappears and awards **1 point**.
- **Avoid other colors:** any wrong-color contact ends the run.
- **P/Escape** pauses/resumes; **R** restarts. Losing focus automatically pauses.

Game over uses an abstract, transparent overlay: a small geometric cube motif, one score,
and **TRY AGAIN**, without explanatory paragraphs or duplicate HUD stats. The city remains
visible behind it. Edit it under **Canvas > Safe Area > Game Over Overlay**.

The default player is **red**. Choose Red, Blue, or Green using **Player Cube Color** on
**Game Manager**. Gameplay uses explicit color identities rather than comparing shaded RGB values.
The player and target cubes share the same saved palette materials. The best collection score
is saved separately from the old distance record.

## Visual direction

The scene follows the reference's simple, colorful aesthetic: a matte blue track, beveled
solid-color cubes, neutral mint/stone city blocks, a pale gradient horizon, soft sunlight/shadows,
and distance fog. No illuminated windows, sci-fi barricade markings, emissive edges, or bloom.
The cube's wake remains as a subtle, non-glowing colored trail. A small squash/pop gives feedback
when a matching cube is absorbed. The camera is closer so the player and cube colors read clearly.

## Edit

- **Player Cube** is a `PlayerCube` prefab instance with a real mesh and BoxCollider.
- **Track > Segment 00–07** are reusable `TrackSegment` prefab instances.
  Their nine row cubes use the existing **`ObstacleCube.prefab`**, now a plain color cube
  with a `RunnerCube` component rather than a barricade.
- **City Surroundings** contains three editable `Skyscraper` prefab designs with matte architecture
  (podium bases, banded shafts, stepped crowns, roof plant, and masts). Sixteen towers are placed
  per segment across four depth rows per side, ending in a taller skyline row on the plaza slab.
- **Player Trail** contains twelve saved ribbon meshes animated by `CubeWake`.
- **Canvas** contains editable UI and persistent button events. The start overlay is transparent
  so the scene remains visible. **City Atmosphere** holds the non-neon color-grading profile.
- Palette assets: **`Assets/Material/CubeDashRed.mat`**, **`CubeDashBlue.mat`**, **`CubeDashGreen.mat`**.
- Sky: **`Assets/Material/CubeDashHorizon.mat`**. Atmospheric profile:
  **`Assets/Settings/CubeDashVisuals.asset`**; bloom is disabled.
- Speed and acceleration are on **Game Manager**. Lane spacing and the three palette material
  references are on **Track**. Use a nonzero **Fixed Seed** for reproducible generation.

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

## Tests

**Window > General > Test Runner > EditMode > CubeDash.EditorTests** checks authored scene
references, all-color/reachable procedural rows, reproducible seeds, swept contacts, matching
collection, wrong-color failure, scoring/restart, pause, the trail, shadow presets, and 50 km of fixed-size recycling.
