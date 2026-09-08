# EL Emental — стылізаваны інтэрактыўны агонь

## Тэхнічная спецыфікацыя і рэферэнсная рэалізацыя v1.0

**Праект:** El-Emental / EL Ements. **Дата:** 6 верасня 2026. **Мэтавы спажывец:** Codex і распрацоўшчык Unity. **Статус:** спецыфікацыя для ўкаранення + зыходнікі асноўных алгарытмаў; не гатовая правераная Unity-зборка.

**Рашэнне:** невялікая колькасць CPU-даменаў і кантактаў кіруе GPU-часцінкамі. Часцінкі існуюць у 3D-прасторы, адлюстроўваюцца камера-арыентаванымі язычкамі, расцякаюцца ўздоўж сапраўдных перашкод і свецяцца праз HDR + агульны Bloom. Поўная fluid-сімуляцыя, raymarching аб’ёму і асобны Rigidbody на язычок не патрэбныя.

**Што ўжо прыкладзена:** арыгінальны працэдурны HLSL-шэйдар, URP preview shader, Initialize/Update для VFX Graph, GPU-field/contact math, CPU-копія кантактнага solver, C# bridge для буфераў, стартавыя параметры і тэсты. **Што трэба сабраць у Unity:** графы, FireWorld, PhysX/surface adapters, profile assets, pooling, тэставая сцэна і падключэнне да існуючага lifecycle. Гэтыя крокі ніжэй зададзены як канкрэтная праца, а не пазначаны ўжо выкананымі.

## 1. Межы задачы і крытэрый выніку

Рэалізаваць інфраструктуру агню, а не набор новых прыёмаў. Паток, сферычная абалонка і віхура тут — толькі геаметрычныя рэжымы аднаго renderer/solver. Не дадаваць hotbar, жэсты, баланс урону, механіку падпальвання арэны або новае кіраванне без асобнай задачы.

Галоўная прыёмка: ужо народжаныя часцінкі пры ўдары ў сцяну паварочваюць і расцякаюцца ў яе датычнай плоскасці. Яны не знікаюць, каб саступіць месца impact-prefab. Асобная іскра або кароткі ўсплёск дапушчальныя як дадатак, але не як замена кантакту.

На кадры без Bloom павінны чытацца колер і сілуэт агню. З Bloom — яркае ядро і мяккі арэол, а не суцэльная белая пляма. Падчас абыходу камерай эфект захоўвае прасторавую структуру; пры slow motion запавольваюцца і палёт, і ўнутраная форма язычкоў.

Мадэль свядома набліжаная. Шэсць probes не могуць дакладна аднавіць адвольную 3D-геаметрыю. Там, дзе лакальных плоскіх участкаў недастаткова, патрэбныя дадатковыя праверкі або лакальны proxy/SDF. Нельга называць сістэму дакладнай fluid-сімуляцыяй або абяцаць непранікненне праз любую магчымую сцяну пры фіксаванай колькасці probes.

## 2. Правераная база праекта

Спецыфікацыя прывязана да галінкі `codex/earth-core-polish`, commit `08afcd32f98ba0e9633b44f60cda6e7a00eed26c`. Правераныя файлы паказваюць Unity `6000.5.7f1`, URP `17.5.0`, VFX Graph `17.5.0`, Mathematics `1.4.0`, Burst `1.8.30`. У `Elemental.Presentation.asmdef` ужо ёсць reference на `Unity.VisualEffectGraph.Runtime`. [R1–R3]

Існуючая архітэктура аддзяляе `Core`, `Simulation`, `Runtime`, `Presentation`, `Input`, `Authoring`. У дакуменце праекта physics clock — 60 Hz, field clock — 20 Hz, thermal clock — 10 Hz. Лакальная гравітацыя, typed events і стабільныя structural IDs павінны застацца агульнай асновай. Гэта правераныя архітэктурныя патрабаванні; поўны executable audit усіх іх адаптараў у рамках гэтага пакета не рабіўся. [R4]

Перад інтэграцыяй Codex павінен паўторна прачытаць `AGENTS.md`, manifest, architecture і фактычныя adapters у сваім checkout. Калі HEAD іншы — зафіксаваць розніцу і адаптаваць новыя модулі, не адкатваць праект да гэтага commit. У гэтым пакеце няма змяненняў у GitHub і няма мерджу ў асноўную галінку.

## 3. Архітэктура і адказнасць

```text
Існуючая магія / debug emitter
            |
            v
FireWorld: групы, жыццё, энергія, вузлы формы
            |
            +--> Runtime: PhysX, surface IDs, gravity samples
            |                    |
            |              Contact snapshots
            v                    v
Readonly FirePresentationSnapshot
            |
            v
FireVfxBuffers -> VFX Initialize -> VFX Update -> Flame Shader
                                                    |
                                           URP HDR + Bloom
```

**Simulation / FireWorld.** Валодае групамі агню, іх энергіяй, геаметрыяй і lifecycle. Прымае чыстыя вынікі запытаў асяроддзя. Не захоўвае GameObject, Collider, Material або VisualEffect. Аўтарытэтная геаметрыя і heat/impulse requests не залежаць ад колькасці намаляваных часцінак.

**Runtime / FireEnvironmentAdapter.** Валодае PhysX-запытамі, спасылкамі на collider, gravity provider і адпаведнасцю canonical surface handle → Unity object. Ператварае вынікі ў чыстыя структуры. Не вылічвае ўрон па GPU-часцінках.

**Presentation / FirePresentationController.** Чытае snapshot, рыхтуе буферы, перадае час, кіруе emitter rate, bounds, святлом і якасцю. Не змяняе canonical state. Візуальная карэкцыя траекторыі не павінна вяртацца ў Simulation як вынік фізікі.

**Authoring / FireProfile.** ScriptableObject з immutable defaults пасля bake/copy. Параметры шэйдара, эмісіі і solver раздзяліць на секцыі, але не ствараць асобныя несумяшчальныя сістэмы для кожнай формы. Усе назвы `Fire*` у гэтай спецыфікацыі — прапанаваныя новыя API, а не сцвярджэнне пра ўжо існуючыя класы.

## 4. Файлы і месца інтэграцыі

```text
Assets/Elemental/Simulation/Fire/
  FireWorld.cs                      [рэалізаваць]
  FireDomainState.cs                [рэалізаваць]
  FireContactMath.cs                [прыкладзены]
  FirePresentationSnapshot.cs       [рэалізаваць]

Assets/Elemental/Runtime/Fire/
  FireEnvironmentAdapter.cs         [рэалізаваць]
  FireContactCache.cs               [рэалізаваць]
  FireSurfaceResolver.cs            [рэалізаваць]

Assets/Elemental/Presentation/Fire/
  FireGpuData.cs                    [прыкладзены]
  FireVfxBuffers.cs                 [прыкладзены]
  FirePresentationController.cs     [рэалізаваць]
  FireLightPool.cs                  [рэалізаваць]
  Shaders/FireFieldCommon.hlsl       [прыкладзены]
  Shaders/FireParticleInitialize.hlsl[прыкладзены]
  Shaders/FireParticleUpdate.hlsl    [прыкладзены]
  Shaders/FireFlame.hlsl             [прыкладзены]
  Shaders/FirePreview.shader         [прыкладзены]

Assets/Elemental/Content/VFX/Fire/
  SG_FireFlame.shadergraph          [стварыць у Unity]
  VFX_FireGroup.vfx                 [стварыць у Unity]
  Fire_Default.asset                [стварыць у Unity]
```

Тэсты пакласці ў фактычную EditMode/PlayMode assembly праекта. Не дадаваць дубль Unity-пакетаў, не мяняць renderer asset і не перапісваць Earth input. Shader includes у зыходніках выкарыстоўваюць паказаны абсалютны шлях `Assets/.../Fire/Shaders/`; пры іншай структуры змяніць includes узгоднена.

Файлы `.vfx` і `.shadergraph` не імітаваныя тэкставымі заглушкамі. Іх трэба стварыць праз рэальны Unity Editor/package API і захаваць пасля паспяховага import/compile. Дакладная схема графаў зададзена ў раздзелах 13–14.

## 5. Група агню, дамены і pooling

**FireGroup** — працяглы эфект з уласным ID/generation, seed і VisualEffect. **FieldNode** — лакальная вобласць уплыву ўнутры групы. **Probe** — запыт асяроддзя. **ContactPatch** — правераны канечны ўчастак паверхні. Гэта чатыры розныя сутнасці; колькасць вузлоў не абавязана супадаць з колькасцю probes.

Для v1 выбраны адзін VFX instance на жывую групу, да 6 вузлоў поля і да 8 contact patches у яе буферах. Гэта пазбягае складанай глабальнай маршрутызацыі кожнай часцінкі па domain slot/generation. GPU-часцінка чытае толькі невялікі набор сваёй групы.

Lifecycle групы: `Active → Draining → Retired`. У Active дазволены новыя часцінкі. У Draining spawn rate роўны нулю, але поле, кантакты, буферы і clock працягваюць абнаўляцца. Retired наступае не раней за апошні spawn + максімальны lifetime + запас 0.10 s у тым жа simulation time. Толькі тады дазволены Reinit і паўторнае выкарыстанне VisualEffect. Пры hard shutdown можна адразу знішчыць presentation, але гэта асобная падзея, не нармальны ўдар.

Пры раздваенні патоку новыя вузлы дадаюцца ў тую ж групу. Старыя часцінкі не пераспаўняюцца і не губляюць age/phase. Іх рух мяняецца праз поле побач. Калі шэсць вузлоў ужо занятыя, спрасціць геаметрыю або адкласці касметычную дэталь; не пераназначаць жывы буфер іншай групе.

Canonical intensity пры падзеле размяркоўваецца: сума энергіі дзяцей + страты не большая за энергію бацькі. Групавы spawn rate не памнажаецца на колькасць вузлоў. Shader Heat01 — мастацкі параметр, а не градусы Цэльсія.

## 6. Каардынаты і адзін час

VFX simulation space — **World**. Усе хуткасці, нармалі і восі — world vectors. Для дакладнасці GPU захоўвае points адносна `FireOriginWS`, зафіксаванага пры стварэнні групы. У Update: `p = positionWS − FireOriginWS`; пры запісе origin вяртаецца. Origin не павінен ехаць за рукой або за Transform emitter.

Новая пазіцыя рукі змяняе вузлы і месца новых births, але не пераносіць усе старыя часцінкі. Пераўтварэнні PlanetLocal/World робяцца на boundary. Калі праект уключыць floating origin, патрэбная адна транзакцыя rebase для GPU positions, nodes, contacts і bounds; да яе рэалізацыі забараніць rebase жывых груп.

Лакальны up прыходзіць з існуючай GravityWorld-логікі. Не выкарыстоўваць глабальны `Physics.gravity` для ўздыму агню. У кожнага field node свой up; `FireFreeUpWS` — апошні валідны лакальны up для кароткага свабоднага хвоста. Пры вялікіх групах або некалькіх gravity sources спатрэбяцца дадатковыя лакальныя samples, а не hardcoded цэнтр планеты.

Canonical хуткія кантакты абнаўляюцца на physics tick 60 Hz. Павольныя параметры можна рыхтаваць на field tick 20 Hz, але яны інтэрпалююцца для presentation. GPU выкарыстоўвае ўласны VFX `Delta Time`, а не паўторна памножаны CPU dt. Shader атрымлівае той жа scaled simulation time праз `FireTime`; `_Time` у прыкладзеным flame-модулі не выкарыстоўваецца.

У v1 выбраць variable-step VFX update: адна presentation-публікацыя на кадр, `playRate = 1`; slow motion ужо ўлічаны агульным scaled clock. Pause ставіць VFX на pause і замарожвае FireTime. Не памнажаць timeScale адначасова ў dt, playRate і shader clock. Калі абраны іншы VFX update mode, трэба паўторна праверыць cadence і time contract; не змяняць яго выпадкова праз global defaults.

Для рухомых contact patches буфер змяшчае pose на пачатак інтэрвалу VFX update, а FireTime — яго канец. CPU захоўвае папярэдні presentation time і reconstruct/interpolate pose на пачатак. HLSL экстрапалюе перанос у межах гэтага інтэрвалу. Нельга падаваць pose канца і пасля другі раз дадаваць поўны рух. Пры вялікім hitch не хаваць час праз `min(dt, 1/60)`: захаваць поўны dt, ужыць да 4 substeps і адзначыць перавышэнне дапушчальнага кроку.

## 7. Поле руху: формулы і тры формы

Вузел змяшчае A, B, radius, shell half-thickness, flow velocity, response rate, local up, lift speed, swirl, noise amplitude/frequency, phase, density і active. Поўнае адпаведнае пакоўванне ёсць у `FireGpuData.cs` і `FireFieldCommon.hlsl`.

Для капсулы бліжэйшая кропка на сегменце A–B вызначае radial vector. Вага роўная адзінцы ўнутры 0.6 radius і плаўна падае да нуля на radius. `A = B` дапушчальна: атрымліваецца сферычны вузел без дзялення на нуль.

Мэтавая хуткасць вузла:

```text
vTarget = flow
        + localUp * liftSpeed
        + swirl * cross(axis, radial)
        + coherentNoise(position, time, phase) * noiseSpeed
```

Гэта мастацкае поле хуткасцяў, не Navier–Stokes. Lift унутры вузла мае адзінкі m/s; FreeLift па-за полем — m/s². Не блытаць гэтыя параметры. Сума перакрытых вузлоў нармалізуецца па вагах, каб перакрыцце не павялічвала хуткасць удвая.

Часцінка пераймае мэтавую хуткасць з інерцыяй:

```text
blend = 1 − exp(−responseRate * dt)
v = lerp(v, weightedTargetVelocity, blend)
```

Такое exponential relaxation дакладна захоўвае вынік для пастаяннай target velocity пры іншым разбіцці часу. Гэта не азначае поўную frame invariance ўсёй нелінейнай collision-сістэмы. Па-за полем дзейнічаюць невялікі local lift і exponential drag; часцінкі натуральна адрываюцца і згасаюць.

**Capsule / stream.** Ланцужок да 6 актыўных лакальных вузлоў; апорныя кропкі змяняюцца ад магіі і кантактаў. Не запаўняць агнём запланаваны маршрут, які яшчэ не пройдзены. Active/density адлюстроўваюць фактычную вобласць агню.

**Sphere shell.** Вага залежыць ад `abs(length(p − centre) − radius)`. Flow і lift праецыруюцца ў датычную плоскасць сферы; мяккае вяртанне да патрэбнага радыуса ўтрымлівае таўшчыню. Пры гэтым positions не прывязваюцца жорстка да transform. Для uniform-volume births радыус выбіраецца праз кубічную інтэрпаляцыю паміж унутраным і вонкавым радыусам, не праз звычайны `lerp(rMin,rMax,u)`.

**Vortex.** Капсульная support-вобласць вакол восі A–B з моцным swirl і асобным восевым рухам. Часцінкі працягваюць сутыкацца з тымі ж contact patches. Гэта рэжым поля, а не асобны fire prefab з іншай фізікай.

У прыкладзеным noise кожная кампанента залежыць ад дзвюх іншых каардынат: незважанае поле мае нулявую дывергенцыю. Spatial falloff, абмежаванне хуткасці і кантакты змяняюць гэтую ўласцівасць. Замена на VFX curl turbulence дапушчальная пасля замераў, але turbulence павінна працаваць перад фінальным collision solve.

## 8. CPU-probes: дакладная паслядоўнасць

Стартавы ліміт — **6 асноўных swept probes на групу за physics tick**, плюс да 6 дадатковых геаметрычных запытаў для ўдакладнення. Гэта не абяцанне «шэсць API calls»: normal refinement, overlap і revalidation таксама лічацца ў бюджэт. Межы можна змяніць пасля profiling; нельга хаваць рэальныя query counts.

Для патоку probes размяшчаюцца ўздоўж бягучага руху і на найбольш рызыкоўных краях шырокай вобласці. Не трымаць усе шэсць толькі на цэнтральнай лініі. Пры невялікім дыяметры прыярытэт маюць front sweep і выяўленыя кантакты; каля кута — дадатковыя праверкі па розных баках. Пры змене геаметрыі contact probes пераразмяркоўваюцца.

Паслядоўнасць аднаго запыту:

1. Узяць previous/predicted position, сапраўдны collision radius і layer mask. Праверыць пачатковы overlap у загадзя вылучаны буфер; swept sphere не закрывае гэты выпадак.
2. Выканаць sweep уздоўж поўнага сегмента руху. Выбраць найбліжэйшы валідны hit, не спадзявацца на выпадковы парадак NonAlloc-вынікаў. Ігнараваць сам emitter і толькі загадзя дазволеныя cosmetic layers.
3. Удакладніць геаметрычную нармаль на тым самым collider: analytic normal для primitive або кароткі Collider.Raycast. Mesh triangle normal можна браць толькі пры валідным triangle index і правільным пераўтварэнні нармалі.
4. Пабудаваць/абнавіць finite patch, дадаць point velocity і canonical surface handle. Пры некалькіх асобных faces пакінуць некалькі constraints.

Unity асобна папярэджвае: SphereCast не выяўляе пачатковыя перакрыцці, а returned normal можа не быць геаметрычнай нармаллю плоскасці. Таму гэты падзел патрэбны не толькі дзеля стылізацыі. [R5]

Пачатковы overlap primitive/convex shapes вырашаць праз analytic distance або ComputePenetration з загадзя створанай query sphere. Не інстанцыяваць query collider на кожны кадр. Нявызначаны/перапоўнены вынік павінен даваць diagnostic counter і кансерватыўнае абмежаванне canonical руху; не дазваляць небяспечнаму дамену прайсці праз сцяну толькі таму, што скончыўся visual query budget.

Пасля расходавання бюджэту спачатку адкласці дробныя касметычныя ўдакладненні. Аўтарытэтны coarse collision патоку мае прыярытэт над іскрамі і дэкорам. Sparse probes не павінны выкарыстоўвацца як непацверджанае дакладнае прадстаўленне ўсіх тонкіх перашкод.

## 9. ContactPatch: мяжа паверхні, рух і разбурэнне

ContactPatch мае point, unit normal, tangent, footprint radius, front steering depth, recovery depth, surface velocity at point, angular velocity, spread fraction, response rate і skin. На CPU дадаткова захоўваюцца canonical surface ID/generation, geometry revision, local-space anchor, tick пацверджання і тып provider. GPU атрымлівае толькі актуальны валідны геаметрычны snapshot.

**Footprint — не бясконцая плоскасць.** Радыус пазначае толькі ўчастак, які сапраўды належыць гэтай face. Для вялікай плоскай primitive-сцяны radius можна атрымаць з analytic bounds з запасам да краёў. Для нерэгулярнага mesh патрэбныя дадатковыя samples або polygon/convex proxy. Радыус нельга расцягнуць праз акно, праём або разлом толькі таму, што побач падобныя normals.

Калі дакладны край невядомы, выкарыстоўваць меншы пацверджаны ўчастак і ўдакладняць суседнія вобласці. Гэта можа прапусціць касметычны язычок, але не павінна стварыць вялікую нябачную сцяну. Для крытычных складаных meshs дадаць лакальны convex/SDF proxy як асобны наступны ўзровень, не ператвараць малую колькасць samples у фальшывую гарантыю.

Нармалі можна плаўна стабілізаваць толькі ў межах той жа face. Не ўсерадняць дзве сцены кута ў дыяганальную плоскасць. Змена face або вялікі кут normal патрабуюць новага constraint. CPU дэбаг-рэжым павінен паказваць footprint і normal, каб няправільнае абагульненне было бачна.

Surface velocity ў кропцы язычка: `vSurface = vAtPatchPoint + cross(angularVelocity, particle − patchPoint)`. Усе разлікі кантакту робяцца ў адносных хуткасцях. Прыкладзены shader улічвае гэтую хуткасць, але замарожвае нармаль у межах update-інтэрвалу: гэта не exact rotational CCD. Калі `|omega| * substepDt > 0.1 rad`, павялічыць subdivision/абнавіць proxy і адзначыць рызыку. Для хуткай дзверы або абломка ля камеры можа спатрэбіцца больш дакладны collider path.

На destruction, pool reuse, geometry revision або знікненне provider адпаведны patch анулюецца да наступнай публікацыі буфера. Пры захаванні кананічнай piece identity local anchor пераходзіць на новую дынамічную частку; пры страце identity patch выдаляецца. Не выкарыстоўваць толькі `GetInstanceID()` як вечны ID. На кожным physics tick пераправяраць блізкія актыўныя patches; непацверджаны patch трымаць не больш за 0.05 s, і ніколі не трымаць яго пасля вядомай інвалідацыі.

## 10. Collision solve і расцяканне

Першая аперацыя — мяккае steering у невялікім слоі перад сцяной. Яна разбівае хуткасць адносна паверхні на normal/tangent складнікі. Нармаль заўсёды накіравана ў даступную прастору.

```text
relative = velocity − surfaceVelocity
incoming = max(−dot(relative, normal), 0)
lateral = position − point − normal * dot(position − point, normal)
outward = normalize(lateral) або seeded tangent direction у цэнтры
redirected = relative + normal * incoming
           + outward * incoming * spreadFraction
redirected = limitLength(redirected, length(relative))
```

Пры прамым удары lateral можа быць нулявым. Таму патрэбны стабільны per-particle phase, які задае розныя кірункі ў датычнай плоскасці. Без гэтага звычайная праекцыя проста абнуліць хуткасць. Spread пераразмяркоўвае адносны рух, не дадае неабмежаваную энергію. Калі сама сцяна рухаецца, world-space энергія можа змяніцца праз яе рух.

Другая аперацыя — swept non-penetration. Для old/new positions вылічваюцца signed distances да плоскасці з улікам virtual particle radius + skin. Калі d0 ≥ 0 і d1 < 0, знаходзіцца time of impact `toi = d0 / (d0 − d1)`. Footprint правяраецца ў кропцы перасячэння, а не толькі ў канцы кроку: хуткая часцінка можа перасекчы face і скончыць рух па-за яе radius.

Пасля валіднага hit карэктуецца пазіцыя і прыбіраецца inward relative velocity. Пры рэзкім прамым удары resolver таксама пераносіць частку ўваходнай хуткасці ў lateral — каб першы кадр удару не ператвараўся ў нерухомую пляму. Пачатковае неглыбокае пранікненне можна аднавіць у межах recovery depth; глыбокае знаходжанне за плоскасцю нельга лечыць тэлепартацыяй праз свет.

Для кутоў робяцца да 3 sequential projection passes па асобных patches. Калі constraints несумяшчальныя або пасля праходаў ёсць істотная пенетрацыя, гэта diagnostic failure: canonical coarse body спыняецца або выкарыстоўвае больш дакладны geometry path. Для касметычнага язычка дапушчальна кантраляванае згасанне. Не павялічваць projection offset да метраў і не хаваць NaN.

Код знаходзіцца ў `FireFieldCommon.hlsl`; CPU-рэферэнс — `FireContactMath.cs`. Solver не мае CPU-запыту на кожную часцінку. Ён не замяняе правільнае выяўленне саміх перашкод і не дае exact CCD адносна адвольнага mesh.

## 11. GPU-layout і жыццё буфераў

Выкарыстоўваюцца два `StructuredBuffer<float4>`: `FireNodes` і `FireContacts`. Адзін радок мае 16 bytes; адзін record — 6 радкоў, 96 bytes. Такі layout відавочны для C# і HLSL і пазбягае mixed-size struct packing. Unity патрабуе супадзення stride і рэкамендуе кратнасць 16 для structured buffers. [R6]

Node rows у парадку: `A.xyz/radius`; `B.xyz/shellHalfThickness`; `flow.xyz/response`; `up.xyz/lift`; `swirl/noiseSpeed/frequency/shape`; `density/active/phase/maxTargetSpeed`. Shape: 0 capsule, 1 shell, 2 vortex. Sphere shell выкарыстоўвае A як centre, B − A як swirl axis; калі axis нулявы, бярэцца local up.

Contact rows у парадку: `point.xyz/radius`; `normal.xyz/frontDepth`; `tangent.xyz/recoveryDepth`; `pointVelocity.xyz/spreadFraction`; `angularVelocity.xyz/response`; `skin/active/reserved/reserved`.

У поўнай групы гэта 576 bytes nodes + 768 bytes contacts = 1,344 bytes карысных буфераў. Гэта не памер усёй VFX-сістэмы і не ацэнка часу GPU. Дадзеныя малыя, але кошт draw calls, upload calls і transparency усё роўна трэба замяраць.

`FireVfxBuffers` вылучае масівы і GraphicsBuffer адзін раз. Publish запаўняе ўсе радкі, абнуляе неактыўныя, правярае finite values і выстаўляе counts. Перавышэнне ёмістасці — памылка caller, а не silent truncation. Гэты bridge не замяняе поўны authoring validator: той павінен дадаткова праверыць positive radius, нармалізаваныя vectors, дапушчальныя ranges і consistency geometry.

Unity не серыялізуе GraphicsBuffer reference; пасля reload патрэбны rebind. Dispose ідзе толькі пасля спынення/адключэння VisualEffect; нельга пакінуць renderer з disposed buffer. [R7]

## 12. Парадак GPU-update: без падвойнай інтэграцыі

VFX Custom HLSL block дазваляе чытаць і змяняць `VFXAttributes`; падтрымліваюцца Init/Update і buffers. Гэта compute/vertex шлях: pixel derivatives тут недаступныя. [R8–R9]

Прыкладзены `EF_FireStep` сам запісвае positions. Таму ў Update context **выключыць аўтаматычную velocity/position integration**. У VFX ёсць убудаванае `position += velocity * deltaTime`; калі пакінуць яго ўключаным, атрымаецца другі рух ужо пасля collision solve. Automatic age update і reap пакінуць уключанымі. [R10]

Унутры аднаго GPU substep парадак такі: sample shared field → exponential follow або free drift → contact steering → speed safety limit → integrate position → 3 collision projection passes. Пасля апошніх collision blocks нельга дадаваць новы position offset, turbulence або drag, якія зноў уціснуць часцінку ў сцяну.

Старт: 2 substeps. Дапушчальны дыяпазон 1–4. Controller можа выбраць колькасць па dt, expected speed і памеры вузла, але не павінен адкідаць рэальны час. Калі нават 4 substeps недастатковыя, зафіксаваць абмежаванне і выпрабаваць цяжкі сцэнар асобна. Камера не ўдзельнічае ў фізічным полі.

## 13. Дакладная схема VFX_FireGroup

**Blackboard properties.** GraphicsBuffer: `FireNodes`, `FireContacts`. UInt: `FireNodeCount`, `FireContactCount`, `FireSubsteps`. Vector3: `FireOriginWS`, `FireFreeUpWS`. Float: `FireTime`, `FireSpawnRate`, `FireMinLifetime`, `FireMaxLifetime`, `FireFreeDrag`, `FireFreeLift`, `FireMaxSpeed`, `FireParticleRadius`. Дадаць параметры шэйдара з раздзела 14. Усе патрэбныя bridge properties павінны быць Exposed.

**Custom stored attributes.** `firePhase` Float у [0, 2π); `fireHeat` Float, звычайна 0.85–1; `fireWidth` Float у дыяпазоне профілю; `fireAspect` Float. Яны ствараюцца адзін раз у Initialize, а не генеруюцца нанова кожны Update. Built-in age/lifetime/position/velocity/alive павінны быць даступныя ў сістэме.

**Spawn.** Адзін Constant Spawn Rate з `FireSpawnRate`. У Draining выставіць rate=0, не рабіць Reinit. Асабісты VFX startSeed — з group seed. Пры admission failure не ствараць нябачную групу са спажываннем буфераў.

**Initialize.** Capacity High=512; World space. Уставіць Custom HLSL з `FireParticleInitialize.hlsl`, выбраць `EF_FireInitialize`. Падключыць два буферы/counts, OriginWS, ParticleRadius, FireTime і lifetime range. Функцыя выбірае вузел з улікам density і аб’ёму, запаўняе капсулу або сферу і ўсталёўвае position/velocity/age/lifetime. Далей Set Custom Attributes для phase/heat/width/aspect.

Spawn rate задаецца на групу, не на вузел. Прыкладзены sampler запаўняе volume; таму field nodes павінны існаваць толькі ў рэальна дасягнутай вобласці агню. Яны не з’яўляюцца preview маршруту. HLSL адкідае births адразу за блізкім вядомым patch; поўны spawn admission/collider validity усё роўна належаць CPU-адаптару. Для вузла, які перасякае сцяну, скараціць/пераарыентаваць spawn support, а не рабіць неабмежаваныя retry на GPU.

**Update.** Custom HLSL `FireParticleUpdate.hlsl` → `EF_FireStep`. Усе аднайменныя ўваходы падключыць да Blackboard; `FireDeltaTime` — да graph Delta Time; `FirePhase` — да Get Attribute `firePhase`. Automatic integration выключана, age/reap уключаны. Не дадаваць стандартны collision па depth як замену гэтага solver.

**Output.** Output Particle Quad з `SG_FireFlame`; ZWrite Off, ZTest LEqual, double-sided, Alpha blending. Built-in particle color/alpha пакінуць адзінкавымі, пакуль explicit shader inputs адказваюць за колер і opacity. Інакш можна выпадкова двойчы памножыць emission/alpha.

Для velocity-aware billboard выкарыстоўваць Orient Advanced або эквівалентны explicit basis. Няхай view = normalize(cameraPosition − particlePosition), projectedFlow = velocity − view·dot(velocity,view). axisY = normalize(projectedFlow), axisX = normalize(cross(axisY,view)), axisZ = cross(axisX,axisY). Пры амаль нулявым projectedFlow выкарыстоўваць cameraUp, спраецыраваны ў view plane, і стабільны fallback. Не выкарыстоўваць нулявы cross product. Camera vectors даступныя толькі ў Output і не ўплываюць на Update.

Width — `fireWidth`, height — `fireWidth * fireAspect`. Дробная змена памеру па age дапушчальная, але асноўны разрыў/згасанне адбываецца ў шэйдары. Для формы сферы не замяняць усе 3D-пазіцыі адной велізарнай camera-facing карткай.

**Output shader inputs.** UV0 → UV; FireTime → SimTime; firePhase → Phase; saturate(age/max(lifetime,0.001)) → Age01; fireHeat → Heat01; profile → Opacity/ShapeFPS/Distortion/EdgeColor/BodyColor/CoreColor/CoreEmission. Злучэнні павінны быць per-particle там, дзе гэта адзначана, а не адным uniform Phase на ўсю групу.

**Bounds.** Кансерватыўнае аб’яднанне актыўных вузлоў + магчымы drift за астатні lifetime + палова дыяганалі найбольшай карткі. Snapshot павінен захоўваць і tail bounds у Draining. Праверыць паварот камеры і culling: выхад за экран не павінен скідаць simulation state. Infinite bounds і выключэнне culling для ўсёй сцэны не лічацца production-рашэннем.

## 14. SG_FireFlame і працэдурны шэйдар

Стварыць **Universal Unlit Shader Graph**, Surface Transparent, Blend Alpha, Render Face Both, Depth Write Off, Support VFX Graph On. Прызначыць яго Output context. Афіцыйная інтэграцыя падтрымлівае гэты шлях; асобны legacy Visual Effect target не патрэбны. [R11]

У fragment stage дадаць Custom Function: Source=File, файл `FireFlame.hlsl`, Name=`EF_Flame`, Precision=Float. Назва фактычнай функцыі ў файле — `EF_Flame_float`. Уваходы і іх тыпы: UV Vector2; SimTime, Phase, Age01, Heat01, Opacity, ShapeFPS, Distortion, CoreEmission — Float; EdgeColor, BodyColor, CoreColor — Vector3/HDR colors. Выхады: Color Vector3, Alpha Float.

Color падключыць да Unlit Base Color; Alpha — да Alpha з улікам depth/near fade. **Color ужо змяшчае HDR-ядро і не памножаны на alpha.** Пры Alpha blend не памнажаць яго ўручную. Калі пазней выбраны Premultiply, праверыць згенераваны shader і памножыць толькі адзін раз. Не спалучаць ручную premultiplication з пайплайнам, які робіць яе паўторна.

Працэдурная форма складаецца з tapered silhouette, двух маштабаў рухомага noise, мацнейшай дэфармацыі верхняй часткі, вузкай рухомай перацяжкі і age-dependent erosion. Гэта арыгінальны код для гэтага пакета. Ад Cyanilux узяты агульны рэферэнсны кірунак «простая форма + noise + выразныя каляровыя зоны», не скапіраваныя файлы або тэкстуры. [R12]

Антыаліясінг межаў і каляровых палос выкарыстоўвае `fwidth`. Таму `FireFlame.hlsl` працуе ў **fragment stage Shader Graph**, а не ў Custom HLSL simulation block. Гэтыя два HLSL-механізмы нельга пераблытаць. [R8]

Стартовая палітра ў linear RGB: edge=(0.65,0.018,0.003), body=(1,0.19,0.008), core=(1,0.72,0.19). CoreEmission=3, Opacity=0.9, Distortion=1. Гэта прапанаваны выгляд, не вымераны эталон. Калі значэнні ўводзяцца праз color picker, улічыць яго color-space пераўтварэнне; не ўставіць linear RGB як sRGB і пасля не перавесці яго яшчэ раз.

ShapeFPS=0 азначае плаўную форму. Значэнне 18–24 дазваляе лёгкую «намаляваную» квантаваную анімацыю, але толькі ўнутры шэйдара. Physics, position і camera не квантаваць. Базавая прыёмка робіцца з ShapeFPS=0.

Не дадаваць цёмную абводку кожнаму язычку. Сумяшчальнасць з cel shading дасягаецца выразнымі формамі і палітрай. Flame renderer не павінен трапіць у opaque outline/shadow passes персанажа. Агульны outline feature праекта трэба праверыць па layer/render queue, не адключаць яго глабальна.

### Depth fade і блізкая камера

У Shader Graph атрымаць Scene Depth у рэжыме Eye і particle fragment eye depth з view position: `particleEye = −Position(View).z`. Fade = saturate((sceneEye − particleEye)/SoftDistance), дзе SoftDistance пачаткова 0.08 m. NearFade = saturate((particleEye − 0.12)/0.16). Імі памножыць Alpha адзін раз.

Depth fade толькі змякчае перасячэнне карткі з opaque geometry. Ён не стварае фізічнага кантакту і не бачыць усе празрыстыя аб’екты. Ён патрабуе даступнай camera depth texture. Пры адсутнасці гэтай тэкстуры выключыць depth fade праз profile/variant, а не чытаць няправільны depth. У perspective і orthographic camera праверыць аднолькавыя метры, не параўноўваць raw depth з linear distance.

`FirePreview.shader` выкарыстоўвае той жа fragment function на звычайным quad. Ён прызначаны для shader-lab і праверкі URP; ён **не з’яўляецца самастойнай particle system і не паварочвае mesh да камеры**. `_FireTime` трэба падаваць з clock праз MaterialPropertyBlock. Гэтая preview-версія таксама павінна быць скампілявана ў мэтавай Unity.

## 15. HDR, Bloom і асвятленне асяроддзя

Агонь піша HDR у camera color target; адзін агульны URP Bloom апрацоўвае яркія вобласці выніковага кадра. Bloom — post-process, не асобны component на кожным язычку. [R13]

Пачатковыя settings: Bloom threshold=1.1, intensity=0.35, scatter=0.6; core emission=3. Падбіраць разам з рэальнай экспазіцыяй/tone mapping. Гэта старт для сцэны, не ўніверсальная фізічная каліброўка. Не перапісваць існуючы global Volume без параўнання астатняй гульні.

Bloom абавязковы ў абодвух native quality profiles. На Low спачатку спрасціць яго фільтрацыю/resolution, дым і святло, а не выдаліць само свячэнне. Аддзяліць замер кошту агню ад ужо ўключанага post-process, каб двойчы не прыпісаць Bloom яго бюджэту.

Bloom не асвятляе камень. Для High — агульны pool да 2 дадатковых point lights без shadow casting. Выбіраць найбольш значныя групы па энергіі і экранным укладзе з hysteresis; пазіцыю прывязваць да energy centroid, інтэнсіўнасць згладжваць прыкладна 0.08–0.15 s. Не пераскокваць штокадрава паміж аднолькавымі крыніцамі. Low можа мець 0 такіх lights, захоўваючы HDR/Bloom.

Да дадання lights праверыць, ці існуючы toon shader прымае additional lights. Калі не, патрэбна асобная невялікая інтэграцыя fire-light tint у яго асвятленне. Гэта не праверана гэтым пакетам і не павінна выдавацца за ўжо працоўны эфект. Не мяняць палітру ўсёй арэны дзеля аднаго полымя.

Іскры чытаюць тое ж поле, але маюць іншыя inertia/lifetime. Дым і heat distortion не ўваходзяць у абавязковы першы slice. Калі яны дадаюцца пазней, smoke і distortion не могуць схаваць няўдалую форму самога агню.

## 16. Бюджэты, LOD і деградацыя

У `Config/Fire_Defaults.json` захаваныя ўсе стартавыя параметры з адзінкамі. Для аднаго звычайнага патоку: radius=0.45 m, speed=12 m/s, response=10 s⁻¹, lift=1.5 m/s, swirl=1.5 rad/s, noise=0.9 m/s. Lifetime=0.45–0.85 s, rate=320 births/s на групу, width=0.14–0.28 m. Пры сярэднім lifetime 0.65 s гэта каля 208 жывых часцінак у steady state да rejection/culling; capacity=512 — запас, а не штучна падтрымліваемая колькасць.

High: да 8 груп, 512 capacity у кожнай, да 6 nodes і 8 patches, звычайна 2 GPU substeps. Low: да 4 груп, capacity=256, менш births і іскраў. Collision identity і базавыя constraints не павінны змяняць аўтарытэтны бой пры пераходзе якасці.

**Прапанаваныя performance gates, не замеры:** у 1080p standalone-player агульны fire CPU p95 ≤0.8 ms; fire GPU p95 ≤2.0 ms без уліку ўжо існуючага Bloom; steady-state managed GC allocation=0 B/frame. Перад прыняццем gate зафіксаваць GPU/CPU, API, quality preset, resolution і колькасць груп. Editor FPS не выкарыстоўваць як канчатковы benchmark.

Памер празрыстых картак і іх перакрыцце могуць каштаваць больш, чым самі probes. Не «аптымізаваць» эфект, памяншаючы колькасць часцінак і адначасова павялічваючы іх да велізарных квадратаў. [R12]

Парадак деградацыі: прыбраць дым/distortion → скараціць іскры → зменшыць births і background groups → спрасціць дапаможныя mesh-ядры → паменшыць рэзалюцыю дадатковых праходаў. Захоўваць галоўны сілуэт, кантакт з паверхняй і Bloom. Новая група павінна праходзіць admission budget; перапоўненая сістэма не можа бязмежна ствараць GameObjects або павялічваць buffers.

VFX Graph — асноўны native backend. Startup validator правярае compute, SSBO і Linear color space; URP VFX Graph не падтрымлівае Gamma. Пры несумяшчальнасці выбраць fallback або выразна адхіліць запуск гэтага backend, не мяняць color space усяго праекта моўчкі. Для WebLab/WebGL2 ён не падыходзіць як адзіны шлях: патрабаванні compute/SSBO трэба правяраць асобна. Прадугледзець `IFireVisualBackend`, але не будаваць цяпер другі вялікі engine. Першы Web fallback можа выкарыстоўваць малы пул ParticleSystem/mesh particles з тымі ж node/contact snapshots і гэтым жа мастацкім шэйдарам. Аўтарытэтная simulation ад backend не залежыць. [R14]

## 17. Адладка, replay і networking

Debug overlay павінен паказваць group ID/generation, nodes, probes, actual query count, valid patches, contact cache age, geometry revision, rejected spawns, budget saturation і buffer counts. У сцэне адлюстроўваць A–B, radius, footprint, normal, surface velocity і траекторыі некалькіх tracked particles. Debug-інструменты можна выключыць у release, але telemetry safety counters трэба захаваць.

У presentation допускаецца візуальнае адрозненне часцінак на розных GPU. PhysX і GPU simulation не абяцаюцца bitwise deterministic паміж платформамі. Для будучай сеткі host/authority перадае group lifecycle, seed, geometry і неабходныя snapshots/corrections. Не сінхранізаваць кожны billboard і не выводзіць урон з яго пазіцыі.

Replay захоўвае resolved commands і кананічныя падзеі ў існуючай сістэме. Camera, Bloom, LOD і soft-particle fade не ўваходзяць у вынік бою. Дэтэрмінаваны seed карысны для падобнага выгляду, але сам па сабе не робіць увесь physics/VFX deterministic.

## 18. Этапы ўкаранення з крытэрыямі гатоўнасці

### Этап A — шэйдарная лабараторыя

Стварыць ізаляваную `FireLab` без змен асноўнай сцэны. Імпартаваць FireFlame/Preview, зрабіць SG_FireFlame і маленькі VFX з world-space births без асяроддзя. Падключыць адзін scaled clock, HDR і існуючы Bloom. Вынік: shader compilation без памылак, тры чытэльныя зоны, выразны верхні язычок і адсутнасць прамавугольніка карткі. Захаваць captures з Bloom і без яго.

### Этап B — адна група і поле

Дадаць FireWorld lifecycle, profile, bridge і graph Init/Update. Спачатку адзін capsule node, пасля некалькі, shell і vortex. Уключыць persistent custom phase. Вынік: emitter рухаецца, стары хвост застаецца ў world space; pause/slow motion узгоднены; пры спыненні spawn група спакойна пераходзіць у Draining.

### Этап C — адна нерухомая сцяна

Рэалізаваць PhysX adapter, finite patch і normal refinement. Падключыць да ўжо жывых часцінак. Праверыць прамы/касы ўдар і рух пры камеры ззаду. Вынік: не impact-substitution, а бесперапынны разлёт; старым tracked particles не скідаецца age; пры прамым удары ёсць lateral motion. Geometry debug і візуальнае полымя адпавядаюць адно аднаму.

### Этап D — складаныя кантакты

Дадаць два constraints у куце, краі, вузкі праём, рухомую face і overlap recovery. Уключыць query budget і overflow counters. Вынік: няма дыяганальнай нябачнай сценкі ў куце, няма глабальнай плоскасці за краем, няма NaN і тэлепартацый глыбока са зваротнага боку.

### Этап E — існуючае разбурэнне і lifecycle

Падключыць surface generations/revisions, destruction і pool reuse. Разбіць сцяну пад бесперапынным патокам. Вынік: пасля актуальнай падзеі геаметрыі агонь больш не бачыць старую surface; дынамічная частка пераносіць толькі свае валідныя anchors; паўторнае выкарыстанне collider не ажыўляе чужы кантакт.

### Этап F — production-профіль

Дадаць bounds/tail handling, LOD, lights, idempotent editor setup і тэставыя сцэнары. Прагнаць 8 concurrent High groups і пацвердзіць budgets у standalone. Усё, што не праходзіць gate, спрасціць або выразна выключыць; не называць slice production-ready толькі па прыгожым скрыншоце.

На кожным этапе — асобны маленькі commit, test/import evidence і працоўная сцэна. Не аб’ядноўваць адначасова рэфактарынг Earth, новыя прыёмы, сетку і поўнае гарэнне матэрыялаў. Feature flag выключае Fire без змен Earth regression.

## 19. Тэсты і канкрэтная прыёмка

**Матэматычны пакет.** `Tests/test_reference.py` запускае 15 праверак: прамы/касы ўдар, finite footprint, хуткае перасячэнне, shallow/deep overlap, інвалідацыю, рухомую сцяну, кут, finite fallback, exponential relaxation, выбар радыуса shell, conservation bookkeeping і тэкставы buffer contract. Уключаны 10,000 seeded выпадковых plane sweeps і 10,000 shell samples. У гэтай сесіі ўсе прайшлі. Гэта незалежная Python-копія матэматыкі, не выкананне C# або HLSL.

**Unity EditMode.** Прыкладзены `FireContactMathTests.cs`. Дадаць layout/count/range tests, group lifetime/generation, deterministic node ordering, no-allocation tests для bounded solver і праверку invalid surface revisions. Спачатку скампіляваць пад фактычным Unity.Mathematics; Python report не замяняе гэтыя тэсты.

**Unity PlayMode.** Паток 12/24 m/s у плоскую сценку пад 0/30/60°, вугал 90°, край, праём, thin wall і moving fragment. Асобна: spawn амаль у сцяне, разбурэнне пры актыўным кантакце, pool reuse, камера 360°, вышыня/curvature планеты, pause, slow motion 0.1×, 30/60/120 FPS і hitch 100 ms.

**Візуальныя gate.** Старым particles не скідаецца age пры кантакце. Калі выключыць іскры і impact overlays, расцяканне ўсё яшчэ бачна. За межамі finite patch няма нябачнага працягу сцяны. Bloom off не ператварае эфект у невыразны дым; Bloom on не выбельвае большую частку сілуэта. Пры паслядоўным перамяшчэнні камеры phase/shape не міргаюць праз random every frame.

**Performance gate.** Запісаць baseline без агню і той жа camera path з агнём. Параўноўваць p50/p95 CPU і GPU, actual probes/queries, batches, overdraw, memory, GC і час Bloom. Прагнаць burst/retire сотні разоў: буферы і GameObjects вяртаюцца да baseline, stale counts не растуць. Не ўключаць Profiler deep profiling для канчатковага замеру кадра.

**Статус пакета на момант перадачы.** Python math tests — выкананы. C# compilation, Unity import, Shader Graph/VFX compilation, GPU visual review, standalone benchmark і Earth regression — яшчэ не выкананы. Гэта абавязковыя далейшыя gates, а не заяўленыя дасягненні.

## 20. Абмежаванні, якія нельга схаваць

Sparse geometry можа прапусціць тонкую перашкоду паміж probes. Disc patch набліжае face і не ведае ўсе яе адтуліны. Прыкладзены solver не рэалізуе exact rotational CCD або поўнае абцяканне адвольнага mesh. Alpha-sorted quads могуць мець sorting artifacts; асобныя VFX instances не маюць ідэальнай глабальнай per-particle сарціроўкі. Гэтыя абмежаванні патрабуюць сцэнавых тэстаў, не толькі матэматыкі.

На першы slice нельга дадаваць full-scene dynamic SDF bake, raymarched volume або дарагія GPU readbacks як хуткае «выпраўленне». Спачатку праверыць, ці праблема ў routing, bounds, normal, footprint, blend або birth support. Лакальны SDF/proxy дадаваць толькі для канкрэтнага даказанага failure case.

Прыкладанні не ўключаюць чужыя платныя assets, тэкстуры і шрыфты. Купля Gabriel Aguiar або іншага пакета не патрэбная для гэтага плана. Вонкавыя матэрыялы — рэферэнсы/дакументацыя, не runtime-залежнасці.

## 21. Парадак перадачы Codex

Пачаць з `CODEX_HANDOFF.md` і гэтага дакумента. `Sources/` змяшчае код для размяшчэння па паказаных шляхах; `Config/Fire_Defaults.json` — уваходныя параметры; `Tests/validation_report.json` — дакладны статус ужо выкананых праверак.

Не «рэалізаваць увесь агонь» адным некантраляваным патчам. Спачатку сабраць shader/VFX lab і атрымаць compile/import evidence; пасля дадаваць physical interaction па этапах. Канчатковы вынік — не толькі код: таксама сапраўдныя `.vfx`/`.shadergraph`, profile, PlayMode scene, тэсты, captures і standalone measurements.

## 22. Крыніцы і праверка сцвярджэнняў

Крыніцы правераныя 6 верасня 2026. Нумараваныя спасылкі пацвярджаюць API і факты пра праект. Формулы, budgets, структура FireWorld і зыходнікі — арыгінальная інжынерная прапанова гэтага пакета; крыніцы не з’яўляюцца доказам яе прадукцыйнасці або production-гатоўнасці.

[R1] El-Emental — ProjectVersion.txt, зафіксаваны commit.
https://github.com/lomatoq/El-Emental/blob/08afcd32f98ba0e9633b44f60cda6e7a00eed26c/ProjectSettings/ProjectVersion.txt

[R2] El-Emental — Packages/manifest.json, той жа commit.
https://github.com/lomatoq/El-Emental/blob/08afcd32f98ba0e9633b44f60cda6e7a00eed26c/Packages/manifest.json

[R3] El-Emental — Elemental.Presentation.asmdef, той жа commit.
https://github.com/lomatoq/El-Emental/blob/08afcd32f98ba0e9633b44f60cda6e7a00eed26c/Assets/Elemental/Presentation/Elemental.Presentation.asmdef

[R4] El-Emental — Docs/architecture.md, той жа commit.
https://github.com/lomatoq/El-Emental/blob/08afcd32f98ba0e9633b44f60cda6e7a00eed26c/Docs/architecture.md

[R5] Unity 6000.5 — Physics.SphereCast, пачатковы overlap і normal caveat.
https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Physics.SphereCast.html

[R6] Unity — GraphicsBuffer.Target.Structured, stride і packing.
https://docs.unity3d.com/ScriptReference/GraphicsBuffer.Target.Structured.html

[R7] Unity 6000.5 — VisualEffect.SetGraphicsBuffer.
https://docs.unity3d.com/6000.5/Documentation/ScriptReference/VFX.VisualEffect.SetGraphicsBuffer.html

[R8] VFX Graph 17.5 — Custom HLSL nodes, buffers і адсутнасць fragment derivatives у simulation path.
https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.5/manual/CustomHLSL-Common.html

[R9] VFX Graph 17.5 — Custom HLSL Block, inout VFXAttributes.
https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.5/manual/Block-CustomHLSL.html

[R10] VFX Graph 17.5 — Update context, implicit integration і aging.
https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.5/manual/Context-Update.html

[R11] VFX Graph 17.5 — Working with Shader Graph.
https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.5/manual/sg-working-with.html

[R12] Cyanilux — Fire/Flame Shader Breakdown; мастацкі/тэхнічны рэферэнс.
https://www.cyanilux.com/tutorials/fire-shader-breakdown/

[R13] Unity 6000.5 — URP Bloom.
https://docs.unity3d.com/6000.5/Documentation/Manual/urp/post-processing-bloom.html

[R14] VFX Graph 17.5 — System requirements.
https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.5/manual/System-Requirements.html
