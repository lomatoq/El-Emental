# Overlay UI contour bloom
Two NEW files only. No actual Assets or Unity calls performed. Overlay canvas renders after URP postprocessing; this is a local multipletap shader blur of the actual source symbol alpha/brightness, rather than screen-space URP bloom.

Integration (keep references in array + card field):
```csharp
_elementBlooms[i] = UiContourBloom.Create(holder.Find("Element glyph").GetComponent<Image>(), new Color(.84f,1,.45f), 9);
_cardBloom = UiContourBloom.Create(_activeElementGlyph, new Color(.84f,1,.45f), 11);
// Inside existing selected-element update: no motion needed, same appearance with Reduced Motion.
_elementBlooms[i].SetStrength(active ? .95f : 0); _cardBloom.SetStrength(.8f);
```
Create once, never per frame. Helper places itself immediately behind source Image, follows exact rect/pivot/aspect/UV on sprite change, owns/disposes its local material. Tint may be changed only on selected element change. 24 blur taps + core sample, small padded quads, no render target/fullscreen pass. Bright alpha-derived contour only; original opaque core explicitly subtracted so there is no second visible icon. Atlas bounds reject neighboring symbols. Premultiplied transparency, UI depth test, stencil and RectMask clip supported.

CRITICAL CENTRING ROOT CAUSE: installed UGUI Image.cs lines810-825 preserveAspect uses pivot, so current Place() pivot=(0,1) aligns non-square glyphs upper-left. After Place on source glyph and card set pivot=(.5,.5) and compensate anchoredPosition by (width/2,-height/2). For card do this after EVERY current Place update, not just construction. This remains necessary regardless of bloom.

Validation: C# Elemental.Presentation offline compile exit0, no warnings/errors. Shader import/GPU and live visual QA still root-owned and pending. Check light follows silhouette, selected bright areas halo without opaque duplicate, atlas neighbor isolated, masks respected. Shared material stencil variants are created by UGUI; use standard Canvas UI masking. No performance threshold claim until runtime measured.
