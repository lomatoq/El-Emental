# Earth Humanoid motion map

Актуальная таблица назначения Humanoid-анимаций для `X Bot`. Gameplay не знает
имён FBX: он выдаёт семантический `EarthTechniqueId`, а
`EarthHumanoidMotionResolver` выбирает стабильный слот. Поэтому клип можно заменить
без изменения физики заклинания.

## Base motion tree

| Состояние | FBX / Motion | Параметры | Loop |
|---|---|---|---|
| Idle | `XBot Neutral Idle` — upright-сегмент из `Standing Idle To Crouch` | `Turn=0`, `Speed=0` | да |
| Walk backward | `X Bot@Walking Backwards` | `0, -2 м/с` | да |
| Walk forward | `X Bot@Walking` | `0, 2 м/с` | да |
| Run | ускоренный `X Bot@Walking` | `0, 6 м/с`, speed `1.65` | да |
| Turn left | `X Bot@Left Turn` | `Turn=-1`, `Speed=0` | да |
| Turn right | зеркальный `X Bot@Left Turn` | `Turn=+1`, `Speed=0` | да |
| Surf enter | `X Bot@Standing Idle To Crouch` | `Surfing=true` | нет |
| Surf hold | `X Bot@Crouch Idle` | `Surfing=true` | да |
| Jump / Fall | `X Bot@Falling` (Jump временный до нового FBX) | `Grounded=false` | да |
| Land | `X Bot@Hard Landing` | `Grounded=true` | нет |
| Moving land | `X Bot@Falling To Roll` | predictive planar landing | нет |
| Hard land | `X Bot@Hard Landing` | `HardLanding=true` | нет |

Все base-клипы используют один canonical Humanoid Avatar из `X Bot.fbx`; кожны
animation-only FBX імпартуецца праз `Copy From Other Avatar`, таму reference pose
таза і каленяў не пералічваецца асобна для кожнага файла. `Injured Idle` больш не
выкарыстоўваецца як звычайны idle. Locomotion — `Freeform Cartesian 2D` па `Turn × Speed`. Скорость считается
относительно движущейся опоры, поэтому подъём платформы не включает ложную ходьбу.
Root-трэкі ўсіх кліпаў выдзяляюцца з позы (`lockRoot* = false`, Y based on feet),
а `Animator.applyRootMotion = false` іх адкідае: Rigidbody і `PlanetMotor`
застаюцца адзінай крыніцай кананічнага перамяшчэння. Гэта не дае падзенню з FBX
падняць бачныя hips на некалькі метраў над фізічнай capsule.

Landing не ждёт визуально запоздалого transition из bool: presentation-only capsule
forecast выбирает soft/moving/hard state за 60–180 ms до ожидаемого контакта, а
фактический `HasStableSupport` только подтверждает recovery. Все значения доступны в
Inspector у `Assets/Elemental/Content/Profiles/CharacterPresentationProfile.asset`.
Там же настраиваются turn enter/release, speed acceleration/deceleration и сглаживание
pelvis на moving support.
Аўтарскі contact-момант (`soft 0.625 s`, `moving 0.533 s`, `hard 0.625 s`) таксама
ляжыць у гэтым профілі. Старт кліпа фазава зрушваецца на predicted TTC, таму
кантактная поза супадае з фізічным кантактам, а не прайгравае паветраны пачатак FBX.

## Earth Magic Upper Body

| EarthPose | Приём | Назначенный FBX |
|---:|---|---|
| 1 | поднятие стены | `X Bot@Standing 2H Magic Attack 05` |
| 2 | поднятие платформы | `X Bot@Standing 2H Magic Area Attack 02` |
| 3 | вытягивание/удержание камня | `X Bot@Standing 2H Cast Spell 01` |
| 4 | бросок тяжёлого камня | `X Bot@Wheelbarrow Dump` |
| 5 | vector push / punch | `X Bot@Lead Jab` |
| 6 | gravity grip / repair | `X Bot@Standing 1H Cast Spell 01` |
| 7 | wave / resonance | `X Bot@Standing 2H Magic Attack 03` |
| 8 | pillar jump | `X Bot@Standing 1H Magic Attack 03` |
| 9 | сборка/раскрытие брони | `X Bot@Standing 2H Cast Spell 01` |
| 10 | залп камней брони | `X Bot@Standing 2H Magic Attack 05` |
| 11 | резервный cast | `X Bot@Standing 1H Cast Spell 01` |

Верхний слой использует Humanoid AvatarMask: ноги продолжают locomotion, пока руки
и корпус колдуют. Категориальные позы не смешиваются через промежуточные номера:
normalized Direct BlendTree получает один из eleven one-hot весов `EarthPose01–11`,
а runtime плавно переводит вес за `0.10 с`. IK лишь уточняет reachable hand targets и
не тянет кисти в далёкую физическую точку. Параметр `EarthMotionTime` фазава
скрабіць толькі upper-body state (`Acquire 0.06 → Strike 0.52 → Sustain 0.68 →
Recover 0.88`): доўгае ўтрыманне магіі захоўвае чытэльную sustain-позу, але base
locomotion працягвае ісці і ніколі не замарожваецца.

## Impact и резерв

| FBX | Сейчас |
|---|---|
| `X Bot@Hit To Side Of Body` | основной upper-body recoil |
| `X Bot@Receiving An Uppercut` | запасной тяжёлый hit |
| `X Bot@Injured Idle` | зарезервирован для injured locomotion |
| `X Bot@Falling To Roll` | moving landing / forward recovery |
| `X Bot@Punch Combo` | зарезервирован для melee/combo |
| `X Bot@Mma Kick` | зарезервирован для melee/kick |
| `Standing 2H Magic Attack 05` без префикса | импортированный alternate, не production slot |

Источник назначения: `Assets/Elemental/Authoring/Editor/EarthHumanoidMotionSetup.cs`.
После изменения таблицы/путей controller пересобирается меню
`Elemental Suite → Character → Rebuild Curated Earth Motion Tree`.
