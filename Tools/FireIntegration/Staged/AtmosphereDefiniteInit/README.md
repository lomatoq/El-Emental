# Definite initialization warning

Most likely source of FXC's `potentially uninitialized variable (FragAtmosphere)` is the analytical cloud fallback's two local arrays: `travel[4]` and `art[4]` are filled via an unrolled loop and an indexed `out` argument, then dynamically sorted/read. Every logical element is written, but FXC's definite-assignment analysis can lose that fact across indexed out/inlining. All ordinary scalar branches inspected in FragAtmosphere/ApplyValleyAtmosphere have assigned values.

Tiny fix explicitly initializes four travel values to−1 and four colors tozero before the existing loop. No rendering behavior or feature changes. Compiler hypothesis must be verified by clearing/reimporting actual shader warning in Unity. No shader compiler available to this staging agent and no Unity calls made.
