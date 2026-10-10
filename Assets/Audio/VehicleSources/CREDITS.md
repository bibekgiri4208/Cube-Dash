# Vehicle audio sources

Both recordings are shared by **qubodup** on Freesound under **CC0 1.0**:
https://creativecommons.org/publicdomain/zero/1.0/

- `FlightEngineSource.mp3`: **Idle F-22 Jet Plane.flac**
  - Sound page: https://freesound.org/people/qubodup/sounds/187734/
  - Downloaded HQ preview: https://cdn.freesound.org/previews/187/187734_71257-hq.mp3
- `TruckEngineSource.mp3`: **Truck Engine Idle Loops.flac**
  - Sound page: https://freesound.org/people/qubodup/sounds/187564/
  - Downloaded HQ preview: https://cdn.freesound.org/previews/187/187564_71257-hq.mp3

The author describes both as extracted from US government video footage and released as public domain/CC0.
The pages explicitly permit copying, modification, distribution and commercial use without permission.
These are actual vehicle recordings, not the earlier generated oscillator sounds.

Cube Dash edits: mono conversion, rumble/hiss filtering, stable section selection, six-second looping
with a half-second equal-power overlap, DC removal and balanced RMS/peak normalization.
The processed files are `../FlightEngineLoop.wav` and `../TruckEngineLoop.wav`.
Rebuild them offline with **Tools > Cube Dash > Refine Vehicle Engine Audio**.
The source MP3 files are retained for reproducible edits; no network access is used during authoring or Play.
