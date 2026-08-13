# El-Emental / Elemental Planets

Физическая игра о магии на маленьких разрушаемых планетах. Магия меняет геометрию, массу, температуру, давление и импульс мира.

Проект реализован по 52-страничному master blueprint `Elemental Planets — Core Design & Technical Architecture v0.1` на Unity 6000.5.7f1 + URP.

## Быстрый старт

1. В Unity Hub добавьте корень этого репозитория как существующий проект.
2. Откройте `Assets/Elemental/Content/Scenes/EarthCoreSlice.unity` или любую milestone-сцену.
3. Нажмите Play.

Если интерфейс Unity мигает или становится чёрным в режиме DX12, сохраните работу, закройте Editor и запустите `Open-Unity-DX11.cmd` из корня проекта. Этот launcher использует официальный параметр Unity `-force-d3d11` и не меняет игровые графические настройки.

Управление: мышь всегда свободна для наведения, выбора камней и рисования стены. `A/D` поворачивают персонажа вокруг локальной нормали планеты, `W/S` двигают вперёд/назад, камера плавно следует за направлением персонажа. ЛКМ по существующему физическому камню сразу берёт его телекинезом; ЛКМ по земле формирует новый камень, а быстрый боковой жест рисует основание стены. ПКМ копит силу толчка или выпуска удерживаемого камня. Удержание `Space` заряжает земляной столб под персонажем. Для волны сначала удерживайте `Shift`, задавая ширину сектора вплоть до 360°, затем удерживайте `Space` для силы и отпустите: ряды столбов расходятся наружу и становятся ниже с расстоянием. Колесо мыши меняет дистанцию удержания.

## Готовые сцены

- `Bootstrap` — чистый старт и clock smoke.
- `GravityToy` — локальная гравитация и обход планеты.
- `VoxelPlanetLab` — редактируемая SDF-планета.
- `EarthCoreSlice` — стена, вырывание камня, бросок и физический урон.
- `CharacterFeelLab` — active ragdoll и recovery.
- `WindLab` — Air и FieldWorld.
- `ElementLab` — Heat, Water, фазы и реакции.
- `VolcanoVillage` — миссии и кризисы.
- `OnlineSpike` — 2–4 клиента с latency/loss simulation.
- `WebLab` — сокращённый WebGL2-профиль.

## Creator Suite

Откройте `Elemental → Tools → Open Elemental Suite`. В UI Toolkit-окне доступны:

- Ability Workbench с компиляцией и schema-first JSON import/export;
- Earth Magic с настройкой сцепления и распада стен, роста/разлома камней и волны столбов;
- Material Lab с Water/Rock/Fuel presets;
- Planet Lab и переходы по игровым сценам;
- budget estimator для NativeHigh, NativeLow и WebLab;
- project validator, bug bundle и кнопки сборок.

Интерфейс меняется через обычные ассеты, Inspector и `Assets/Elemental/Content/UI/ElementalSuite.uss`.

## Проверка и сборки

- Полные EditMode/PlayMode тесты: `Scripts/Test-Unity.ps1`.
- Windows: `Elemental → Build → Build Windows Native Smoke`.
- macOS: `Elemental → Build → Build macOS Native Smoke`.
- WebGL2: `Elemental → Build → Build WebLab WebGL2`.
- Диагностика: `Elemental → Diagnostics`.

Архитектура и evidence находятся в `Docs/architecture.md`, `Docs/blueprint-compliance.md`, `Docs/adr` и `Docs/milestones`.

## Архитектурная граница

`Elemental.Core` и `Elemental.Simulation` содержат авторитетную симуляцию. Команды входят, типизированные события и snapshots выходят. MonoBehaviour, UI, VFX и звук — тонкие адаптеры и не могут менять канонический state.
