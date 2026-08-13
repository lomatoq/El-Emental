# M7 Missions

Status: complete

## Gate

Implement reusable mission/objective/crisis/score contracts, six crisis primitives, Volcano Village, civilian proxies, performance-aware escalation, replayable outcome scoring, debug timeline, deterministic seed control, and three materially different scripted success strategies.

## Delivered evidence

- Pure contracts cover MissionDefinition, ObjectiveGraph, CrisisEvent, EscalationCurve, SpawnRule and ScoreRule.
- A seeded bounded director composes LavaAdvance, StructuralFailure, SmokeHazard, CivilianPanic, BlockedRoute and TimedEvacuation primitives.
- Civilian proxies use two-route steering, danger scoring and explicit waiting/evacuating/rescued/lost states.
- Volcano Village contains two routes, fourteen structures, destructible route blocks whose destruction can help or hurt, pooled crisis presentation and a UI Toolkit mission/debug HUD.
- Win, partial success and failure derive from the same simulation; score breakdown records rescue, loss, structure and time terms.
- Focused EditMode is 6/6 and PlayMode 1/1. Earth fortification, Air evacuation and Water cooling all win the same mission, fixed seeds reproduce the exact event timeline, and escalation remains within the 12-event budget.
