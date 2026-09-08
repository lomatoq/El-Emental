from pathlib import Path
p=Path('Tools/FireIntegration/Staged/ReferenceSpriteFidelity/after/Assets/Elemental/Presentation/UI/FrontendMenuView.cs');s=p.read_text(encoding='utf-8').replace('Place(footerRule.rectTransform,0,46,430,1)','Place(footerRule.rectTransform,44,46,386,1)');p.write_text(s,encoding='utf-8')
