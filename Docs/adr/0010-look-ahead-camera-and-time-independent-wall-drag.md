# ADR-0010: Look-ahead camera and direct ground wall drag

Status: accepted  
Date: 2026-08-12

## Context

The first free-cursor camera looked almost horizontally through the character and hid the ground where Earth gestures begin. Wall recognition then required a mostly horizontal screen stroke and passed through a second generic recognizer on release, so a visibly valid footprint could still be rejected. A wide acquisition sphere could also grab a nearby rock before drawing began.

## Decision

- `PlanetCameraFramingSolver` keeps camera position, look focus and occlusion anchor separate in the local gravity frame.
- The Earth slice uses an elevated shoulder camera and a focus ahead of the character. Forward speed adds bounded extra look-ahead.
- `PlanetCameraRig` smooths position, heading and focus independently in `LateUpdate`; it remains presentation-only.
- Starting LMB on terrain and moving a small finite viewport-normalized distance enters wall drawing in any screen direction. The projected ground path is the wall footprint.
- The unified Earth wall bypasses the older generic gesture classifier at commit. The same direct contract controls both preview and commit.
- Existing rock acquisition uses an exact raycast. A loose rock is grabbed only when the player actually points at it; nearby rocks do not steal a terrain wall gesture.

## Consequences

The ground casting area stays visible. A player draws a wall by pressing terrain, dragging along the desired footprint and releasing, without a direction quiz or hidden timing window. A still press continues to form a rock, and a precise press on a loose stone acquires it.
