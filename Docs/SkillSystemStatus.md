# 기술 시스템 구현 현황

작성: 2026-10-13. 설계는 `Docs/GameDesign/Skills.md`, 이 문서는 구현 상태와 남은 일을 정리한다.
Greybox 검증 420개 통과. 전투와 화면은 직접 확인하지 못했다.

## 구현된 것

### 구조 변환 (장비 → 기술)
- `EquipmentCatalog`를 `SkillCatalog`로 바꿨다. 기술 5종, 슬롯 3개, 최대 Lv10, 빈 슬롯(-1)을 지원한다.
- 반지 3종과 룬 지팡이를 없앴다. 식량·마나·체력 보너스는 이미 레벨업 성장 카드에 있다.
- 슬롯은 **좌클릭 · 우클릭 · Q**다. 일시정지 화면의 조작 안내도 바꿨다.
- 골드를 **빛의 조각**으로 표기했다. 저장 키 `gold`는 그대로 둔다.
- 저장 데이터를 version 3으로 올렸다. version 2 저장은 자동 변환된다. 빛 광선·치유는 이어받고, 룬 지팡이와 반지는 레벨당 빛의 조각 40으로 환급한다.
- 준비 화면에 빈 슬롯, 슬롯 비우기, 빛의 조각으로 기술 강화를 넣었다. 전투 HUD는 빈 슬롯을 처리하고 새 스킬 아이콘을 쓴다.

### 습득 스테이지
| 스테이지 | 기술 |
|---|---|
| 2 | 치유 |
| 7 | 충격파 |
| 15 | 얼음 마법 |
| 16 (2막 보스) | 메테오 |

- 나머지 스테이지는 빛의 조각 보너스(`100 + 50 × 스테이지`)만 준다.
- 새 기술은 비어 있는 첫 슬롯에 자동으로 들어간다. 보상표는 `SkillCatalog.RewardFor`에 있다.

### 상태 이상 (`StatusEffects`)
- **기절**: 제자리에 멈추고 휘두르던 공격을 취소한다.
- **빙결**: 기절에 더해 애니메이션도 멈춘다.
- **밀려남**: 0.25초에 걸쳐 미끄러진다.
- **보스만 면역**: `Combatant.StatusImmune`을 보스에게만 켰다. 보스도 피해는 받는다. 일반 적과 중장갑 유닛은 효과를 받는다.
- 일시정지 때 함께 멈춘다. (`BattleSimulation`)

### 기술 효과
| 기술 | 효과 | 소모 MP | 재사용 | 비고 |
|---|---|---|---|---|
| 빛 광선 | 조준한 적 하나에 피해 18 | 8 | 0.5초 | 기존 |
| 치유 | 영웅 주변 6m 아군 회복 25 | 25 | 5초 | 기존 |
| 충격파 | 반경 4.5m 적에게 피해 20, 기절 1.5초, 3m 밀어냄 | 25 | 8초 | 대상 지정 없음. 슬램 접촉 시점(0.4초) 뒤에 발동 |
| 얼음 마법 | 조준한 적에게 피해 10, 빙결 3초 | 20 | 6초 | 내밀기 접촉 시점(0.3초) 뒤에 발동 |
| 메테오 | 미구현 | 50 | 15초 | |

- 위력 배율은 `1 + 0.2 × (레벨 − 1)`이다.
- 수치는 임시값이다. 상수는 `HeroAbilities`(충격파·얼음)와 `SkillCatalog`(비용·재사용)에 있다.

### 모션
| 기술 | 모션 | 비고 |
|---|---|---|
| 빛 광선 | 지팡이 내밀기 (Thrust) | 재생 속도 1.5배(재사용 0.5초에 맞춤) |
| 얼음 마법 | 지팡이 내밀기 (Thrust) | |
| 충격파 | 두 손 머리 위 내려찍기 (Slam) | 지팡이를 세워 올렸다가 한 번 휘둘러 머리가 앞쪽 땅을 때린다 (접촉 0.46초) |
| 치유 | 오른팔 들기 (Raise) | 지팡이 든 오른팔만 곧게 들어 올리고 지팡이는 세운 채 일직선으로 오르내린다. 몸통·왼팔은 그대로 (접촉 0.35초) |
| 메테오 | 기존 Cast (웅크리기) | 유지 |

- 클립은 `HeroMotionSetup`이 영웅 뼈대에서 계산해 만든다. 메뉴: `Lightbringer > Art > Build Hero Skill Motions`.
- 클립은 `Assets/Art/Characters/FemaleHero/Animations/`에 `FemaleHero_Anim_Thrust / Slam / Raise.anim`으로 저장된다.
- 기술과 클립의 연결은 `VisualOverride.skillMotions`에 저장되고 `ModelAnimationDriver`가 읽는다.
- Thrust·Slam·Raise는 지팡이 방향을 클립이 정한다(`PropStabilizer.SetAuthored`). 지팡이가 수평에 가까울 때 생기던 깜빡임을 없애기 위해서다. Cast는 기존 방식이다.
- 확인용 창: `Lightbringer > Art > Hero Motion Viewer`.

### 스킬 아이콘
- Higgsfield로 생성한 메달리온 일러스트 5개를 `Assets/UI/Art/Skill_*.png`에 넣었다.
- 원본은 `Docs/ArtReference/Skills/`, 프롬프트는 `Docs/ArtReference/SkillIconPrompts.txt`에 있다.
- 빌드: `Lightbringer > UI > Build Skill Icons`.

## 확인이 필요한 것
- 모션 생성 시 클립마다 뼈대를 기준 자세로 되돌린다. 앞 클립이 끝난 자세에서 지팡이 방향을 읽으면 Slam·Raise 방향이 어긋난다(2026-10-13에 고침). 모션을 바꾼 뒤에는 뷰어에서 지팡이 머리가 위인지 확인한다.
- **수치**: 피해, 기절·빙결 시간, 반경은 직접 플레이해 보고 조정한다.

## 다음 작업

작업 순서 제안: 1번(메테오) → 2번(시각 표시) → 3번(정리). 4~5번은 해당 콘텐츠가 생길 때 한다.

### 1. 메테오 구현
현재 상태: `SkillKind.Meteor`는 정의만 있고 효과가 없다. 지금 쓰면 마나 50만 소모되고 Cast 모션만 나온다. 테스트를 위해 현재 저장 데이터에서는 이미 배운 상태로 바꿔 두었다(원본 백업: `campaign-v1.json.before-learn-skills-*`).

**먼저 정할 것**
- **지점 지정 방식**: 추천은 화면 중앙 조준점이 닿는 땅이다(영웅에서 15m 이내로 제한). 대안은 마우스 커서(커서 잠금 때문에 비추천).
- **수치**: 반경, 피해, 낙하까지의 지연 시간. 임시안은 반경 4m, 피해 80, 지연 1.2초(경고 원이 보이는 시간).
- **아군 피해**: 추천은 적만 맞는다. 아군이 맞으면 영웅이 몰려 있는 아군 위에 쓰기 어렵다.
- **보스에게**: 피해는 받는다. 기절·밀려남은 어차피 면역이다.

**구현 항목**
- `HeroAbilities.TryCast`: 메테오는 대상 지정이 필요 없다(`SkillCatalog.NeedsTarget`은 이미 제외). 조준 카메라(`aimCamera`) 정면 방향 레이캐스트로 땅 지점을 구하고, 15m를 넘으면 가장 가까운 유효 지점으로 당긴다. 땅에 닿지 않으면 시전을 거절하고 안내 문구를 띄운다.
- 지연 처리: 이미 있는 `delayed` 목록(`Delayed` 구조체)에 지점(`Vector3`)을 추가해 재사용한다.
- 시전 후 지연 시간이 지나면 반경 안 적에게 피해를 주고 `SpellLanded(id, 지점, 반경)`을 낸다.
- **경고 원**: 지점에 바닥 원을 보여 주고 낙하까지 점점 차오르게 한다. 보스 공격 경고(`BossStrikeVfx` 계열)의 기존 구현을 먼저 읽고 재사용할 수 있는지 본다.
- **낙하 연출**: 위에서 불꽃 꼬리를 끌며 떨어지는 운석, 착지 때 폭발·먼지 고리(`CombatEffects.Burst`, `Ring`, `DustRing` 재사용), 화면 흔들림. 스킬 아이콘의 용암 운석 그림과 어울리게 한다.
- 모션: 기존 Cast(웅크리기) 그대로. 접촉 시점과 시전 시점이 어긋나도 지연이 길어서 문제없다.
- 효과음: 낙하음·폭발음이 없으면 기존 `SoundCue` 중 쓸 것을 고르거나 새로 정한다(`BattleAudio.Cast`는 메테오를 아직 처리하지 않는다).
- 검증 스크립트(`HeroSystemsValidation`): 지점 지정 시전, 지연 후 피해, 반경 밖 무피해, 아군 무피해, 마나 소모, 쿨타임 15초.
- 준비 화면 설명(`SkillCatalog.Descriptions`)과 수치를 구현값에 맞게 갱신한다.

### 2. 빙결·기절 시각 표시
- 지금은 빙결되면 애니메이션만 멈춘다. 얼음 색조(재질 속성 덮어쓰기 또는 얼음 껍질 오브젝트)를 정한다.
- 기절(충격파)에는 머리 위 표시나 휘청임 같은 구분 가능한 표시가 없다.
- 스타일이 입혀진 모델은 자체 팔레트를 쓰므로 `_BaseColor` 덮어쓰기가 먹히는지 먼저 확인한다.

### 3. 정리
- **옛 `Item_*` 아이콘 6개**(`Assets/UI/Art/Item_*.png`)와 이를 만드는 `PreparationArtBuilder`의 `Staff()`, `Ring()`, `Save("Item_*")`. **삭제해도 되는지 확인이 필요하다.** 지우면 `PreparationArtBuilder.Build`가 다시 만들지 않는다.
- 기술 아이콘 `Skill_LightBeam`(광선이 가늘다는 인상)과 `Skill_Shockwave`(빛번짐)가 실제 HUD에서 어떻게 보이는지 확인하고, 필요하면 후보 이미지(각 스킬 2장씩 생성, 일부만 사용)에서 다시 고른다.

### 4. 수치 조정 (직접 플레이 후)
- 충격파: 피해 20, 기절 1.5초, 밀려남 3m, 반경 4.5m.
- 얼음 마법: 피해 10, 빙결 3초.
- 위의 값은 `HeroAbilities`의 상수(`ShockwaveDamage` 등)에 있다. 기술 레벨에 따라 커지는 것은 피해뿐이고, 기절·빙결 시간은 고정이다. 레벨당 효과를 따로 줄지 정해야 한다.
- 밀려나는 거리는 실제 전투에서 확인한다(검증 스크립트의 테스트 유닛에 `CharacterController`가 없다).

### 5. 2막 스테이지가 생길 때
- 얼음(15)과 메테오(16)는 `SkillCatalog.RewardFor`에 이미 들어 있지만, 구현된 스테이지가 8개뿐이라 얻을 수 없다. `CampaignProgress.StageCount`를 늘릴 때 보상 표시(준비 화면)와 검증(`ProgressionValidation`의 "Act 1 teaches...")을 같이 본다.
- 이어받는 저장 데이터의 `cleared`/`unlockedStage` 길이 검사(`IsValid`)가 스테이지 수에 묶여 있다. 스테이지를 늘리면 저장 변환(version 4)이 필요하다. 이 부분은 다른 쪽에서 수정 중인 진행 시스템 파일이므로 먼저 `Docs/GameDesign/Progression.md`와 현재 코드를 읽는다.

### 작업 전 메모
- 같은 파일(`CampaignProgress`, `GrowthCatalog`, `HeroAbilities` 등)을 다른 곳에서도 수정한 적이 있다. 수정 전에 항상 다시 읽는다.
- 컴파일은 Unity 창에 포커스가 있어야 시작된다. 컴파일 뒤 Greybox 검증이 자동으로 돈다(마지막 통과: 420개).
- 모션을 다시 만들면 뷰어(`Lightbringer > Art > Hero Motion Viewer`)에서 "다시 불러오기" 후 확인한다. 지팡이 머리가 위를 향하는지, 불필요하게 회전하지 않는지 본다.

## 주의
- 같은 파일(`CampaignProgress`, `GrowthCatalog`, `HeroAbilities` 등)을 다른 곳에서도 수정한 흔적이 있다. 수정 전에 다시 읽어야 한다.
- 에디터 요청 파일(`Docs/Validation/*.request`)로 도는 작업은 그 클래스에 `[InitializeOnLoad]`가 있어야 처리된다.
- Unity는 창에 포커스가 있을 때만 컴파일한다. 컴파일 후 Greybox 검증이 자동으로 돈다.
- 연쇄 번개, 방벽 등 목록에 없는 기술은 만들지 않는다.

## 변경한 주요 파일
- 새로 만든 것: `Player/SkillCatalog.cs`, `Combat/StatusEffects.cs`, `Editor/HeroMotionSetup.cs`, `Editor/HeroMotionViewer.cs`, `Editor/PreparationArtBuilder.SkillIcons.cs`, `Docs/GameDesign/Skills.md`
- 고친 것: `Player/HeroAbilities.cs`, `Progression/CampaignProgress.cs`, `Progression/CampaignSaveStore.cs`, `Core/CampaignSession.cs`, `Core/BattleSimulation.cs`, `Core/EnemyWaveSpawner.cs`, `Combat/Combatant.cs`, `Combat/UnitCombat.cs`, `Units/UnitPathFollower.cs`, `Units/UnitSupport.cs`, `UI/PreparationView.cs`, `UI/BattleHudView.cs`, `UI/PauseView.cs`, `Visuals/ModelAnimationDriver.cs`, `Visuals/PropStabilizer.cs`, `Visuals/HeroSpellVfx.cs`, `Visuals/ArtStyleLibrary.cs`, `UI/LightbringerHUD.uss`, 검증 스크립트 여러 개
