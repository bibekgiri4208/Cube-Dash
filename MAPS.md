# Standalone maps

**Main Menu > Start** now loads a separate scene for every map:

| Card | Scene | Retained Level scenery |
| --- | --- | --- |
| City | `Assets/Scenes/City.unity` | City neighborhoods, parks and flyovers |
| Jungle | `Assets/Scenes/Jungle.unity` | Jungle, river and landmarks |
| Mountain | `Assets/Scenes/Mountain.unity` | Mountains, snow and forests |
| Desert | `Assets/Scenes/Desert.unity` | Dunes, mesas and cacti |
| Beach | `Assets/Scenes/Beach.unity` | Coast, palms and islands |
| Country Side | `Assets/Scenes/Country Side.unity` | Linked Japanese countryside, now with the same highway/UI/sky system |

All scenes are enabled in Build Settings, with Main Menu still first. `Level.unity` is retained as the original mixed-biome runner, not replaced or renamed.

## What stays the same

The five new maps are copies of the saved Level rig, not new gameplay implementations. They retain the same player/collision geometry, camera, three-lane road, shoulders, guardrails, markings, road curves, pooled encounters, coins, power-ups, vehicle presentations, controls and sounds. Difficulty still increases with travelled distance through the same five stages; choosing Mountain or Beach does not start at a later difficulty or reset progression every scenery loop.

All six maps now use the Main Menu-themed Level HUD, compact distance ribbon, independent left-side power-up duration cards, countdown, pause, Game Over, retry and return-to-Main-Menu Quit behavior. Country Side's Japanese nature/architecture remain its distinct environment; see [COUNTRY_SIDE.md](COUNTRY_SIDE.md).

## What is separated

Each new scene retains only its chosen biome roots and ambience sources. Shared prefab/material/mesh assets are not modified. Track prefab instances are unpacked only within the new scenes so unused scenery can be removed without editing the original track prefab; nested player, pickup and retained environment prefabs remain linked where originally linked.

`EnvironmentDirector` opts into a saved fixed biome only in these five scenes. `EndlessTrack` retains its normal section indices and gameplay progression, but passes that chosen biome to scenery recycling. Lighting/sky/fog and ambience use that same biome at every distance. Saved map sky materials under `Assets/Maps/Skies` also give each scene its appropriate atmosphere before Play.

Scenery retains its varied interior layouts and reveal animations. Cross-biome edge dressing, fading terrain/river ends, City entrance/exit flyover ramps and Beach-to-City headlands no longer repeat every eight sections inside a standalone map. Main roads are unchanged. The original Level still uses all those transitions normally.

## Editing and safe reapplication

Open the individual scene to edit its layout. **Tools > Cube Dash > Separate Level into Five Maps** (`StandaloneMapScenes.ApplyFromCommandLine`) creates missing maps from Level, adds build entries and refreshes the six menu cards. It preserves already-separated scenes and their manual edits; it refuses to overwrite an unrecognized existing scene or orphaned sky asset. It copies Level before editing, so even interrupted authoring does not modify the original Level.

**Tools > Cube Dash > Add Main Menu Map Selection** refreshes the saved card targets, PLAY badges and navigation without rebuilding the submenu. Navigation follows the displayed three-column desktop/two-column portrait grid. Back/Escape/gamepad Back restore Start focus; selecting a map stops menu music, and in-game Quit returns to Main Menu.

## Validation

Passed **98/98** map/environment/ambience/road/progression/HUD/menu/music regressions and **2/2** GPU integration checks in an isolated Unity 6000.4.1f1 project. Rendered previews of all five maps and landscape/portrait map selection were inspected.

Saved-scene tests compare gameplay settings/references, player and vehicle hierarchy, camera, complete HUD hierarchy/callbacks, main road/Plane overrides, shoulders/guardrails/markings, coin/hazard colliders and power-up layouts against Level. Each new scene has one retained biome and a matching enabled build GUID. A seeded **3,780-metre** simulation of every map matches the original Level's hazard, coin, power-up and road-curve states, keeps the selected biome through recycling, and verifies pause/reset/countdown and menu return. Existing mixed-biome transitions and ambience tests still pass.

Reapplying the creation tool preserves scene/GUID/sky files byte-for-byte. During the initial five-map separation, the Main Menu audit changed only 15 existing UI documents, adding/removing none and preserving camera/scenery/artwork; original Level, the then-current Country Side and shared prefab/material assets were hash-verified unchanged. Scene/metas, map skies, menu, Build Settings and code were hash-verified against the tested assets before application; unrelated project settings were not transferred. Country Side's subsequent consistency upgrade and validation are documented in [COUNTRY_SIDE.md](COUNTRY_SIDE.md).
