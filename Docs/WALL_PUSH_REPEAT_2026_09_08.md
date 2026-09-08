# Wall shove strength and repeat repair — September 8, 2026

Baseline main: 05cfbb6a. The previous 1.43 m ordinary shove did not meet the requested feel.

Ctrl plus RMB still charges in place and launches on RMB release. A quick click uses the base impulse; one second of charging multiplies it by 2.5. Releasing Ctrl first cancels. Base impulse now caps at 24000 kg m/s with an 18 m/s light-wall limit; the released intact-body speed bound is 18–45 m/s. Mass-dependent drag remains. These supersede the older 12000 / 14–28 settings.

A fractured flag previously rejected even a standing connected wall. The new branch captures the largest surviving connected component, freezes its actual child bodies during charge, and releases its foundation bonds before distributing impulse by child mass. Interior bonds and prior damage remain; the retired root never reacquires their mass or collision. Fully disconnected debris remains individual debris. Cancellation restores prior child motion and does not rebuild support broken during charge.

Ground support now samples both the current foot and leading edge, including one physics-step lookahead. A bounded vertical support force helps follow shallow physical floor slopes; PhysX still owns all translation and blocking collisions. There are no position teleports.

Validation evidence is recorded in LandingRowEdit and WallPushPowerPlay reports and WallPushInput/tap-hold-range.txt. The focused Play suite covers actual paired keyboard/mouse input and occlusion, real arena support/obstacle response, three successive shoves after settling on the same physical floor, airborne behavior, and repeated charges on a damaged connected wall. No network pair, standalone build or GPU claim is made.
The initial stronger real-arena run exposed 6.1 cm floor penetration. Leading-plane velocity support removed that. The subsequent longer slide reached Arena_Column_SouthWest_INTACT, where a measured 8.7 cm collision recoil is legitimate. The input fixture now retains its 2.5 cm pre-obstacle jitter limit and 4 cm floor-penetration limit, identifies a blocking collider by actual narrow-phase overlap and opposing normal, and separately caps collision recoil at 15 cm. It does not waive floor errors or disable obstacles.

Final evidence: Edit 99/99 at 2026-09-08T16:09:20Z; Play 4/4 at 2026-09-08T16:19:37Z (55.807 s). Same 1668.881 kg wall: tap 4.944867 m, charge 22.26719 m, subsequent taps 5.412861–5.521204 m. Real arena 895.0063 kg wall: 2.488278 m before obstruction, 186 dust / 30 chips, peak held-push marker 0.0615 ms. Production scene restored after tests. Existing unrelated obsolete-API warnings remain; no new script warning introduced by these files.

WallPushInputPlay preserves the earlier diagnostic failure before the collision-aware fixture correction. WallPushPowerPlay is the final passing run and includes that same production input test.
