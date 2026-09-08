# Very-far chromatic light fringe and tonal stipple

Three staged existing sources; no actual Assets/Unity changes. Parent-owned profile asset and its haze/height/density tuning remain untouched.

Only upper opaque geometry beyond1000m; smoothstep to1800m. Existing playable-window/planet protection remains multiplied in; no effect on sky, near arena, avatar or overlay UI. Existing distant chromatic .65px becomes1.3px at1080 (hard1.5px cap), positive lightward R/B difference only, maxgain.42 times distance/day masks. Neighbor depth guards also require>1000m. Black/dark RGB edge halos are not introduced. This replaces previous400m subtleCA start with explicit very-far gating.

Static7px-at1080 jittered stipple cells: soft derivative-filtered dots, broad sine tonal envelope, local-color multiplicative variation[-1.26%,+1.8%] atfull distance/defaultstrength. No time/frame input; reducedmotion naturally static. No black dots, bands, animated noise, new textures or fullscreen passes. ExistingGamma source values are adjusted directly; no double color-space conversion. Effect runs before analytical clouds and existing sun dust; particleclouds subsequently draw through original owner.

Controller optional `FarArtEnabled` defaulttrue and `FarStippleStrength` .018, clamped0–.03. Existing FarChromaticPixels0 still disablesCA. No profile schema/file edit.

After baseline check/import/refresh: inspect shaderconsole (offlineC#compiler does NOT validateHLSL). In ready Main then Combat Play run `Elemental/Graphics/Capture Very Far Art On Off`; copy Logs/VeryFarArt before next capture overwrites. Captures1920same camera/lighting synchronously, restores owner flag/target/resources. Inspect farupperislands at100% and compare nearsilhouette/reticle area forzeroeffect; any TAA/post accumulation may complicate strictpixeldiff. Reverse/underprotected existingQA remains available. Stronger shader cost notmeasured; noGPU claim. Artistic acceptance pending actualcapture; lower FarStippleStrength or disableFarArt if dotpattern becomesvisiblegrain.

Reports/far-art-contract.json is a numerical contract audit (not actualGPU): distance threshold/monotonicgate/bounded localtonalrange/no time input verified. AllthreeUnity C#assemblies compileoffline0. Baseline hashes preserved.
