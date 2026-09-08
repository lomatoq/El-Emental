# Duel HUD readability and design system — September 6

**Superseded by the user's restoration request:** the premium appearance below
is historical. See [Original HUD restoration](HUD_ORIGINAL_RESTORE.md) for the
current original layout with only a 12% increase in explicit font sizes.

Working tree based on main `1235579`; all changes remain uncommitted.

## Implemented native UI

The production EarthDuelHud remains a UI Toolkit document with Painter2D gauges
and a planet globe. Existing health, mana, scoring, countdown and restart owners
are retained. There is no HTML overlay, browser runtime or third-party UI package.

- Health/mana values increase from 12 to 38 reference pixels; timer 34 to 52.
- Captions increase from 9–10 to 16 reference pixels. Existing bold font style
  is reinforced with a bounded 0.4px matching outline on primary numbers.
- All values and score teams have a dark contrast backing. The same 8px corner,
  cream text, subdued gold edge, coral red and cyan blue repeat across the HUD.
- Existing side gauges still fill upward. Their drawing reads accent/outline
  custom properties from USS, so the palette can be changed in one file.
- The local team is explicitly marked YOU. The clock changes to coral during
  the last 30 seconds; health at or below 25% gains a coral value and border.
  These cues do not pulse, flash or shake.
- Respawn and round-result actions use the same vocabulary, with 22px respawn
  text and a 54px-tall restart button. Empty respawn notifications are hidden.
- Rebuilding the HUD clears old numeric caches, so a re-enabled document cannot
  keep initial labels while the game state has already changed.

## Editing the visual system

`Assets/Elemental/Content/UI/EarthDuelHud.uss` owns tokens under `.duel-hud`:
`--duel-ink`, `--duel-muted`, `--duel-gold`, `--duel-line`, `--duel-surface`,
`--duel-red`, `--duel-blue`, `--duel-caption-size`, `--duel-value-size`, and
`--duel-radius`. Component classes own spacing and geometry. No prefab or scene
rebuild is needed to adjust these values.

The existing panel scales against a 1920×1080 reference with height matching.
At 720p, the 38px vital values render at approximately 25px; the 52px timer at
approximately 35px. The UI leaves the center of play clear. The mana panel and
navigation backing have an explicit nonoverlap gate in the capture fixture.

## Baseline and service attempt

Inspected the existing production screenshot
`BuildReports/DuelHud/gameplay-hud.png`: tiny vital values and unbacked captions
were visible against bright arena/sky colors. Archived copies are in
`BuildReports/HudPremium/Before`. This is historical baseline evidence, not a
fresh post-change audit capture.

Read the installed Superdesign skill and attempted its mandated bare CLI
preflight. It failed with EPERM while creating npm cache. One retry with a
permitted temporary cache failed with EPERM reading `C:\Users\nirrt`.
Stopped at the skill's one-retry limit. No service generation, remote canvas,
external font download or authentication occurred. Native implementation
continued independently from the inspected project source and screenshot.

## Fresh validation — 01:42 UTC

The integrating task owns Unity and ran the production fixture in
`BuildReports/SeptemberPremiumPlay.xml`. No refresh, Play Mode or scene save was
triggered by the HUD worker.

The HUD fixture **passed 1/1 in 4.275695 seconds**. The combined integration run
is 5/7, with separate charge-camera and arena-descent failures; those are not
waived or covered by the HUD result.

The existing production test
`EarthDuelHudPlayTests.SavedHudRendersAndTracksHealthManaScoreAndRoundRestart`
captures into `BuildReports/HudPremium`, preserving the old folder. It already
checks actual accepted shot/mana, HP75, score/death, round result/restart, HUD
CPU marker, and production gameplay camera compositing. The fixture now also
requires resolved 36px-or-larger vital text, 48px-or-larger timer, and separate
mana/navigation bounds at 720p, 1080p, ultrawide and 4K.

Fresh files were produced at 01:42:00–02 UTC. Inspected the actual gameplay
composite at 1920×1080 and the 1280×720 panel directly. Also inspected the
2560×1080 and 3840×2160 captures, resized by the image viewer for full-frame
layout review. Values/timer are visibly heavier and readable, text fits its
backing, side panels retain safe margins, and mana/navigation do not overlap.
The game view remains open around the character and aiming area. No follow-up
source correction was required by this visual review.

`BuildReports/HudPremium/evidence.txt` records 60 HUD Tick CPU frames:
mean **0.02351 ms**, p95 **0.02830 ms**, max **0.05480 ms**. This is the
`Elemental.DuelHud.Tick` marker only, not UI rendering or whole-game frame cost.
The screenshots establish the active gameplay HUD layout. They do not claim
visual coverage of every transient result/respawn/warning state.
