# Level UI

Open `Assets/Scenes/Level.unity` and press Play, or choose any of the six maps under **Start** in the Main Menu. City, Jungle, Mountain, Desert, Beach and Country Side share Level's UI and gameplay progression. The old start-game card is no longer shown. A saved, central red ribbon counts **3, 2, 1**, then gameplay starts automatically.

## Shared visual theme

- Uses the same `MenuRibbonGraphic`, `MenuPanelGraphic`, Anton font, pink paper, red accent, cream labels and dark outlines as the Main Menu.
- Points/best stay top-right, the pause icon stays top-left, and distance stays top-centre in a compact **180 × 72** ribbon (below the corner HUD on narrow layouts). The counter shows only metres, such as **125 m**, with no stage text; gameplay progression is unchanged.
- Pause: Resume Run, Restart, Sound, Quit. Game over: one score, Try Again, Quit. **Both in-game Quit buttons return to Main Menu**, without closing the application or ending Editor Play mode; only the Main Menu's own Quit exits. Button focus uses the same outline/scale feedback as menu ribbons.
- Power-ups use separate compact pink cards stacked at the left safe-area edge, with red duration meters. Cheat messages keep their matching ribbon. Old instruction lines and geometric motifs remain editable but hidden.
- All UI is saved under `Canvas/ Safe Area`; runtime does not generate UI objects or replace gameplay/scenery assets.

## Countdown and controls

- The three-second countdown keeps the track, player position, points, distance and power-up timers frozen. Confirm/steering cannot skip it.
- Pause works during the countdown; resume continues the remaining time rather than starting early or resetting it. Focus loss or an active controller disconnect also pauses.
- Restart/Retry buttons, R and gamepad retry prepare a fresh run and display the countdown again. Normal pause/resume during a running game does not insert another countdown.
- `CubeDashGame.RequestRun()` is the player-facing entry. `StartRun()` remains the immediate programmatic reset for simulations/authoring tests.
- Countdown and ribbon presentation are scene opt-ins. Country Side's saved scene/prefabs and its original ready/start flow are unchanged.

## Pause sound and map music

All six playable scenes (Dynamic/Level, Country Side, City, Jungle, Desert and Beach) loop `Assets/Audio/Music/Main Menu Bg.mp3`. Each scene owns its music source, so changing maps or returning to Main Menu does not stack loops. Music continues during pause and game over.

**Pause > Sound** opens separate Music and SFX sliders with percentage readouts. Changes apply immediately and survive map changes/relaunches. Music shares `CubeDash.MusicVolume` with Main Menu Options. SFX uses `CubeDash.SfxVolume` and the SFX bus in `Assets/Resources/Game Audio.mixer`, covering coin/power-up pickups, vehicle engines, impacts and biome ambience without changing their source envelopes. The existing Main Menu master volume remains the overall limit.

Mouse, gamepad/D-pad and keyboard navigation work throughout. Left/right adjusts the focused slider; Tab cycles controls. Back, Escape/P or gamepad B/Menu returns to the pause menu with Sound selected, **without resuming**. Confirm on a slider does nothing; confirm on Back closes the panel. Background mouse clicks preserve focus. Paused countdowns and runs remain frozen.

Use **Tools > Cube Dash > Add Pause Sound Controls and Map Music** (`CubeDash.Editor.PauseSoundAuthoring.ApplyFromCommandLine`) to apply the additive upgrade to saved maps. UI is authored in each Canvas, music under its Game Manager, and Country Side uses scene-local prefab overrides; shared world/gameplay prefabs are not rebuilt.

The sound update passed **42/42** audio/input/menu tests, including independent channel mute, saved levels across maps and back to Main Menu, no duplicate music sources, countdown pause, keyboard/controller Back, focus retention, and landscape/portrait rendering. Scene audits excluded unrelated serialized defaults and preserved existing scenery documents.

## Editing

Use **Tools > Cube Dash > Match Level UI to Main Menu** (`CubeDash.Editor.LevelMenuTheme.ApplyFromCommandLine`) to upgrade a legacy Level. It only accepts Level, requires saved edits, and preserves already-themed UI on reapplication. The older HUD layout, desktop controls and color/arcade polish tools leave the new UI alone.

The countdown is `Canvas/Safe Area/Run Countdown`. Pause and game-over use the existing `Menu Overlay/Menu Card` and `Game Over Overlay/Abstract Composition`, preserving button navigation and quit behavior. Editing the gameplay/world prefabs is not required.

## Power-up duration cards

`Canvas/Safe Area/Power-Up Bars` contains saved 216 × 56 cut-corner cards with Anton labels, dark outlines, and a cream/red meter. Active cards stack without empty gaps; inactive cards are hidden. Multiple simultaneous effects are shown separately rather than combined into the old long banner.

- Double Points, Flight, Truck, Magnet and temporary post-vehicle protection show remaining seconds (rounded up) and a meter normalized to their actual configured duration.
- The one-hit Shield shows **1 HIT** and a full charge; Supercar shows **ON**, since neither has a timed expiration.
- Recollecting a timed effect refills its own meter. Expiration or consuming a shield removes the card. Vehicle transitions replace the relevant card with timed protection, using the correct flight/truck protection duration.
- Pause freezes the meters; game over, restart and countdown hide/clear them. Cards sit below the top HUD, with additional clearance on narrow screens, and scale down if necessary to fit the available vertical safe area.
- Meters are display-only sliders: no handles, input callbacks, raycasts or gamepad focus. They do not change timers or gameplay.

Use **Tools > Cube Dash > Add Left Power-Up Duration Bars** (`CubeDash.Editor.LevelPowerUpBars.ApplyFromCommandLine`) to upgrade an already-themed Level. The Main Menu Level-theme authoring also applies this upgrade. Reapplication preserves manual card edits. `RunnerPowerUpBars` drives existing cards; no runtime objects are created. The old `Power-Up Status` and `Power-Up Ribbon` remain inactive and editable. Country Side's existing UI is not modified.

## Validation

Rendered previews were checked for 3/2/1, the running HUD, power-up/cheat ribbons, pause and game over, including portrait panels. The rendering fixture also verifies visible numeral geometry and saved score text. Labels use the Main Menu's higher-resolution `MenuLabelText` sampling; large numerals permit vertical overflow so Anton's line metrics cannot truncate them.

A serialized-scene audit confirmed that camera, world, player/collider, track/power-up references and tuning were preserved. Unity reordered one existing player-prefab override list and explicitly saved existing default progression/track values; these do not alter behavior. The visually checked scene and countdown/UI code were hash-verified after transfer from an isolated validation project. The open source Editor was not interrupted, and Main Menu/Country Side scenes, shared prefabs and build configuration were not modified.

The power-up-card upgrade passed 10 initial UI/duration/countdown checks, including rendered landscape/portrait previews of three simultaneous effects. A second scene audit confined the card upgrade to Canvas objects; gameplay/world objects were unchanged. Tests cover separate configured-duration fractions, refresh/expiry, shield charge, vehicle swaps, post-vehicle protection, pause, reset, navigation/raycast exclusion, safe reapplication, and repeated runs without UI-object growth.

Final power-up/gameplay/UI regressions passed **76/76**. The compact distance-only counter and pause-to-Main-Menu follow-up passed **14/14** UI checks, including stage-boundary readings, pause/restart distance reset, returning from both a paused run and paused countdown, duplicate Quit clicks, menu music/focus restoration, and starting Level again. Pause Quit uses the existing saved callback and a guarded single-scene load; no button rewiring or gameplay progression changes were needed.

Game Over Quit now uses the same guarded Main Menu return. Its follow-up test verifies duplicate-click safety, continued Play mode, gameplay unloading, restored menu music/focus and a visible replay of the startup stagger; **34/34** combined menu/Level/UI checks passed.
