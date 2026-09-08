# Final HUD ornaments, staged only

Four files based on actual post-UIFinalFollowup source; no Unity calls or actual Assets edits. Offline Presentation/Authoring.Editor compilation passed. SHA guards and ornaments.patch supplied.

- Reference profile gets optional hudReadableFont, populated by installer only when empty from existing Assets/TextMesh Pro/Fonts/LiberationSans.ttf. Original font assets/theme/manual HUD layout remain unchanged. Combat captions/numbers use this existing sans face in bold; reference serif result title and menu headings remain. Result score uses readable bold numerals. Static TMP LiberationSans SDF has runtime sourceFontFile null, hence a direct optional Font reference is needed instead of relying on TMP sourceFontFile at runtime.
- Wheel receives a non-picking gold curved line with two end dots behind existing live element tokens. Earth moves20px upward. Selected element glow/state unchanged.
- Existing score sides get gold rule/inner corner/detail. Existing gauge visual receives non-picking top/bottom gold caps; actual fill geometry/current/trail/mirror logic untouched.
- Unchecked Reduced Motion square gets2px gold outline and dark inset; existing Toggle/check sprite/listener retained.
- Existing true globe and data source untouched; no fake terrain added.

Next: hash-check/copy only these four afterfiles after Unity lease transfer. Refresh/wait actual DLL update; run Elemental/UI/Install Stone Artwork once to bind existing sans Font. Run existing Stone Skin Play to retain behavioral checks and capture fresh Combat/Settings/Victory. Inspect whether bolder timer still fits plate, value/caption readability, arc passing behind tokens, Earthlabel spacing, goldcaps following gauge sides and checkbox outline. These visuals have not been rendered yet and are not claimed accepted.
