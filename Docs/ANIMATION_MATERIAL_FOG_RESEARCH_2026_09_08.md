# Плавная боёвка, камень, ветер и голубой туман

2026-09-08, working tree main, base 1235579. Три независимых агента исследовали анимацию, материалы/пыль и туман. Исследование не является визуальной приёмкой новых анимаций или материалов.

## Решение по анимации

Сохранить текущий graph, A/B buffers, C1 inertialization и владельцев контактов. Главный дефект найден в EarthMagicClipClock: Sustain доходит до единственного маркера и удерживает кадр; smoothstep обнуляет скорость на промежуточных маркерах. Плавное смешивание само по себе не оживит эту позу. Angular-velocity inertialization уже существует в EarthRotationInertialization; старое предложение добавить её заново устарело.

Первый срез: PullStone entry → непрерывный sustain loop → release/cancel и два связанных удара. В EarthMagicMotionProfile описать отдельные сегменты/клипы, loop interval, контактные и допустимые выходные маркеры. EarthMagicClipClock получает непрерывную локальную фазу удержания. HumanoidCharacterPresentation использует существующие A/B буферы, а следующий удар выбирается из нескольких разрешённых входов по близости позы и скорости. PlanetMotor остаётся владельцем корневого движения, simulation — момента удара, foot controller — финальных контактов. Ручной выбор locomotion сохраняется.

Контентный proof: 6–8 согласованных сегментов, сначала из уже импортированных 40+ Mixamo FBX. Оценка 2–4 рабочих дня при пригодных исходниках, не обещание срока. Для проверки закупка не требуется. Сначала 10 секунд живого удержания и 20 циклов hold → strike → strike → cancel на обеих моделях, стоя/в движении при 30/60/120 Hz. Проверить петлю, скорость суставов, контакт с моментом gameplay event и существующие ограничения foot drift/gap. Новое A/B видео обязательно.

SONIC не выбирать для быстрого production пути: локальный preview имеет p95 планирования около 79–82ms и модель 774MB; это не полный runtime бюджет и не готовая Earth-хореография. MotionBricks также требует отдельного Python/CUDA/G1 пути.

Источники: [Unity layers](https://docs.unity3d.com/Manual/AnimationLayers.html), [inertialization](https://theorangeduck.com/page/spring-roll-call), [существующий JLPM](https://github.com/JLPM22/MotionMatching), [Mixamo FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html), [MotionBricks](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/motionbricks/README.md).

## Камень и пыль

Unity 6000.5.7f1 / URP17.5 Forward уже имеет RumbleRockLit: triplanar albedo, macro variation, bevel/fracture masks, SSAO и rest-frame mapping. Normal/roughness textures отсутствуют. Сначала довести крупную форму, свет и фаски на одной скале и одном разломе при одинаковой камере. Затем, только при видимой пользе, добавить слабую triplanar normal и packed roughness/cavity; правильно переориентировать нормали проекций, сохранить _FractureLocalToStructure. Сумеречную темноту проверять отдельно до/после grading.

EarthSurfaceWindDust уже имеет 192 ограниченных ground mesh wisps, tangent wind, препятствия и LightDustMote с premultiplied alpha/depth fade. Следующий шаг — общий медленно проходящий пространственный порыв направления/силы, затем небольшая поперечная турбулентность у камней. Текущий sin(time*.8+slot*.41) не создаёт единого порыва. Pure math — EarthSurfaceWindPolicy, параметры — EarthSurfaceWindDustProfile, адаптация частиц — EarthSurfaceWindDust. Не включать Shuriken Noise поверх покадрового переопределения velocity.

Не добавлять MixFog без проверки: fullscreen atmosphere идёт после transparents и уже обрабатывает их цвет, но использует opaque depth. Проверить пыль перед далёкой скалой и небом. Четыре ground probes/frame дают до 48 кадров обхода при 192 частицах: края/свежие разломы — обязательный случай.

План: полдня A/B, ориентир 1–2 дня на материал и ветер при исправном editor loop. Цели для добавки, пока не измерения: CPU p95 ≤.2ms, 0 steady-state GC, GPU delta ≤.5ms при 1080p. Приёмка: одинаковые 10–15 секунд orbit днём/закатом/ночью, неподвижный узор при fracture swap, согласованный порыв, отсутствие дымных шаров, светящихся прямоугольников, мерцания и прыжков репроекции.

Источники: [triplanar normal mapping](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Triplanar-Node.html), [particle noise](https://docs.unity3d.com/6000.0/Documentation/Manual/PartSysNoiseModule.html), [soft particle depth](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/particles-unlit-shader.html), [particle optimization](https://docs.unity3d.com/6000.0/Documentation/Manual/particle-system-optimization.html).

## Внесённое исправление тумана

DayFog (.64,.82,.98), DayFogBottom (.48,.69,.91) сохранены в ValleyAtmosphereV2.asset, defaults и cloud installer. Существующие закат/ночь остаются под ValleyTimePalette.

FarClosureStart=1800m, FarClosureEnd=3200m задают smoothstep закрытия геометрии независимо от разреженного верхнего слоя height fog. Закрытие находится внутри существующей защиты ближней арены/верхушки планеты. Публичный pure контракт ValleyAtmosphereMath.DistanceClosure проверяет конечные входы и упорядоченный диапазон; ComposeSurfaceOpacity имеет необязательный closure=0.

Far chromatic edges, stipple и light diffusion теперь затухают с transmittance, поэтому не возвращают скрытые детали после fog composite. Полностью закрытые скалы используют общий цвет по лучу, а не собственную глубину. Это не обещает совпадение с прозрачным верхним небом: там физическая sky transmittance остаётся. Старый общий горизонтальный underside band не возвращён. Новые pass/texture samples не добавлены; GPU стоимость пока не измерена.

Проверка Unity и итоговые ограничения будут записаны после импорта.

Validation: actual ValleyAtmosphereMath.cs compiled through PowerShell Add-Type. PASS: 12001 distance samples monotonic, exact clear/opaque plateaus, three distant heights fully opaque, reverse-view planet protected. Eight Unity EditMode cases added. Initial Unity status succeeded (EarthCoreSlice, nonplaying, not compiling); subsequent import timed out. BlueFogClosureEdit requested, result absent. Shader compile, PlayMode, captures and GPU remain unverified. No scene rebuild or editor restart performed.

## Fresh implementation evidence — 2026-09-08

Unity recovered from its unrecoverable D3D11 device-reset dialog; recovery copies preserved, production scene reopened. Living Hold installer succeeded and saved the derivative torso clip/mask plus existing-controller layer. LivingHoldEdit **27/27** passed at00:16:29Z; LivingHoldPlay **6/6** passed at00:22:53Z in80.098s: actual10s hold/cancel, held aim/reacquisition, repeated punches and30/60/120Hz continuity. Hold trace maximum chest step .578782degrees, summed torso travel2.850973degrees, hand travel .095597/.165650m. These are path lengths, not displacement or extra-effect-only amplitude. Captures `BuildReports/LivingHold/hold-00…08.png` inspected at production view. Full-body authored hand-loop and whole-game animation/GPU acceptance are not implied.

Fog/wind pure Edit **33/33** passed00:16:08Z. Initial combined Play **4/5**: both new UI tests, existing held centered press, and production wind passed; strict fog pixels exposed a further defect. `AtmosphereFullscreen.shader` used raw depth >1e-5 to classify opaque geometry; far surfaces at4000m with .1m near clip were treated as sky. It now tests exact cleared depth (>0 reversed / <1 normal). This is separate from closure and post-fog detail attenuation. Strict black/white and depth-independence test rerun follows.

Final verification: MotionFogWindPlay **5/5 Passed** at2026-09-08T00:23:41Z,25.1315s. Actual production URP/RTX4070 D3D11 centre32x32 source difference: fog-off730573, fully closed black/white0, hidden4000/4500m depth0, near20m contrast737960. See BuildReports/ValleyFogClosure/evidence.txt and seven640x360 captures. Saved blue(.64,.82,.98) and closure1800–3200 confirmed in Unity; fog shader errors=false. EarthCoreSlice restored nonplaying and clean. Previous fog failure is superseded by this measured repair. Runtime UI2+existing press1 and production wind1 are included. Whole wind adapter mean .22627ms/peak .9070ms,109particles,86moving samples,.53928m mean travel in recorded interval; this does not isolate gust cost and does not meet/prove the research .2ms p95 incremental target. No full-frame GPU or comprehensive animation content acceptance claimed. Scoped diff whitespace checks clean. Changes remain uncommitted. Unity handed to parallel fire task after these checks.
