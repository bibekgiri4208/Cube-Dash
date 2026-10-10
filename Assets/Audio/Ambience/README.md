# Biome ambience

The four source MP3s were provided for this project. They are retained unchanged; no third-party
license or authorship is inferred here.

`Loops/` contains offline-edited stereo WAVs derived from these recordings:
- `jungle ambience.mp3` → `JungleAmbienceLoop.wav`
- `snow mountain ambience.mp3` → `MountainAmbienceLoop.wav`
- `desert ambience.mp3` → `DesertAmbienceLoop.wav`
- `beach ambience.mp3` → `BeachAmbienceLoop.wav`

Up to 60 seconds of non-silent recording is retained per loop, with a two-second equal-power
tail/head overlap, DC removal, stereo-linked soft peak control and matched 0.1 RMS bed levels.
Stereo channels and
natural pitch are preserved. Unity streams the loops as quality-0.8 Vorbis in builds, avoiding
four large fully decoded audio buffers. No runtime synthesis or network access is used.

The level has four dedicated non-positional stereo AudioSources for an enveloping background,
not an encoded multichannel/ambisonic surround mix. Only audible biomes run. City has no supplied
recording and remains silent. Equal-power crossfades use exactly the same smoothed distance blend
as the sky and lighting, including Beach → City and City → Jungle. Pause freezes playback and
the fade; resume continues it. Game over gently fades out; restart and scene cleanup stop/reset it.

Tune **Volume**, **Fade In Seconds** and **Fade Out Seconds** on the game object's **Biome Ambience**
component. Scenery crossfade width follows **Track > Environment Director > Transition Distance**.
Rebuild/reconnect with **Tools > Cube Dash > Apply Biome Ambience Audio**.
Pickup cues, coin sounds and vehicle engines use separate sources and remain unchanged.
