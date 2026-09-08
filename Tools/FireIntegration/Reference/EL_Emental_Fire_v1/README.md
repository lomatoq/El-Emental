# EL Emental — Fire reference kit v1.0

Пачаць з **CODEX_HANDOFF.md**, пасля прачытаць **Docs/TECHNICAL_DESIGN_BE.md**.

Пакет змяшчае дакладную тэхнічную спецыфікацыю і зыходнікі асноўных частак. Гэта **не гатовы Unity package і не гульнявая зборка**: тут няма серыялізаваных `.vfx`, `.shadergraph` або `.unity` assets. Іх стварэнне, FireWorld/PhysX integration і Unity-прыёмка прапісаныя ў тэхдоку.

## Змесціва

`Sources/Shaders/` — агульная field/contact math, VFX Initialize, VFX Update, працэдурны flame fragment і URP preview shader.

`Sources/Simulation/` — чысты C# contact solver на Unity.Mathematics.

`Sources/Presentation/` — дакладны GPU-layout і C# GraphicsBuffer bridge.

`Config/Fire_Defaults.json` — прапанаваныя стартавыя параметры з адзінкамі.

`Tests/test_reference.py` — 15 аўтаномных Python-тэстаў, уключаючы 10,000 выпадковых plane sweeps. Запуск: `python Tests/test_reference.py`. Патрэбны Python 3.10+; знешнія бібліятэкі не патрэбныя.

`Tests/FireContactMathTests.cs` — NUnit source для EditMode tests у Unity. Тут не запускаўся.

`Tests/validation_report.json` — вынік фактычна выкананых праверак і спіс нявыкананых Unity/GPU gates.

## Важныя ўмовы

Не капіраваць усю тэчку як finished assets і не чакаць гатовага эфекту ад аднаго Material. Код размяшчаецца па шляхах тэхдока. VFX Update аўтаматычная інтэграцыя выключаецца, таму што source сам рухае positions. Fragment flame і compute simulation — розныя HLSL-шляхі.

Зыходнікі напісаныя для гэтага пакета; чужыя asset-пакеты, платныя тэкстуры і шрыфты не ўключаныя. Крыніцы API і reference-art пазначаныя ў тэхнічным дакуменце. Performance values — мэты для праверкі, а не ўжо атрыманыя замеры.
