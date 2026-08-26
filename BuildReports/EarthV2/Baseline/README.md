# Earth Core V2 baseline

Captured: 2026-08-14  
Commit: `7bdce0dac855007ec14e1f7114a6af37e563614a`  
Unity: `6000.5.7f1` (`017862109af0`)  
Player: `Builds/WindowsRelease/ElEmental.exe`, 1280×720, D3D11

## Automated baseline

- EditMode: 191 passed, 0 failed, 0 skipped.
- PlayMode: 64 passed, 0 failed, 0 skipped.
- Windows Release: succeeded, 106,003,631 bytes, 60.68 s, 0 warnings/errors.

## Captures and SHA-256

- `day-explore.png` — `B3844FBE3D3D558A39307C38169B058FFF8D41158494C2E99D9BD2C0BBC47541`
- `wall-rise.png` — `867110BD441C6CFBCE8DF2641CF20601470FBF847D135C4F8A29E4DD47590737`
- `legacy-wave.png` — `9F4F3F80DD4F1A85CB1C23B60E7BE04AAD71EEB1643FE62F6CC7AF4124746030`
- `platform-max.png` — `AE3A2ADB1C15A5E250BFBCCA1B7E96BCF06276CAC2803FF7FDA62C4CA3CB4BA4`
- `locomotion-cast.png` — `53B658D9C1A49A079EEA9AA2A817DCCD600BDE9060FD1F8868462D00097788D6`

## Baseline findings

- Day exploration is dominated by black sky; celestial disks do not establish daylight.
- Camera is low and directly behind the character, hiding near-ground technique results.
- Wall reads as a view-filling slab and its old physical root moves during emergence.
- Legacy wave has large gaps and reads as independent pillars rather than a traveling crest.
- Platform and locomotion/cast frames have weak silhouette and state readability.

These files are evidence only; they are intentionally not treated as target art.
