# Menu alignment polish
Stage only; root owns Unity and actual imports.

- Footer uses four equal centered label slots (100 px wide; 110 px pitch), matching gold geometry rule at y=46. Selected outlined diamond sits under the active word center (x=50,160,270,380), not hardcoded left. 220ms smooth movement; Reduced Motion snaps and freezes subtle perimeter pulse. Rule has 2 logical-pixel thickness, no texture sampling.
- Main element label center now equals frame center (x=88 + 128*i). Water glyph compensates 1.8px left-heavy alpha crop; Fire moves 1px down for source padding. No atlas pixels changed.
- Active card icon moved to green chamber center (119,52 logical px), away from upper-left edge. Title and traits now share x=354 center and width 348.
- Root staircase 30px/row, Main no-autofocus and keyboard navigation code included unchanged in before/after.

Validation: offline Elemental.Presentation exit 0, no warnings. Elemental.Authoring.Editor exit 0 with existing unrelated obsolete API warnings; no changed-code warnings. Runtime/screenshots pending root. Inspect Main wide and 16:9, switch all four elements: diamond must settle below each word; card title/traits share axis. No gameplay or HUD/result edits.
