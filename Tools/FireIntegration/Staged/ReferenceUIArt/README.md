# Reference UI art candidates

Current chosen panel: panel-selected-alpha-v1.png, generated from the user's explicitly selected target-10-selected-panel.png. Actual RGBA1024x1536 with alpha0..254; source exec-76f1e504-8005-4f76-96f5-ce666615b6b2.png. This replaces earlier panel candidates in the actual reference curtain while retaining its asset GUID. Slight redrawing and a soft exterior gold glow remain visible differences from the source; final game composition is still under review. The source reference remains unchanged. Never substitute the RGB checkerboard outputs.

Generated using built-in ImageGen from user reference target-7-panel-buttons.png. The user explicitly authorized custom asset generation and selected the reference style.

button-cream-v1.png: actual RGBA2007x783, alpha0..255. Solid-alpha bounds in top-origin image pixels: x29..1977,y222..508; faint alpha extends x27..1979,y17..669. Import a sprite rectangle around the button itself, not the full padded canvas. Keep text, role icon and chevron as live UI elements. Generated source: exec-92f206ef-6e0a-4dd3-a113-2ca44abff00e.png in Codex generated_images. Visually reviewed as suitable candidate; actual game composition still requires review.

panel-v1-rejected-alpha.png: rejected RGB image with baked checkerboard. Do not import. Alpha-correction output exec-839b5cc8-c862-416d-9bcf-42a5b21f1752.png also remains RGB and rejected. A new generation requests actual alpha without referencing the failed checkerboard image. Exact panel prompts are preserved separately.
