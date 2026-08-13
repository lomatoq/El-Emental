# ADR-0008: Charged Earth pillar mobility and mouse-facing control

Status: partially superseded by ADR-0009  
Date: 2026-08-12

## Context

The inherited motor performed an immediate generic jump and only corrected local up. It did not turn toward the mouse, while the follow camera derived its orbit from character forward. Turning the character and camera from the same transform produced unintuitive feedback. Earth mobility also needs visible material cause without synchronously remeshing the voxel planet.

## Decision

- Space press begins one bounded Earth mobility charge while grounded; release commits it. A 1.35 second hold reaches the cap.
- `EarthPillarLaunchSolver` is pure data. Charge continuously maps pillar height from 1.5-7.2 m and target upward speed from 7.5-19 m/s.
- `EarthPillarMobility` owns the bounded physical launch sequence and suppresses grounding during it. The original delayed single-impulse implementation is superseded by ADR-0009's continuous carry.
- `EarthPillarFeedback` reuses one authored low-poly pillar and twenty cosmetic ground chips. The pillar is a bounded presentation cache, so mobility does not trigger an SDF edit, mesh rebuild or one-Rigidbody-per-piece cost.
- The locked-pointer orbit and virtual cursor decisions are superseded by ADR-0009 after playtest feedback showed that sharing the mouse between camera control, wall drawing and object selection was not usable.

## Consequences

Tap and hold share one Earth-specific mobility verb, with `LIFT` charge visible in HUD. The pillar is not canonical persistent terrain and cannot yet be used as a lasting platform; persistent terrain pillars require a separate bounded structure contract.
