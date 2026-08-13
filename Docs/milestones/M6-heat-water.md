# M6 Heat + Water

Status: complete

## Gate

Implement sparse thermal regions, an ADR-selected bounded water representation, hysteretic phases, heat/mass/phase/pressure operators, six vertical-slice abilities, state-driven reactions, presentation proxies, conservation telemetry, ElementLab, and scripted cross-element replays.

## Delivered evidence

- `ThermalWorld` provides bounded sparse regions with temperature delta, transfer coefficient, lifetime, tags, bounds, priority, fixed-rate scheduling and visible update/query debt.
- ADR 0002 selects canonical bounded `WaterVolume` records after comparing shallow cells and gameplay particles; presentation density is independent of authority.
- The enthalpy-based `PhaseState` model preserves latent energy and supports hysteretic solid/liquid/gas transitions.
- All eight requested operators and all six compiled Fire/Water abilities are executable through the command/event/replay path.
- `ReactionResolver` implements state-threshold rules for water+cold, water+heat, hot brittle material+rapid cooling, air+steam, and fire+fuel.
- UI Toolkit HUD, liquid/spray/steam particle proxies, phase materials, ice collision bridge, heat light and reaction impulse adapters read canonical state without owning it.
- `ElementLab.unity` runs a six-command cross-element replay. Focused EditMode is 6/6 and PlayMode is 2/2, including 10,000 transfers, 64-region budgets, reversible phase cycles, all operators, zero canonical mass/energy error, and bounded steam/pressure responses.
