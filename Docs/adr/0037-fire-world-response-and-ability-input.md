# ADR 0037: Fire world response and local ability controls

Status: implemented and native-tested, September 9 2026. Edit38/38;9 distinct production scenarios pass across038/039. Final focused0395/5; broader standalone performance remains open.

The user's new request extends Fire from a directed demonstration to stone
destruction, force, soot, lift, projectiles, a ring and a ground swipe. This
supersedes ADR 0036's prohibition on moving or damaging stones through Fire
gameplay. Cosmetic gas parcels still have no independent damage authority.

`FireAbilityControls` produces typed intent without Unity dependencies. Fire owns
Space while selected: hold for progressive lift; Shift+Space produces one ring
per chord edge. Double LMB gives a hand bolt, subsequent taps within .30 seconds
give foot bolts. Holding both mouse buttons captures a ground swipe, committed
once on release after 18 pixels of travel and .7 metres of supported world span.
The single held LMB stream remains. UI, focus, death and element changes cancel
ownership; a held key interrupted by UI requires release before resuming.

`FireAbilityController` owns eight swept projectiles, twelve supported ground
nodes and one expanding ring. Hand/foot windups precede release at the actual
limb by .12/.24 seconds. Projectiles expire after 2.5 seconds; ring radius8m in
.65 seconds; ground nodes expire after3.5 seconds and follow their support.
Nearest solid cover blocks projectile, ring and ground contact queries; owner
colliders are excluded. Query saturation rejects unverified motion/contact.

`FireWorldImpact` receives only explicit authoritative contacts. A bounded
stable-ID/generation heat ledger prevents compound colliders multiplying force
or exposure within one fixed step. Full-energy thresholds are .35/.85/1.5s
for small/medium/large stones. Small matter enters the existing dust/archive
transaction; medium/large partition into two/three physical children. The parent
is retained if canonical partition admission fails. Controlled/returning matter
is preserved. Structure contacts use the existing structure impact router.
Push is a bounded mass-aware impulse, not per-particle Rigidbody creation.

The explicit shared scar pool follows contact-local coordinates and stable
generation, with fading seven-second soot. The saved pool's formerly empty
references are repaired by the scoped Fire installer. A prewarmed bounded
cosmetic adapter reuses the approved Fire atlas and volume shader for actual
feet, bolts, ring and ground nodes. No global runtime discovery is introduced.

`PlanetMotor` consumes local-up thrust in its physics step, with progressive
2–8m/s target speed, gravity compensation and real body ceiling sweep. Existing
fall presentation is used during lift; release preserves velocity and gravity.
Respawn keeps body scale1 and zero presentation lift from first visible frame,
retaining the terrain light wave and original materialization timing.

Native tests and actual rendered evidence must precede acceptance. Worst-case
cosmetic capacity is bounded but CPU/GPU cost requires measurement; no claim of
incompressible fluid simulation or standalone performance is made.
