# Diagnose authoritative stone acquisition and launch

Run-20260907T093755: host accepted 1518 motor and 1642 control packets, no rejects/gaps. Host shots array was empty because it recorded only a pre-router primary edge with QuickPrimed plus ReservedOrHeldFragment. Client recorded 24 clicks, all unprimed; local HeldBody absence is expected for a replica. Actual LooseStone responses exist on both peers, but none were attributable to the probe shot. This evidence does not establish a damage bug.

The probe also walks forward for one second, bringing actors almost into contact. Ground seed is then 2 m toward the opponent, frequently beyond it. Fire diagnostics show negative target distance along the parallel ray and a 1.2-3.4 m miss, using actor COM because no stone was held. These are warning signs in probe geometry, not measured launched projectile misses. Do not claim otherwise.

One development-only C# overlay: record every host primary press/release, actual first camera-ray collider (diagnostic broad ray, not the gameplay target selector), route owner, phase, pending extraction, stun and component state. Bounded actor-two status/rejection/spawn/launch event records include input sequence, source ID, mass, actual launch point/direction/speed. Attribute source IDs from actual actor-two authoritative FragmentLaunched events after input, not a pre-router primed snapshot. Existing nonzero source/LooseStone/target-one/Knockout/same replicated response and life-preservation/menu gates remain unchanged. Event handlers are removed on destroy.

No changes to input timings/aim/movement, physics, damage, world, networking transport or normal game behavior. No source-zero acceptance, forced spawn, teleport or injected damage. Use the next host abilities array to distinguish rejection, cancel/stun, missing routing and actual launch before changing gameplay. All data remain developer-probe opt-in.

Offline Elemental.Online compile passed with no warnings. Actual new pair is coordinator-owned and requires a fresh build. Apply stone-trace.patch after baseline.json verification; before/after provided.
