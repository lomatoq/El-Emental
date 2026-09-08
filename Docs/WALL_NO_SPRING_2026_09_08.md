# Wall support without spring launch

Baseline: main cb7538ad. User reported walls popping out of the ground like springs.

The held-push support adapter previously converted negative probe gap into upward acceleration (penetration spring up to 80 m/s²), while slope assistance could retain upward velocity after leaving the slope. Remove the penetration spring. Only a sampled rising support plane can request bounded upward speed; flat support or lost support removes outward bounce velocity while keeping downward motion and tangential movement. The root remains physical and collides normally. Root overlap resolution is capped at 0.5 m/s; the authored-frame path also discards outward velocity. Detached fracture bodies retain their existing physics.

This correction does not add a new forward-toppling mechanic. Existing fracture/collision behavior is preserved; no ballistic lift is intentionally added to the intact wall.

Pure contract: SupportedRiseLimit and RemoveUpwardBounce in EarthWallPushMotion. New physical regression starts a fully charged wall with its base 8 cm inside a real floor and samples 160 fixed steps, rejecting any gap above 2.5 cm or contact-resolution upward speed above 0.6 m/s. Existing ramp, repeat, heavy-contact and production keyboard cases also run.
Validation: Edit137/137 at2026-09-08T21:49:28Z; final Play10/10 at2026-09-08T21:57:04Z (139.89s). Initial Play9/10 exposed excess flat-ground pressure reducing tap range; restored the previous flat pressure without restoring the spring. Final buried test peak gap0, peak upward speed2.98e-8m/s, airborne samples0/160. Tap4.944867m, charged10.24167m, repeated taps above5.4m. Ramp travel6.63857m, peak gap0.05268097m, zero samples above15cm. Production marker peak0.0853ms. No new toppling mechanic or standalone build is claimed.
