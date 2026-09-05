# Mixamo: какие движения нужны для плавного locomotion

Дата: 2026-09-05. Основа: текущие файлы рабочего дерева на HEAD `d2174ed`, Unity `6000.5.7f1`, Animation Rigging `1.4.1`. Это исследование и план интеграции, не отчёт о выполненной замене анимаций.

**Рекомендация:** сначала закончить цепочку «отрыв → полёт → короткое приземление → продолжение движения» и добавить старт/торможение. Затем повороты с переступанием, после них — вариации idle. Большая библиотека сама по себе не исправит контакты и выбор фаз.

## Что подтверждено в проекте

Проверены FBX/meta, сохранённая `EarthMotionLibrary.asset`, GUID-ссылки сохранённого `KayKitMage.controller`, генераторы и код EAMM. Unity Play Mode в рамках исследования не запускался. Наличие файла и корректное проигрывание на персонаже — разные уровни проверки.

| Участок | Факт рабочего дерева | Следствие |
|---|---|---|
| Базовое движение | В библиотеке есть Mixamo idle, ходьба вперёд/назад, бег назад; KayKit бег вперёд и боковой бег | Повторно скачивать базовый набор целиком не требуется |
| Старт и остановка | 30 записей recipes, из них 9 проходят фильтр searchable base; ни одной записи роли Start/Stop | Реальный пробел в наполнении библиотеки |
| Поиск переходов | `ResolveQueryTag` в `PlanetEAMMCharacterController` выбирает Idle/Forward/Backward/Left/Right. Константы Start/Stop/Pivot есть, но этот маршрутизатор их не возвращает | Добавление файлов и тегов само по себе не включит переходы |
| Повороты | Есть `pivot.left.mixamo`; authored turn tree использует Left Turn и его mirror | Правый/180° варианты полезны, но им нужна явная политика выбора |
| Прыжок | В recipes присутствуют KayKit `Jump_Start`, `Jump_Idle`, `Jump_Land` с ролью Recovery | Прыжки не отсутствуют как ресурсы; эта роль исключена из базового MM-поиска |
| Authored Jump/Fall | Оба сохранённых состояния ссылаются на `X Bot@Falling.fbx` | Наличие Jump_Start в recipes не означает, что он назначен в authored takeoff |
| Authored Land/Hard Land | Оба ссылаются на `X Bot@Hard Landing.fbx`; graph уже уменьшает амплитуду обычного приземления | Настоящее мягкое приземление — отдельная полезная замена, при сохранении текущего исправления амплитуды |
| Moving Land | Используется `Falling To Roll`; Moving Land Back использует Hard Landing | Нужен обычный выход в движение без переката; перекат оставить отдельным действием |
| Landing уже скачан | `X Bot@Landing.fbx.meta`: Generic (`animationType: 2`), без Humanoid mapping и явно заданных диапазонов | Сначала подготовить и оценить этот ресурс; пока это не готовая Humanoid-замена |
| Резерв Mixamo | Running, Walk Strafe Left/Right и другие боковые FBX присутствуют, но проверенная библиотека использует другие записи | Это запас для проверки качества, а не доказанные отсутствующие клипы |
| Разные настройки импорта | Idle копирует Avatar; Running и проверенный Walk Strafe Left создают собственный Avatar | Различие требует проверки basis/скелета; само по себе оно не доказывает дефект клипа |

Числа 30/9 относятся к сохранённым recipes и текущему фильтру baker, не к реально загруженной базе. Ранее соседняя задача сообщала 460 poses/14 clips и проблемы коленей; это исторические данные, здесь повторно не измеренные. Генератор библиотеки также содержит записи, которых нет в прочитанном asset: при интеграции сверить свежесть recipe asset, сгенерированной базы и bind pose.

## Что найдено в живом каталоге Mixamo

Поиск выполнен непосредственно на Mixamo без авторизации. Наличие следующих названий подтверждено карточками; Running Jump и Standing Jump Running Landing открывались в превью на Default Character. Проверки на нашем X Bot и скачивания не было. Ссылки ведут на соответствующую выдачу; одинаковые названия нужно различать по описанию карточки.

| Приоритет | Кандидат | Назначение и отбор |
|---|---|---|
| P0 | **Standing Jump Running Landing** | Выход после прыжка в бег. Первый кандидат для обычного moving landing. [Поиск](https://www.mixamo.com/#/?page=1&query=landing&type=Motion%2CMotionPack) |
| P0 | **Standing Land To Standing Idle** | Парный выход в неподвижную стойку. [Поиск](https://www.mixamo.com/#/?page=1&query=landing&type=Motion%2CMotionPack) |
| P0 | **Fall A Land To Run Forward** | Альтернатива для схода с края/падения в бег. [Поиск](https://www.mixamo.com/#/?page=1&query=landing&type=Motion%2CMotionPack) |
| P0 | **Standing Jump Running**, **Running Jump**, **Standing Jump** | Сравнить отрыв с разбега и с места; оценить посадку таза и возможность отделить фазы. [Поиск](https://www.mixamo.com/#/?page=1&query=jump&type=Motion%2CMotionPack) |
| P0 | **Start Walking**, **Idle To Sprint** | Разгон. У Start Walking выбрать обычную ходьбу из стойки, без оружия. [Поиск](https://www.mixamo.com/#/?page=1&query=start&type=Motion%2CMotionPack) |
| P0 | **Stop Walking**, **Run To Stop** | Торможение. Выбирать обычную остановку ходьбы и быстрое торможение бега; исключить вариант упора в объект. [Поиск](https://www.mixamo.com/#/?page=1&query=stop&type=Motion%2CMotionPack) |
| P1 | **Left Turn 90**, **Right Turn 90**, **Quick 180 Turn** | Переступание при развороте. Проверить суммарный yaw и опорную ногу. [Поиск](https://www.mixamo.com/#/?page=1&query=turn&type=Motion%2CMotionPack) |
| P1 | **Backward Jump**, **Left Strafe Jump**, **Run Strafing Jump** | Боковой/обратный прыжок после принятия передней цепочки. [Поиск](https://www.mixamo.com/#/?page=1&query=jump&type=Motion%2CMotionPack) |
| P2 | **Breathing Idle**, **Neutral Idle**, **Idle** с переносом веса | Спокойное дыхание и редкие переступания. Не заменять рабочий idle без сравнения. [Поиск](https://www.mixamo.com/#/?page=1&query=idle&type=Motion%2CMotionPack) |
| P2 | **Crouch Walk Forward**, **Crouch Walk Back**, **Crouch Walk Left**, **Crouch Walk Right**, **Crouched To Standing** | Для отдельного наземного crouch-режима, если он нужен механике. Не смешивать автоматически с позой surf. [Поиск](https://www.mixamo.com/#/?page=1&query=crouch&type=Motion%2CMotionPack) |

В выдаче приземлений также присутствуют Action Adventure Pack (22) и Magic Locomotion Pack (16). Это кандидаты на согласованный набор; число клипов подтверждено карточками, совместимость и полный состав не проверены. Для первой интеграции достаточно выбрать две посадки, один takeoff, один start и один stop.

## Retarget и IK: что действительно поможет

**Retarget переносит движение между скелетами.** Unity Humanoid делает это через настроенные Avatar. Для новых загрузок выбрать существующий X Bot на Mixamo и проверить соответствие иерархии canonical `X Bot.fbx`. Для того же скелета использовать принятую в проекте политику Copy From Other. KayKit с другой иерархией должен сохранять собственный корректный Avatar и проходить Humanoid-retarget; слепо копировать ему X Bot Avatar нельзя. [Unity Humanoid retargeting](https://docs.unity3d.com/6000.0/Documentation/Manual/Retargeting.html).

**Control rig нужен для правки исходного движения.** Официальный [Mixamo add-on от Adobe](https://www.adobe.com/products/substance3d/plugins/mixamo-in-blender.html) предоставляет инструменты управления ригом в Blender. На Blender Extensions есть стороннее продолжение [Mixamo Rig](https://extensions.blender.org/add-ons/mixamo-rig/): IK control rig и bake между ним и скелетом. На дату проверки опубликована версия 1.2.2; каталог указывает Blender 4.2 LTS и исключает 5.5+. Совместимость с фактической установленной версией Blender здесь не проверялась. Полезное применение: поправить глубину приседа, опору стоп и начало/конец клипа, затем запечь в исходный деформирующий скелет.

**Runtime IK адаптирует контакт к окружению.** Unity Two Bone IK решает положение конца конечности относительно target; hint задаёт направление сгиба. Это не готовая система locomotion. В проекте уже установлена Animation Rigging, есть `EarthFootContactController` и отдельный стабильный arm IK. Новый параллельный решатель ног не нужен. [Unity Two Bone IK 1.4.1](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/manual/constraints/TwoBoneIKConstraint.html).

Сохраняется действующее разделение: PlanetMotor/Rigidbody двигает персонажа; EAMM через PlayableGraph даёт базовую позу; authored действия перекрывают её по текущей политике; EarthFootContactController владеет финальной коррекцией стоп, коленей и таза. На сферической поверхности все контактные измерения опирать на `LocalUp`, а не глобальный Y.

## Требования к импорту и подключению

1. Сначала сравнить уже имеющиеся KayKit Jump_Start/Jump_Land и Mixamo Landing. Для нового исходника записать название, описание варианта, дату, настройки Mixamo и выбранного персонажа. Получить FBX без повторного mesh, если skeleton совпадает; начальный baseline — 30 FPS под текущую базу 30 Hz, без упрощения ключей.
2. `applyRootMotion` остаётся false. Это не команда обнулить вертикальное движение таза или одинаково переключить все Bake Into Pose. Сохранить позу и принятую root/feet-политику importer; отделить игровое перемещение от визуального движения тела. У loop включать loop только после проверки шва, у start/stop/land — выключать.
3. Прогнать `AnimationClip.SampleAnimation` на canonical rig и на bake source rig. Проверить направления бедра/голени/стопы, длины сегментов, масштаб, bind basis, finite quaternion и позу на обеих границах. Изменения поз между skeletons не считать исправленными по зелёному значку Avatar.
4. Разметить отрыв, первый контакт L/R, окно посадки, recovery/cancel и фазу шага. Использовать существующий metadata pipeline и его проверки; значения contactStart/contactEnd по умолчанию не заменяют реальные контакты обеих стоп.
5. Start/Stop/Pivot подключать и в recipe, и в генератор `MotionLibraryWindow`, и в query policy. Для старта учитывать переход от покоя к намерению, для stop — торможение, для pivot — ошибку направления. Проверить возможность мгновенной смены намерения и повторного старта.
6. В `MotionLibraryBuilder.SampleClip` сейчас synthetic position = origin + direction × nominalSpeed × time, rotation содержит nominalYaw × time. Такой источник не описывает реальное ускорение/торможение; nominalYaw фактически используется как скорость поворота. Для новых переходов нужен измеренный/авторский профиль скорости и yaw либо отдельное управляемое воспроизведение перехода. Не превращать полный угол 90° в 90°/с автоматически.
7. Jump/Land/Dodge/Recovery не добавлять в обычный directional search. Существующий baker исключает Recovery/Magic/Impact. Назначить подходящие клипы в authored action lane и генератор; иначе рецепт будет существовать, а нужное действие останется прежним.
8. На выходе из посадки выбрать фазу locomotion по опорной ноге и направлению/скорости. Мягкая посадка допускает продолжение движения; тяжёлая посадка и перекат сохраняют отдельные условия. Не растягивать takeoff на всю длительность полёта и не ждать конца тяжёлого клипа для каждого короткого прыжка.
9. Foot IK отпускать при отрыве и переносе ноги, включать по реальному контакту. Переступание в idle также требует отпустить соответствующую стопу. Проверять финальную позу после EAMM, authored blending и IK: длинный blend может скрывать ошибку, но добавлять задержку.

Для вариаций idle предлагается сначала две: спокойное дыхание и редкий перенос веса. Выбирать их после нескольких секунд покоя, немедленно прерывать движением/магией; поворот взгляда можно делать существующим процедурным слоем, если он уже поддерживается. Эти правила — предложение, не измеренные настройки.

У crouch есть дополнительное ограничение: `RepairProductionLocomotionCatalog` сейчас удаляет старые `walk.crouch`/`walk.sneak`. Для нового режима нужны собственные семантика и фильтр поиска, а также согласование с этим генератором. Простое возвращение старых IDs при очередном repair не сохранится.

## Следующий исполнимый шаг и критерий готовности

Один небольшой проход: подготовить и сравнить `Standing Jump Running Landing` и `Standing Land To Standing Idle`, плюс существующий Landing/Jump_Land; подключить выигравшую пару в authored lane, сохранив текущие условия тяжёлого приземления и переката. Затем отдельно добавить Start/Stop с маршрутизацией и корректными траекториями.

Для проверки воспроизвести: прыжок с места; прыжок на бегу без отпускания ввода; отпускание и разворот в воздухе; сход с края; повторный прыжок сразу после контакта; остановка с обеих опорных ног; склон и неровный фрагмент. После базового принятия — боковой/обратный ход, surf и magic в сочетании с прыжком.

Сравнить до/после на одном сценарии при 30/60/120 FPS: ID/время источника позы, applied EAMM weight, скорость мотора, контакт L/R, смещение стопы во время опоры, скачок угла колена и фазу выхода из посадки. Использовать имеющиеся semantic/rescue/surface/continuity проверки; соответствие пределов брать из текущих acceptance policies. Новые численные «нормы» без baseline не объявлять. Визуально подтвердить отсутствие паузы перед бегом, скольжения опорной стопы и резкого изменения высоты таза.

Исследование завершено проверкой каталога и исходников. Пригодность выбранных клипов и улучшение game feel ещё требуют импорта и этого Play Mode-сравнения.

## Точки интеграции

- `Assets/Elemental/Authoring/Editor/EarthHumanoidMotionSetup.cs`: импорт, назначение authored клипов и генерация переходов.
- `Assets/Elemental/Content/Animation/KayKitMage.controller`: фактические сохранённые ссылки состояний.
- `Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset`: проверенные 30 recipes.
- `Assets/Elemental/Authoring/Editor/MotionMatching/MotionLibraryWindow.cs`: наполнение и ремонт каталога.
- `Assets/Elemental/Authoring/Editor/MotionMatching/MotionLibraryBuilder.cs`: bake, synthetic trajectories, фильтр ролей, bind pose.
- `Assets/Elemental/Presentation/MotionMatching/PlanetEAMMCharacterController.cs`: выбор query tag.
- `Assets/Elemental/Presentation/MotionMatching/EAMMBasePoseBridge.cs`: перенос базовой позы и фактический вес EAMM.
- `Assets/Elemental/Presentation/MotionMatching/EarthAnimationGraph.cs`: композиция, амплитуда посадки, inertialization.
- `Assets/Elemental/Authoring/Editor/EarthAnimationClipMetadataPipeline.cs`: контакты и семантическая валидация.
- `Assets/Elemental/Simulation/Characters/EarthAnimationTransitionPolicy.cs`: политика переходов и прерываний.
- `Docs/adr/0033-animation-contact-and-arena-rendering-rehabilitation.md`: действующее владение контактами.

Координация: задаче «Дарабіць EAMM анімацыі і механікі» отправлено уведомление о поиске, её ограничения учтены. Runtime, сцены, импортёры и исходные FBX в этой задаче не изменялись.

## Последующая установка Mixamo Rig — 2026-09-05

По отдельному запросу пользователя установлен Mixamo Rig 1.2.2 в Blender 5.2.1 LTS, модуль `bl_ext.user_default.mixamo_rig`. Установка выполнена штатным оператором Blender в фоновом процессе: live MCP не подключался к localhost:9876, а CLI MCP не находил настроенный executable. Официальный архив Blender Extensions проверен по SHA-256 `5c59e419355b0b4b5014b7bbcbee4d9a4c38476b8225526e3c904a81b056a191`.

Свежий отдельный запуск подтвердил сохранённое включение расширения, регистрацию 6 панелей и 21 оператора (control rig, import/bake, IK/FK). Это проверка установки и регистрации; создание рига на персонаже ещё не проверялось. Панель: 3D View → N → Mixamo → Mixamo Control Rig. Уже открытым экземплярам Blender потребуется перезапуск для загрузки сохранённого расширения.

Настройки сохранены с резервной копией в локальной папке конфигурации Blender. Отчёты установки и проверки находятся в `Tools/BlenderMixamoRigInstall/` родительского рабочего каталога, вне Unity-проекта. Уточнение к сведениям каталога выше: manifest установленного архива задаёт min `4.2.0`, max `5.5.999`; фактическая загрузка на `5.2.1` подтверждена.
