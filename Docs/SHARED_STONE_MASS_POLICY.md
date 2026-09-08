# Shared stone mass policy

Implementation: September 6, 2026; uncommitted main based on `1235579`.
Unity 6000.5.7f1 compiled the change; the coordinator installed the asset and
saved production scene bindings. `SharedMassPolicyEdit` passed **7/7** at
15:42:36 UTC (0.122s); `SharedMassPolicyPlay` passed **3/3** at 15:44:36 UTC
(10.904s). These are suite durations, not per-frame performance measurements.

`Elemental/Setup/Install Shared Stone Mass Policy` creates
`Assets/Elemental/Content/Profiles/EarthMatterMassPolicy.asset`, binds the active
scene's explicit matter kernel and existing owners, and saves the scene. It
rejects ambiguous multiple kernels and requires the authored debris pool.
`Elemental/Tuning/Impacts & Stones` edits this real asset and previews mass,
transferred impulse and character response. Policy changes apply to the next
Play session. Each kernel captures an immutable pure-data snapshot; changing an
asset does not silently reprice live bodies or canonical records. Unauthored
standalone fixtures retain the built-in deterministic ArenaStone default.

Default physical density is 2300 kg/m³. The reference conversion is 230 physical
kg to 120 gameplay kg, exponent .68, bounded 12–1800 gameplay kg for newly
created matter. Solid volume remains independently stored in m³. Two new stones
with the same solid volume use the same gameplay mass, regardless of source.
Parent fragments may be lighter than the new-matter minimum: splitting never
applies the minimum or nonlinear conversion again.

Creation routes now use the shared snapshot:

- Decor collider volume and authored arena-piece volume.
- Terrain extraction, spawned hero stones, quick punches and bot projectiles.
- Meteors, walls, platforms, pillar cells, surf boards and physical armor plates.
- Accretion adds `F(oldVolume + addedVolume) - F(oldVolume)` to the existing mass.
  Multiple cosmetic chip deliveries therefore cannot repeatedly add a minimum
  stone mass. Reserved pending deliveries count against the accretion budget.

`EarthFragmentPool.Acquire` and `EarthFragment.Initialize` still accept already
resolved gameplay kilograms; they do not normalize caller-supplied child mass.
Fragments and pillars no longer infer volume by dividing compressed mass by a
legacy density. Existing legacy density fields remain serialized for content
compatibility, but are hidden from tuning inspectors and do not drive creation.

Held-boulder fracture reserves capacity, creates unregistered shells, and uses
one canonical `Registry.TrySplit` before rebinding all four children. Failure
retains the unchanged source. The source shell becomes one child; it no longer
retains a full parent record alongside newly minted child records. Every child
inherits exactly one quarter of parent volume and mass. Persistent debris keeps
its existing volume-fraction partition and registry split/merge checks. Wall and
platform physical pieces now receive the same mass shares as their canonical
records; the retired wall shell is kinematic and carries no meaningful mass.

Accretion updates both canonical mass and volume. Its provenance keeps the total
reserved terrain volume, but clears the claim that all added material belongs to
the first exact source cavity. Returning or splitting the result therefore reads
the grown canonical volume rather than reconstructing it from mass.

The character damage/impulse normalization and existing transfer multiplier are
unchanged. This change fixes the masses supplied to that normalization. Cosmetic
landing-cushion chunks have no Rigidbody/damage target and do not enter this
contract. Existing mesh authoring and animation clips are unchanged.

## Focused acceptance

- `Elemental/QA/Shared Mass Policy Edit Tests`: seven policy/binding tests,
  including immutable per-world snapshots, monotonicity, invalid geometry,
  delivery-count-independent accretion, split/merge and rejection of children
  that were incorrectly normalized again.
- `Elemental/QA/Shared Mass Policy Production Tests`: production decor/wall
  physical/canonical mass equality; actual extraction followed by canonical
  accretion and held split; existing persistent-debris conservation/lifecycle.
  Reports: `BuildReports/SharedMassPolicyEdit.*` and `SharedMassPolicyPlay.*`.

The accretion Play assertion tests the reserved-volume adapter separately from
cosmetic chip timing. It is not evidence for an end-to-end multi-cavity return
transaction. The installed production scene and focused tests passed; broad
play-feel calibration across all source sizes/speeds remains separate from the
mass/volume conservation evidence above.
