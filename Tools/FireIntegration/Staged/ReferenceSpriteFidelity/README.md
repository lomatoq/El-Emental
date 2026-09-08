# Reference sprite fidelity — staged import

Scope: latest user button crop `62871bd5`, element row `3a8f8313`, footer `cdb97909`, active card `a3f989e6`. This is a reviewable implementation, not a claim of pixel fidelity without Unity captures.

## Import ownership

- Import only `after/`: 13 files, five PNGs and eight C# files. `import-manifest.json` records actual-before and staged-after SHA256. All five modified actual files matched before snapshots at freeze.
- Root owns Unity. This lane never imported Assets, refreshed Unity, edited scenes, or launched Play.
- `StoneReferenceHudPresentation.cs` is **not** in `after`. Its result-button changes are in `merge-inputs` and handed to `/root/fire_runtime`; that agent owns the final combined HUD file and optional exact HUD profile. Import their final file, not this merge input.
- Run existing `Elemental/UI/Install Stone Artwork`. Its new helper imports native textures with NPOT None, max4096, clamp, no mipmaps, uncompressed, sRGB, alpha transparency; creates/preserves standalone Sprite asset GUIDs. Old source artwork, fonts, scene layout and actual callbacks remain.

## Concrete changes

- All five menu pages use the common new dark passive plate with a fine gold perimeter and no large gold corner triangles. Disabled buttons use the same plate dimmed.
- Hover/focus/press uses the **whole** cream plus green ribbon construction, including the lower-left tail. `Press Visual` remains the stable face-sized visual parent; `Button artwork` alone expands left/down using source-pixel insets `(210,237,2,21)`. Nine-slice protects the full left construction and end bevels while stretching only the long center. The hit rect remains560×78 and visual hover4%, without moving neighboring rows.
- Text and role glyphs remain live inside the cream-face rectangle. Filled vector silhouettes map real Host→people, Join→person-plus, Settings→gear, Quit→door, Bot→concentric diamonds. Back/copy/ready retain their existing real roles.
- Button hover glow and selected-element breathing drive rim-local light using CanvasRenderer alpha, not a radial image or per-frame vector mesh rebuild. Native mesh color remains static while pulsing.
- Fire/Earth/Water use three native SpriteRects in a colored glyph atlas. Air is a separately cleaned RGBA icon; the unused atlas Air slot was rejected for extraction debris.
- Element status diamonds use thin colored geometric rims and selected1.24 scaling. Active card includes its full slanted green construction and actual selected-element name/traits. The native card is594×133 logical pixels; page origin shifts484→552 to leave its tail clear of first controls. Main row gaps remain12; Quit ends at logical1022 with the existing panel offset, inside1080.
- Footer uses an outlined diamond with edge light and a gold rule starting at its right point; no radial blob or opaque center square.
- Handed-off result buttons use these same sprites and visual-only hover expansion. Their real Button.text, callback and restart permission remain authoritative; the decorative caption mirrors live text, including WAITING FOR HOST.

## Artwork provenance and alpha evidence

Built-in imagegen **edit mode** derived text-free art from the user's four local references. No Python raster edits, threshold masks, baked-background crops, or external media downloads were used. Python/PIL only read dimensions/alpha or copied bytes. See `art-provenance.json` for original generated paths, SHA256 and dimensions.

All five accepted PNGs are RGBA with alpha0–255. Fully transparent pixel fractions: selected construction57.53%, passive64.85%, card57.02%, glyph atlas77.49%, separate Air65.25%. This verifies genuine transparency, not visual-perfect extraction. First RGB/checkerboard candidates and RGB cleanup outputs were rejected; they are never imported.

Prompts: reconstruct complete selected cream face **plus green left/down ribbon**, remove all text/role/chevron; reconstruct passive low charcoal chamfered plate with thin gold edge and no large gold triangles; reconstruct entire green active card preserving separator and tail but removing text/icon; reconstruct four colored glyphs without frames/labels; extract Air alone after atlas Air cleanup failed. Background-removal edits explicitly required genuine RGBA and no drawn checkerboard. Native source dimensions and crop rectangles are explicit in `StoneReferenceSpriteInstaller`.

## Verification and required visual acceptance

Offline Roslyn compilation passed Presentation, Authoring.Editor, Tests.EditMode, Tests.PlayMode. Existing unrelated obsolete API warnings remain; this is not a Unity execution or performance claim.

Run `ReferenceSpriteGeometryTests` after installation: native dimensions/NPOT, genuine source alpha, positive cream-face geometry and green overflow. Run existing stone skin installer idempotence tests and StoneSkin Play suite. Updated Play assertions check the actual artwork child, unchanged hit rect,4% hover, native2138×736 selected texture UVs and left/down overflow rather than treating the cream crop as the whole sprite.

Capture1920×1080 Main/Settings/Host/Join/Pause/Pressed/Combat/Victory/Defeat/Draw, then a narrow/wide Main. Required visual checks:

1. Selected green tail survives whole, behind next row at the left; no text shift or sliced cream clipping. Passive bevel/texture is thin and quiet, with correct filled roles.
2. Card icon occupies left green compartment; both live text rows stay inside their respective divisions. No overlap with element labels or first button after moving page origin.
3. All elements have colored glyphs, brighter larger active Earth and light confined to diamond edges. Inspect atlas Fire/Earth/Water edges at actual display size; those remain generated approximations, not traced originals.
4. Footer hollow diamond and rule stay aligned at normal and wide aspect. No prior rectangular veil returns.
5. Result visual text still updates NEW ROUND/RETRY ROUND/WAITING FOR HOST and clicks trigger original operations. HUD layout acceptance belongs to the merged exact-HUD lane.

If any of these fail, iterate on actual captures before reporting completion. No frame captures have been fabricated in this staging-only lease.
