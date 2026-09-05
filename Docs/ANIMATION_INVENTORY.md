# Анимации, уже имеющиеся в проекте

Проверено 2026-09-03 через Unity AssetDatabase: перечислены реальные импортированные AnimationClip, а не предполагаемое содержимое FBX. Наличие файла не означает, что он подключён к игровому контроллеру.

## Mixamo — 22 разных именованных движения

Папка: `Assets/ThirdParty/Mixamo/`. Обычно файл называется `X Bot@<название>.fbx`.

| Группа | Точные названия для поиска в Mixamo |
| --- | --- |
| Ходьба и поворот | Walking; Walking Backwards; Left Turn |
| Ожидание и присед | Crouch Idle; Standing Idle To Crouch; Injured Idle |
| Падение и приземление | Falling; Hard Landing; Falling To Roll |
| Атаки без оружия | Lead Jab; Punching; Punch Combo; Mma Kick |
| Реакции на удар | Hit To Side Of Body; Receiving An Uppercut |
| Магия одной рукой | Standing 1H Cast Spell 01; Standing 1H Magic Attack 03 |
| Магия двумя руками | Standing 2H Cast Spell 01; Standing 2H Magic Attack 03; Standing 2H Magic Attack 05; Standing 2H Magic Area Attack 02 |
| Дополнительная | Wheelbarrow Dump |

Также есть второй файл `Standing 2H Magic Attack 05.fbx` с внутренним именем `mixamo.com` и базовая модель `X Bot.fbx` с таким же непрозрачным именем клипа. Это не подтверждение двух дополнительных уникальных движений.

Дополнительные импортные вырезки, не отдельные скачанные движения:

- `XBot Neutral Idle` — из `Standing Idle To Crouch`.
- `XBot Walk Neutral` — из `Walking`.

## KayKit — имеющиеся клипы по пакетам

Папка: `Assets/ThirdParty/KayKit/Animations/`. Это отдельный источник, не Mixamo. T-Pose во всех четырёх пакетах ниже опущена.

### Rig_Medium_MovementBasic.fbx

- Running_A, Running_B
- Walking_A, Walking_B, Walking_C
- Jump_Start, Jump_Idle, Jump_Land
- Jump_Full_Short, Jump_Full_Long

### Rig_Medium_MovementAdvanced.fbx

- Running_Strafe_Left, Running_Strafe_Right
- Dodge_Forward, Dodge_Backward, Dodge_Left, Dodge_Right
- Walking_Backwards
- Crouching, Sneaking, Crawling
- Running_HoldingBow, Running_HoldingRifle

### Rig_Medium_General.fbx

- Idle_A, Idle_B
- Spawn_Ground, Spawn_Air
- Hit_A, Hit_B
- Death_A, Death_A_Pose, Death_B, Death_B_Pose
- Throw, Interact, PickUp, Use_Item

### Rig_Medium_CombatRanged.fbx

- Ranged_Magic_Raise, Ranged_Magic_Shoot, Ranged_Magic_Spellcasting, Ranged_Magic_Spellcasting_Long, Ranged_Magic_Summon
- Ranged_Bow_Draw, Ranged_Bow_Draw_Up, Ranged_Bow_Release, Ranged_Bow_Release_Up, Ranged_Bow_Idle, Ranged_Bow_Aiming_Idle
- Ranged_1H_Aiming, Ranged_1H_Shoot, Ranged_1H_Shooting, Ranged_1H_Reload
- Ranged_2H_Aiming, Ranged_2H_Shoot, Ranged_2H_Shooting, Ranged_2H_Reload

## Что стоит искать сейчас

- Настоящий кувырок назад / backward roll с завершением на ногах.
- Мягкое приземление / soft landing без кувырка и глубокого падения.
- Вставание с живота и со спины / get up face down, get up face up.
- Короткий прыжок для X Bot: короткий уже есть в KayKit, но отдельного Mixamo-клипа с этим назначением в текущем наборе нет.

`Assets/Elemental/Content/Animation/XBot Landing Roll Back.anim` — сгенерированная обратная переделка переднего кувырка, НЕ настоящий скачанный backward roll. После жалобы на перекручивание она отключена в настройке контроллера; файл оставлен для сохранности. `Dodge_Backward` из KayKit — название уклонения, оно само по себе не гарантирует нужный кувырок при приземлении.

## Настройки скачивания Mixamo под текущий проект

- Character: X Bot.
- In Place: включить, если настройка доступна. Игровое перемещение здесь задаёт PlanetMotor, не root motion клипа.
- Format: FBX for Unity; Skin: Without Skin.
- FPS: 60; Keyframe Reduction: None.
- Mirror: выключить; скорость/Overdrive: оставить исходную; Trim: полный клип.
- Для backward roll скачивать именно движение назад, не зеркалить и не реверсировать передний кувырок.

Формат, экспорт без модели и отсутствие сокращения ключей согласуются с [руководством Unity по импорту анимаций](https://discussions.unity.com/t/how-to-troubleshoot-imported-animations-in-unity/371889). In Place и выбор 60 FPS — настройки для текущего проекта; внутренний motion-matching bake использует 30 Гц.
