# Save the initial arena before physics starts

Cause: EarthSceneReadinessGate released its timeScale pause once ground/debris cooking was ready. EarthPlanetRockScatter still builds 184 gameplay stones at 4 per frame, then 128 cosmetic clusters at one per frame. CaptureArenaBaselineIfReady explicitly refuses incomplete scatter. Its callers (ShowMain, RestartRound, SetRoundReady) are one-shot boundaries, so the first successful capture could occur after an authored boulder had already shattered. The 09:18:58 production trace showed an invalid restored parent and no live family; XML ordered scene ready before scatter complete.

Patch: include active owning-scene scatter completion in startup readiness; synchronously capture active authority duel baselines before IsReady and before RestorePause. Scope discovery is cached and only performed during loading. Fail explicitly if the baseline cannot be captured. Existing scatter placement, mass, fracture, scene and rendering are untouched.

Production test now requires no canonical boulder collisions while startup is pending, complete scatters and a captured baseline at readiness. Existing strict full EndMatch restored identity/provenance/mass/volume/grip and foreign-scene assertions remain unchanged. Ordinary KO still verifies the lawful source family.

Offline Runtime and PlayMode compile passed. Runtime has 7 existing CS0618 warnings outside this overlay; no new warnings. Actual PlayMode acceptance remains coordinator-owned. Apply startup-baseline.patch after verifying baseline.json; 3 existing C# files only. No Assets files were modified by this staging task.

Public diagnostic: EarthMvpDuelController.ArenaBaselineCaptured. On successful game verification record startup includes procedural scatter plus baseline before physics in PROJECT_TECHNICAL_STATE and PROJECT_EXECUTION_TRACKER; do not label the correction accepted before running production tests.
