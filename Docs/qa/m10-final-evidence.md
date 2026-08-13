# M10 Earth Core final evidence

Date: 2026-08-13

Unity: 6000.5.7f1

Render path: Windows Release, D3D11, 1280 x 720 windowed

## Automated gates

- Project validation: 10 milestone scenes, 14 abilities, 3 capability profiles.
- EditMode: 191 total, 191 passed, 0 failed, 0 skipped, 1.225 s.
- PlayMode: 64 total, 64 passed, 0 failed, 0 skipped, 125.823 s.
- NativeHigh, NativeLow and WebLab: 216,000 ticks each, 0 managed bytes, no canonical rule changes.
- Windows Development: succeeded, 171,123,394 bytes, 57.941 s, 0 warnings, 0 errors.
- Windows Release: succeeded, 106,003,631 bytes, 60.678 s, 0 warnings, 0 errors.

## Visual baselines

Each standalone scenario completed its own gameplay precondition and exited with code 0. Files are stored under `BuildReports/VisualQa/M10Final`.

| Scenario | File | Bytes | SHA-256 |
|---|---:|---:|---|
| Dawn / close exploration | `dawn.png` | 324110 | `5d2687425dcd4e03460984bf93999484125f6eb88c09e1dbfde266d72e4c4735` |
| Gravity grip / fracture | `gravity.png` | 382244 | `fdd119312e9a68a5dff7148235d0ce54165f35881a1c11f410691bbf27032c3d` |
| Mage cast / held mass | `mage_cast.png` | 403896 | `beee8217070aa305aad6d1f6fdf5984a7fbeafb264c9b30820bb533619c3648c` |
| Meteor impact | `meteor.png` | 418618 | `4e47f4ae3f460162d8f178afe499b961f4d17c44263086d522efe4d1da81197a` |
| Moving platform | `platform.png` | 404985 | `f23ca202781675e8e8586ed823f96a7c0b2d1da4715aaed4af3a09f3a4db9668` |
| Physical reassembly | `reassembly.png` | 404017 | `c99f952c6ee6a42d96b7655592227a6d2db02218b24cdee08593922e81e509f7` |
| Ground wave | `wave.png` | 539456 | `aeeecad0bee074e201ae4518146e076b726ac22b8584517b72fe1865233fec52` |

Visual review checked for a rendered world rather than a black frame, readable player/active magic composition, gross clipping, missing structures and missing HUD state. These images are regression baselines, not a substitute for a long interactive P95/P99 GPU capture or final art-direction approval.
