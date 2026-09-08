# Codex handoff — Fire v1 для EL Emental

Рэалізуй сістэму стылізаванага surface-aware агню паводле `Docs/TECHNICAL_DESIGN_BE.md`, выкарыстоўваючы `Sources/` і `Config/Fire_Defaults.json`. Гэта задача на код і сапраўдныя Unity assets, а не яшчэ адно даследаванне. Прыёмы, hotkeys і новы ўрон цяпер не дызайніць.

## Спачатку правер checkout

Прачытай `AGENTS.md`, `Docs/architecture.md`, актуальны manifest, asmdefs і існуючыя gravity/surface/destruction adapters. Reference baseline: `codex/earth-core-polish`, commit `08afcd32f98ba0e9633b44f60cda6e7a00eed26c`, Unity 6000.5.7f1 / URP і VFX Graph 17.5.0. Калі цяперашні HEAD адрозніваецца, захавай новыя змены і адаптуй гэты план. Не рэсэтай галінку да reference commit.

Праца — у асобнай рабочай галінцы. Не перапісвай Earth, input, renderer або UI. Remote push/merge — толькі ў межах асобна дазволенага workflow. Пакет сам па сабе не дае патрэбы мяняць рэпазіторый па-за гэтай задачай.

## Непарушныя ўмовы

Часцінкі жывуць у World space. Кантакт перанакіроўвае ўжо існуючыя часцінкі; Reinit/impact-prefab не могуць падмяніць расцяканне. Local up прыходзіць з агульнай GravityWorld-логікі. CPU canonical state і GPU cosmetics раздзяліць. Bloom абавязковы ў native-профілях, але ўрон, heat і network state не чытаюцца з renderer.

Да 6 field nodes і 8 finite contact patches на FireGroup; baseline 6 swept probes і абмежаваныя дадатковыя праверкі. Footprint не можа працягвацца праз край, праём або знішчаную частку сцяны. Surface identity/generation/revision важней за InstanceID і lifetime prefab.

`EF_FireStep` сам рухае position: адключы automatic position integration у VFX Update. Aging/reap пакінь. `FireFlame.hlsl` — толькі fragment stage, бо выкарыстоўвае fwidth. `EF_Flame_float` выдае straight HDR RGB; не рабі падвойную premultiplication. І source code, і графы павінны кампілявацца ў фактычнай версіі Unity, а не толькі выглядаць сінтаксічна праўдападобна.

## Парадак выканання

**A. Shader/VFX lab.** Імпартуй зыходнікі ў дакладныя шляхі з тэхдока; ствары SG_FireFlame і VFX_FireGroup праз Unity Editor/package API. Не выдумляй серыялізаваны фармат графаў. Правайдзі сапраўдны import/compile. Падключы scaled clock і пакажы чытэльны агонь з Bloom і без яго.

**B. FireWorld і bridge.** Рэалізуй bounded state, nodes, profile, snapshots, pool і lifecycle Active/Draining/Retired. Падключы прыкладзеныя Init/Update і C# buffers. Граф павінен захоўваць custom phase/heat/width/aspect. Паварот billboard — толькі ў Output, simulation не залежыць ад камеры.

**C. Адна сцяна.** Рэалізуй PhysX sweeps + initial-overlap handling + true normal refinement. Ствары finite contact patch. Правер прамы і касы ўдар з адключанымі impact-добавкамі: павінен быць бачны рух старых часцінак уздоўж паверхні.

**D. Куты і рух.** Дадай некалькі асобных constraints, surface point/angular velocity, budget saturation handling, geometric edge validation. Не згладжвай розныя faces у адну normal. Пры праблеме sparse probes зрабі лакальны proxy; не запускай full-scene fluid/SDF pipeline.

**E. Разбурэнне.** Падключы canonical surface generations/revisions і события разлому. Invalid patch выдаляецца да наступнай presentation-публікацыі. Старое pool-значэнне не можа адносіцца да новай сцяны. Нельга выкарыстоўваць таймер як замену вядомай geometry invalidation.

**F. Production gate.** Дадай bounds з улікам хвоста, budgeted lights, LOD, idempotent setup/editor validator і FireLab test scene. Прагнаць EditMode/PlayMode, standalone performance і Earth regression. Асобна правер Direct3D/Metal, калі яны ўваходзяць у мэтавыя платформы праекта. Несумяшчальны backend павінен выключацца праз profile, не ламаць увесь запуск.

## Прыёмка

У repo павінны быць actual `.vfx`, `.shadergraph`, profile, isolated lab scene і tests. Прадстаў compile/import log, PlayMode вынікі, captures прамога/касога ўдару, кута і разбурэння, profiler baseline/delta з назвай hardware і graphics API. Замеры бюджэту не замяняць прыблізным FPS у Editor.

Прыкладзеныя 15 Python-тэстаў правяраюць матэматычную мадэль і тэкставыя contracts; яны ўжо прайшлі, але гэта НЕ Unity compilation. Дададзены Unity NUnit test source трэба сапраўды запусціць. Не пазначай нявыкананыя gates выкананымі.

Прапанаваныя gate: 1080p standalone, 8 High groups, p95 fire CPU ≤0.8 ms, p95 fire GPU ≤2.0 ms без ужо існуючага Bloom, 0 B/frame managed GC у steady state. Пры непраходжанні зафіксуй фактычныя лічбы і спрасці presentation, а не падганяй справаздачу. Усе values — пачатковыя мэты, не абяцаная хуткасць.

У фінальным report аддзялі: што рэалізавана, што скампілявана, што запушчана, што візуальна праверана, што вымерана і якія абмежаванні засталіся. Не абвяшчай гатовым увесь game element, калі зроблены толькі shader lab.
