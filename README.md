# Cube Dash — Coin Runner

Open **`Assets/Scenes/Level.unity`**. The player, colored cubes, dynamic landscapes, track, Canvas,
trail, lighting, and original **Main Camera** are real saved scene/prefab objects, visible before Play.

## Play

- Desktop-first: press **Play**, then **Space/Enter**, gamepad **A / Cross**, or click **START RUN**.
- Move with **A/D**, **Left/Right**, gamepad **left stick**, or **D-pad left/right**.
- **Collect gold 3D coins:** each coin disappears and awards **1 point**.
- **Avoid obstacle cubes:** touching an obstacle ends the run.
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
and **TRY AGAIN**, without explanatory paragraphs or duplicate HUD stats. The current landscape remains
visible behind it. Edit it under **Canvas > Safe Area > Game Over Overlay**.

The default player is **red**. Choose Red, Blue, or Green using **Player Cube Color** on
**Game Manager**. Gameplay uses explicit color identities rather than comparing shaded RGB values.
The player and obstacle cubes use the saved palette materials; rewards always use gold coin materials. The best collection score
is saved separately from the old distance record.

## Power-ups

- **Shield:** a solid cyan shield pickup with silver emblem; absorbs one obstacle contact.
- **Double Points:** a gold extruded **2×** pickup; doubles coin scoring for seven seconds.
- **Magnet:** a red horseshoe magnet with silver tips; pulls nearby coins from all three lanes
  within **8 metres** for **10 seconds**, without attracting hazards or other power-ups.
  It works alongside Double Points and Truck, freezes on pause, and clears on restart/game over.
  Collecting another Magnet refreshes its timer. Magnet also pulls coins across lanes while flying.
- **Fighter Plane:** a miniature fighter pickup transforms the player into a faceted, twin-engine
  jet for **10 seconds**. It climbs above the rows, banks while steering, and emits two bounded
    smoke/afterburner trails. Flight automatically attracts nearby ground coins from the plane's current lane,
   lifting them up to the plane without needing a Magnet pickup. An active Magnet expands this to nearby coins
   across all lanes, and Double Points still applies. Ground obstacles and power-ups remain out of reach while airborne;
  distance and scenery continue advancing at the normal speed.
- At the end of flight, the cube returns and descends with **3 seconds of continuous landing
  shield**, displayed as a cyan bubble and HUD countdown. It protects against multiple wrong-color
  contacts, without spending an existing one-hit shield. Pause freezes flight, shielding and exhaust;
  restart clears all flight effects, timers and restores the cube/collider/trail.
- **Truck:** a miniature blue cab-over truck pickup transforms the cube into a three-axle tractor
   for **10 seconds**. It keeps normal lane steering and smashes obstacle cubes into colored,
   tumbling pieces, regardless of color. Coins are collected normally (and respect Double Points),
   without creating obstacle fragments. Obstacles are destroyed without ending the run or spending a shield charge.
   The compact truck has soft suspension bounce, body sway and steering front wheels. Both tall chrome
    stacks emit bounded, billowing smoke that rises and drifts behind the truck.
    Six tire-contact emitters add light gray smoke along the road, with stronger haze during steering
    and at higher speeds. Stopped tires produce no new smoke; the short-lived puffs fade naturally.
- When the truck ends, the cube receives **3 seconds of continuous exit shield**, with a cyan bubble
   and HUD countdown. Pause freezes the truck timer, wheels and debris; restart clears everything.
    Stack and tire smoke also freeze on pause and clear when the truck ends or the run restarts.
   Collecting another truck refreshes its duration. Fighter Plane pickups are ignored while the truck is active
   and become collectible again after it expires.

The five bonus types share the existing pooled pickup slots and unchanged spawn frequency.
Power-ups occupy dedicated positions **7 metres before coin rows**, rather than sitting on coins.
Even staggered hazards remain at least **4 metres** away. Pickups sway/bob while running and freeze on pause.
Their Shield, 2× and Magnet meshes are saved under `Assets/3D Models`, with editable prefabs
`Assets/Prefab/CubeDash/ShieldPickup.prefab`, `DoublePointsPickup.prefab` and `Magnet.prefab`.
**Tools > Cube Dash > Add Magnet and 3D Power-Up Models** rebakes them and updates saved pickup slots,
without rebuilding coins, environments or obstacles. Magnet duration and range are exposed on **Game Manager**.
Fighter
geometry, particles and shield are saved assets, not generated during Play. The editable model is
`Assets/Prefab/CubeDash/FighterPlane.prefab`, with mesh `Assets/3D Models/CubeDashFighter.asset`
and materials in `Assets/Material/FighterPlane`. **Tools > Cube Dash > Add Fighter Plane Power-Up**
rebakes and reconnects the feature without changing difficulty or environments. Duration, landing
shield and altitude are exposed on **Game Manager**.
The reference-style truck has a square blue sleeper cab with a navy band, a lighter flat roof,
split windshield, hollow bevelled-square exhaust stacks, an open ladder chassis and fifth-wheel saddle.
Faceted dark fuel tanks, curved rear mudguards, separate mudflaps and dual rear tires complete the model.
Its six animated wheel assemblies contain ten tires (two front, eight rear).
The truck model is saved in `Assets/Prefab/CubeDash/Truck.prefab`, with body/front/rear-wheel meshes under
`Assets/3D Models` and materials in `Assets/Material/Truck`. **Tools > Cube Dash > Add Truck Power-Up**
rebakes and reconnects it. Truck duration and exit-shield duration are exposed on **Game Manager**.
**Tools > Cube Dash > Refine Truck Presentation** updates only the truck/pickup prefabs and smoke,
preserving the level and gameplay tuning.
**Tools > Cube Dash > Rebuild Reference Truck Model** rebakes the reference-style geometry and palette
without changing the level, power-up durations, smaller player scale or smoke/steering behavior.
**Tools > Cube Dash > Add Truck Tire Smoke** updates only the truck/player/pickup prefabs and tire-smoke
material, preserving existing geometry and gameplay. Emitters are wheel siblings so smoke never spins
with the tires; each has a fixed 48-particle limit and creates no runtime objects or colliders.
**Obstacle Fragments** is a fixed pool of 128 saved pieces: impacts create no objects,
rigidbodies or gameplay colliders, and fragments disappear after 1.15 seconds.

## Visual direction

### Changing environments

The run travels through **City → Jungle → Mountains → Desert → Beach**, then loops. Each region
lasts **336 metres** (eight track sections), so scenery changes with distance, not frame rate or score.
New landscapes approach naturally along the road; existing visible sections never suddenly swap.
Sky, fog, sunlight and ambient colors blend across each boundary, without environment-name text
on the HUD. The blue road and the red/blue/green gameplay palette remain unchanged.

- **City:** recessed glass windows, projecting sills, entrance canopies, corner trim and roof vents
  on the existing stepped towers.
- **Jungle:** tapered trunks, visible branches and buttress roots, fuller rounded canopies,
  bark grain, fern undergrowth, mossy rocks, a river and layered distant hills.
- **Mountains:** detailed asymmetric craggy ridges with broken snowlines, weathered rock grain,
  scree, distant peaks and five-tier evergreens with scalloped branch silhouettes.
- **Desert:** wind-shaped dunes with subtle sand ripples, layered sandstone bluffs,
  pebbles, dry grass and rounded, curved cactus arms.
- **Beach:** leaning ringed palm trunks, curved fronds with individual leaflets and coconuts,
  turquoise ocean, wet shoreline sand, timber houses with hipped thatch roofs, framed windows,
  porches and steps, and individual jetty planks.
- Beach sand extends **1,024 m to either side** and ocean reaches **1,024 m outward**, beyond the
  600 m camera far plane. All other landscape floors are widened too, removing lateral cutoffs
  at wide FOVs without reducing the view angle.
- Saved ground/ocean aprons extend the two outer pool edges by **1,024 m** as well, covering
  extreme side-angle views. Only the first/last section activates them; biome boundaries inside
  the pool retain their normal footprints, avoiding overlapping water or sand in other regions.
- The Beach ocean has four directional **Gerstner swells** with raised/choppy crests, denser
  nearshore geometry and separate horizon wave meshes. Rolling breakers, broken shoreline foam,
  crest whitecaps, ripple normals, a shallow-to-deep gradient, Fresnel reflection and sun glints
  give the surface more depth. A sloping seabed stays below the larger wave troughs.
- An accelerated **40-second visual tide** raises/lowers the ocean and advances/retreats its
  shoreline, with smaller swash motion and synchronized dark, glossy wet sand. Tide height/travel,
  period, swell amplitude/speed, choppiness and foam are editable on
  `Assets/Material/Environments/Lagoon Water.mat`. If changing shoreline travel or period,
  match those settings on `Coastal Sand.mat` and `Wet Sand.mat` in the same folder.
  **Tools > Cube Dash > Improve Beach Ocean Waves** rebakes just these ocean/beach assets,
  preserving the scene, scenery layouts and gameplay; the saved level needs no setup command.
  River water uses
  a separate, calmer material. This is opaque stylized water, not screen-space reflection/refraction.
- Beach boundaries form curved bays rather than rectangular water edges. Shore foam and a rocky
  headland close the exit; sand blends into the next city plaza. Per-section property blocks keep
  boundary clipping/blending local, including horizon aprons, and reset when sections recycle.
- Slowly drifting clouds, waves, surf, tides and wet-sand animation freeze on pause and reset on restart.

The skybox now uses deeper biome-specific blues, two cloud layers with soft shaded billows,
a restrained sun disc/halo aligned with the sunlight, and seamless direction-space cloud noise.
Clouds fade into the existing biome horizon/fog, avoiding stretched bands or abrupt sky changes.
Coverage, scale, opacity and drift are editable on `Assets/Material/CubeDashHorizon.mat`.

Each non-city region has three deterministic mesh layouts. Saved scenery roots are pooled with
the eight track sections; recycling toggles roots and swaps shared meshes without instantiating
objects or adding gameplay colliders. Environment selection uses no obstacle random numbers.
Tune **Segments Per Biome** and **Transition Distance** on **Track > Environment Director**.
Assets are under `Assets/Prefab/CubeDash/Environments`, `Assets/Material/Environments` and
`Assets/3D Models/Environments`. **Tools > Cube Dash > Apply Dynamic Environments** rebakes the
saved landscapes and reconnects the scene without rebuilding the road, cubes or existing city.
**Tools > Cube Dash > Rebuild Environment Models** refreshes scenery meshes, materials and prefabs,
preserving scene settings, difficulty and HUD. The richer meshes are still shared and pooled;
no trees, terrain or colliders are generated during a run.

The scene keeps the colorful blue track, beveled cubes, and an opening mint/stone city,
with selective HDR bloom on the cubes and trail instead of washing out the landscapes.
Warm soft sunlight, cool shadow-free sky fill, and three-color ambient illumination improve
shaded faces and contact lighting. This is an ambient/indirect-light approximation, not baked
GI: the endless city moves and recycles, so world-space baked lightmaps would not stay aligned.
The original fog and high-quality shadow presets are retained.

Lane changes ease in/out with a restrained bank and yaw; the camera follows smoothly. The ready
cube breathes gently with a synchronized glow. Coin pickups trigger a squash-then-rebound,
an emission flash, a score pop, floating **+1**, and an
original rising arcade chime. Every player color gets the same feedback for coins,
never for a fatal obstacle contact.
Sound is a short, locally authored PCM clip, with a five-step pitch cycle and Inspector volume
and pitch step. Rapid pickups combine in the floating score popup rather than losing feedback.
Pausing freezes gameplay feedback and pauses audio; restarting clears the pulse and tail history.

Power-ups have five original, distinct PCM pickup cues: a shield shimmer, ascending **2×** notes,
airplane launch sweep, low truck rev and electric Magnet trill. Coin pickup audio remains unchanged
and uses its own source. Airplane mode uses an actual F-22 recording; Truck uses a recorded heavy-vehicle
engine, replacing the earlier synthetic tones. Both are CC0 recordings shared by qubodup, with credits
and retained sources in `Assets/Audio/VehicleSources/CREDITS.md`. Six-second loops use stable recorded
sections, filtering and half-second crossfades, with restrained speed-responsive pitch and steering rev.
Loops fade in, do not restart
on refreshed power-ups, pause/resume with gameplay, and stop on expiry, restart, game over or disable.
`RunnerAudio` on **Game Manager** exposes separate pickup/airplane/truck volumes; its three saved child
AudioSources are non-spatial and never autoplay. All seven clips live in `Assets/Audio` and are edited
offline, not synthesized during Play. **Tools > Cube Dash > Refine Vehicle Engine Audio** rebakes only
the two engine loops without changing pickups, sky, volume settings or the level.
**Tools > Cube Dash > Improve Sky and Power-Up Audio** rebakes
the sky/audio assets without rebuilding the level or changing gameplay, biome fog or lighting.

The tail is a longer, soft-edged luminous ribbon, tapered and faded from head to tip. Its saved
pieces sample the actual driven path by distance, connect through lane changes, and lengthen with
speed. Pickup light ripples from head to tip, freezing when paused and clearing on restart.
It uses a fixed-size history and the existing twelve meshes, with no runtime object churn.
The HUD uses rounded navy panels, clear coral actions, responsive placement, animated menu entry,
and hover/press feedback. The current environment remains visible around the menu cards.

## Edit

- **Player Cube** is a `PlayerCube` prefab instance with a real mesh and BoxCollider. Its saved
  **Cube Visual** child banks and squash/stretches independently, keeping collision bounds stable.
- **Track > Segment 00–07** are reusable `TrackSegment` prefab instances.
  Their nine row cubes use the existing **`ObstacleCube.prefab`**, now a plain color cube
   with a `RunnerCube` component. Reward slots show the saved **Coin Visual** child instead of the cube mesh.
- **City Surroundings** contains three editable `Skyscraper` prefab designs with detailed architecture
  (podium bases, banded shafts, stepped crowns, roof plant, masts and shared baked facade details). Sixteen towers are placed
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

Speed grows gently with collection score, from **12 m/s** to **30 m/s** over the first **180 points**,
with acceleration limited to **0.8 m/s²**. Rows arrive roughly every **1.17 → 0.47 s**,
giving more time to react and look at the environments. Recycled rows increasingly demand lane
changes, but same-lane coin routes only fall from **50%** to **20%**, retaining more straight stretches.
The opening remains safely delayed, and the matching route never jumps across two lanes at once.
Tune **Score For Maximum Difficulty**, **Maximum Speed**, and **Acceleration** on **Game Manager**.
Existing row cubes and road segments are still reused; difficulty does not add runtime objects.

Gold coins are saved in `Assets/Prefab/CubeDash/Coin.prefab`, with mesh `Assets/3D Models/CubeDashCoin.asset`
and materials in `Assets/Material/Coins`. The double-sided model has a bevelled edge, raised rim and embossed
star. Coins spin and bob while running, freeze on pause, and reset when their pooled slot is reused.
Their animation never moves the gameplay hitbox. **Tools > Cube Dash > Add 3D Coins** rebakes the assets
and updates saved reward visuals and HUD wording without changing obstacles or gameplay tuning.

Track layouts now mix single-lane obstacles, open stretches, occasional two-lane blocks and staggered
hazards instead of always forcing one safe lane. Open lanes can be empty or carry coins;
multiple lanes can offer points, and bonuses may appear in any open lane. At least one coin route changes
by at most one lane per row, and a two-lane block remains reachable from every previously open lane.
Two-lane blocks never appear consecutively. The first row is safely delayed, has all three lanes open,
and includes a center-lane coin. Staggered hazards arrive up to 3 metres before their row's rewards,
leaving time to steer away after collection. At top difficulty, most patterns still have multiple open lanes.
Existing segments/cubes are recycled and recolored; no geometry or UI is created at runtime.
Swept collider checks handle low-frame-rate contacts in chronological order, prevent duplicate
collection, and never award points for coins after an earlier fatal touch. Magnet attraction uses bounded
swept substeps, begins at its pickup time, and never awards a direct-contact coin twice.

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
references, varied/reachable procedural layouts, reproducible seeds, swept contacts, coin
collection, obstacle failure, scoring/restart, pause, the trail, shadow presets, and 50 km of
fixed-size recycling. `ArcadePolishTests` additionally checks selective bloom, emissive palette,
the non-silent/click-free pickup clip, saved audio/UI references, squash/rebound and traveling
trail light, rapid-pickup popups, all three colors' pitch cycles, and pickup/pause/restart feedback.
`DesktopInputTests` uses virtual gamepads to check drift/hold/reversal handling, steering,
menu navigation, single-confirm resume/retry, controller disconnect/reconnect, and the shared desktop fallback actions.
`EnvironmentTests` checks biome boundaries, saved scenery/layouts, collider-free decoration,
mesh budgets, road clearance, wide coastal coverage, matching wave-surface seams, saved facade
detail, shader depth/shadow passes, horizon-apron ownership during recycling, fixed-size pools,
complete city windows between floor bands, isolated coastal blends, smooth transitions, pause and restart.
`FighterPowerUpTests` additionally checks saved fighter geometry and bounded particles, pickup
transformation, flight/pause/restart, timed landing protection, automatic current-lane coin attraction,
Magnet/Double Points stacking in flight, airborne hazard/power-up suppression and
chronological takeoff during long frames.
`TruckPowerUpTests` checks the authored truck and debris pool, transformation, all-color smashing,
steering, pause/restart, three-second multi-hit exit shielding, bounded fragments, pickup refresh,
fighter/truck switching and chronological activation on long frames. It also checks the six saved tire-smoke
emitters, speed/steering response, stationary emission suppression, particle limits and pause/reset cleanup.
`CoinPickupTests` checks the saved 3D coin, pre-Play reward visuals, collection with every player color,
stable hitboxes, spin/bob, pause/restart, duplicate-score prevention and fixed-size pooling.
`MagnetPowerUpTests` checks the saved Shield/2×/Magnet models, separate power-up positions through recycling,
cross-lane coin attraction for every player color, range limits, Double Points stacking, timers/pause/restart,
pickup animation reset, airborne Magnet collection and chronological long-frame collection.
`SkyAndAudioTests` checks saved sky/sun settings, independent pickup/engine sources, non-silent PCM
clips and loop seams, engine mode/speed/pause/expiry/refresh/restart behavior, and coin-pitch isolation.
