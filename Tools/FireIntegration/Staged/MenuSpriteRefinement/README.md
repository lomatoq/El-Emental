# Main sprite refinement after actual capture

Viewed `BuildReports/CombinedVisual/Main-core-haze-ui.png`. Three staged source files only; no actual Assets/Unity calls. All before hashes match current actual source. Offline Presentation and Authoring.Editor compile exit0.

- Shift the shared menu column54→82 logical pixels. At4% hover, previous leftmost green extent was approximately54−52.5−12=−10.5, clipped by screen. New position leaves about17.5 logical pixels; header, element row, card and buttons keep their common parent alignment. Outer hit rectangles and row spacing remain unchanged.
- Center all menu captions inside equal100px side insets of the cream-face/hit rectangle. Green overflow never contributes to the text's center.
- Passive chevrons now lime green; selected cream chevrons remain dark. Real role icon colors/actions remain unchanged.
- Enable Box-filter mipmaps and Trilinear on the five new reference-art textures. Native dimensions, NPOT None, max4096, SpriteRects, border/inset values and source pixels remain unchanged. These2K images were rendered at40–400px using only bilinear level0, a clear minification alias risk. Filtering should suppress the etched glyph/rim and bright hatch speckles; **it does not mathematically desaturate or redesign the original artwork**. Remaining gold saturation must be judged on the next real capture.

Import `after` (3files), run existing Install Stone Artwork so the sampler change is applied, capture Main normal/hover plus Settings at1920×1080 and current wide aspect. Verify full tail, caption alignment, green arrows and filtered card/glyph edges. Do not claim saturation/art acceptance without that comparison. HUD owner was notified that its shared glyph textures receive improved minification; no HUD source/profile/layout was edited.
