# Lightbringer 아트 파이프라인

아트 방향은 [ArtDirection.md](ArtDirection.md), 생성 프롬프트는 [../ArtReference/Prompts.md](../ArtReference/Prompts.md)를 본다. 에디터 도구는 `Lightbringer > Art` 메뉴에 있다.


## Lightbringer Art Pass 가이드 (1단계: 교체 구조 · 셰이더 · 오라)

기준 자료: `Docs/GameDesign/3D_Paladog_Project_Handoff_v0.3.docx` 9장, `Docs/ArtReference/Characters/CharacterSheet_FemaleHero_v1.png`.
게임 로직·밸런스·충돌 판정은 바꾸지 않았다. 외형만 교체된다.

### 1. 시작하기

1. Unity 창을 활성화해 스크립트/셰이더를 컴파일한다.
2. `Lightbringer > Art > Build Art Style Assets` 실행 (최초 1회).
   - `Assets/Art/ArtStyleLibrary.asset`, `Assets/Art/Materials/LB_*.mat`, `Assets/Art/LB_BattlePostProcess_v2.asset` 생성
   - `CampaignPrototype.unity`의 `CampaignSession > Art Style`에 자동 연결
   - `Prepare and Play Campaign`도 라이브러리가 없으면 최초 1회 자동 생성·연결한다.
3. `Lightbringer > Prepare and Play Campaign`으로 확인.
4. 비교용으로 그레이박스 캡슐을 보고 싶으면 `CampaignSession`의 `Art Style` 필드를 비우면 된다.

### 2. 구성

| 요소 | 위치 | 내용 |
|---|---|---|
| 스타일 라이브러리 | `Assets/Art/ArtStyleLibrary.asset` | 팔레트 색, 진영 머티리얼, 조명·안개, 오라 색, 후처리, **모델 교체 슬롯** |
| 툰 셰이더 | `Assets/Art/Shaders/LightbringerToon.shader` | 2톤 램프, 그림자 틴트, 림라이트, 금속 하이라이트, 외곽선, 발광, **오라 안 아군 발광** |
| 오라 룬 셰이더 | `LightbringerAuraRunes.shader` | 회전하는 룬 띠·내부 별·외곽 링 (절차적, 텍스처 없음) |
| 파티클 셰이더 | `LightbringerGlow.shader` | 오라 안에서 떠오르는 빛 입자 |
| 공용 도형 빌더 | `Scripts/Visuals/Environment/PropShapes.cs` | 환경·소품·투사체용 버텍스 팔레트 프리미티브 (유닛은 전부 실제 모델 사용) |
| 적용 | `Scripts/Visuals/Units/UnitAppearance.cs` | 소환/스폰 시 캡슐 → 스타일 외형 교체 |

성능 설계:
- 환경 소품 메시는 **메시 1개 · 머티리얼 1개**로 구워지며 팔레트는 버텍스 컬러에 들어 있다.
- 오라 반응은 셰이더가 전역 값으로 계산하므로 병사 수와 무관하다.

### 3. 실제 모델로 교체하는 방법

`ArtStyleLibrary > Model overrides`에 항목을 추가한다.

| 필드 | 설명 |
|---|---|
| `id` | Hero, Swordsman … Dragon, EnemyRaider, EnemyArcher, EnemyStronghold |
| `prefab` | 교체할 모델 프리팹. **피벗 = 발바닥(지면 접점), 정면 = +Z** |
| `localOffset / localEuler / scale` | 미세 조정 |

- 프리팹의 Collider는 자동 제거된다. 판정은 기존 CharacterController와 본진 BoxCollider를 그대로 쓴다.
- 기준 높이: 영웅 약 2.0m, 일반 병사 약 1.6m, 적 본진 약 6 × 4 × 4m.
- 교체한 모델은 자체 머티리얼을 쓴다. 톤을 맞추려면 `Lightbringer/Toon` 셰이더를 쓰고 `Use Baked Vertex Palette`를 끈다.
- 교체 모델의 애니메이션(Animator)은 다음 단계(Presentation)에서 전투 상태와 연결한다.

### 4. 조정 포인트

- 색감: 라이브러리 팔레트 색. 플레이 중 변경은 **새로 소환되는 유닛부터** 반영된다.
- 외곽선 굵기·그림자 색·림: `LB_Allied`, `LB_Enemy` 머티리얼.
- 오라 밝기: 라이브러리 `Aura Color`(HDR), `LB_AuraRunes`의 `Fill Strength`/`Pulse`.
- 블룸·톤매핑·채도: `LB_BattlePostProcess_v2`.
- 안개·환경광: 라이브러리 `Battlefield lighting`.

### 5. 다음 단계 후보

- 전장 환경 그레이박스(고저차, 폐허·성채 블록, 원경)
- 여성 영웅 3D 시험 생성 (Higgsfield, 비용 확인 후) → `Hero` 교체 슬롯에 연결
- 애니메이션 연결, 투사체·마법 VFX (Presentation 단계)


## 무료 유닛 모델 파이프라인 (크레딧 최소화)

병사 1종당 비용: 이미지 약 0.5 크레딧(Claude가 생성) + 나머지 무료.

### 1. 방향별 이미지 (Claude)
`Assets/Art/Characters/<Unit>/Source/`에 생성됨:
- `*_1_Front.png` 정면
- `*_2_Side_FacingRight.png` 측면 (오른쪽을 봄)
- `*_3_Back.png` 후면
- `*_4_Side_FacingLeft.png` 측면 (왼쪽을 봄, 좌우 반전본)

### 2. 3D 변환 — Tripo (tripo3d.ai) 또는 Meshy (meshy.ai) 무료 사용량
1. 로그인 → Image to 3D (가능하면 **Multiview / 여러 장** 모드)
2. 슬롯에 이미지 넣기
   - Front ← `1_Front`, Back ← `3_Back`
   - Left ← `4_Side_FacingLeft`, Right ← `2_Side_FacingRight`
   - 결과의 얼굴/앞뒤가 뒤집혀 있으면 Left/Right 이미지를 서로 바꿔 다시 생성
   - 멀티뷰가 없거나 사용량이 부족하면 `1_Front` 한 장만 넣어도 됨 (병사는 작게 보여서 충분)
3. 텍스처 포함으로 생성, 가능하면 면 수 1~2만(병사는 화면에 많이 나오므로 가볍게)
4. **FBX**로 다운로드 (Mixamo용). FBX가 없으면 GLB도 가능(Claude가 변환 경로 안내)
5. **라이선스 확인:** 무료 플랜 결과물의 상업적 사용 조건(CC BY 등)을 확인

### 3. 리깅 + 동작 — Mixamo (mixamo.com, Adobe 무료 계정)
> 인간형 전용. 드래곤·말처럼 사족 보행이거나 Mixamo를 쓰지 않을 때는 아래 "프로젝트 자체 리깅 파이프라인" 방식을 쓴다.

1. Upload Character → 2단계에서 FBX 업로드 → 턱·손목·팔꿈치·무릎·사타구니 마커 배치 → Next
2. 리깅된 캐릭터를 **T-Pose 상태로 Download** (Format FBX, Skin: With Skin)
3. Animations에서 검색해 각각 Download (Format FBX, **Skin: Without Skin**, In Place 체크 가능하면 체크)
   - 대기: `Sword And Shield Idle`
   - 이동: `Sword And Shield Run`
   - 공격: `Sword And Shield Slash`
   - (다른 병종은 Bow / Magic / Spear 계열로 검색)

### 4. 프로젝트에 넣기
- 리깅된 캐릭터 FBX → `Assets/Art/Characters/<Unit>/`
- 동작 FBX → `Assets/Art/Characters/<Unit>/Animations/Source/`
  - 파일 이름에 `idle`, `run`, `attack`(또는 slash) 포함
- 이후 연결(크기·방향·Legacy 설정·동작·무기 부착)은 Claude가 처리


## 프로젝트 자체 리깅 파이프라인

외부 툴(Mixamo, Blender, 유료 오토리깅) 없이 **Unity 에디터 스크립트만으로** 리그와 애니메이션을 만드는 방식.
검사·궁수·창병·방패병·마법사·사제·기사(+말)·드래곤에 사용. 추가 비용은 없고, 품질은 프로토타입 수준(관절·천 부분은 단단하게 움직임).

- 원본 FBX는 건드리지 않는다. 스크립트가 새 에셋(메시·프리팹·클립)을 만든다.
- Mixamo는 인간형만 되므로 사족 보행·날개가 있는 드래곤, 말에는 이 방식을 쓴다.
- 모든 단계가 스크립트라 다시 실행하면 같은 결과가 나온다.

### 실행

| 대상 | 메뉴 | 스크립트 |
|---|---|---|
| 방패병 · 창병 · 마법사 · 사제 · 기사(+말) · 드래곤 | `Lightbringer/Art/Assign and Rig Tripo Unit Models` | `Editor/Rigs/UnitModelSetup.cs` → `ShieldbearerRigSetup` / `SpearmanRigSetup` / `CasterRigSetup` / `KnightRigSetup` / `DragonRigSetup` 호출 |
| 검사 | `Lightbringer/Art/Build Swordsman Rig and Weapons` | `Editor/Rigs/SwordsmanRigSetup.cs` |
| 궁수 | `Lightbringer/Art/Build Archer Rig and Bow` | `Editor/Rigs/ArcherRigSetup.cs` |


### 전체 흐름

1. **소스 준비** — Tripo zip을 풀어 `Assets/Art/Characters/<Unit>/<Unit>.fbx`와 `<Unit>.fbm/`(텍스처)로 넣는다.
2. **재질** — basecolor / normal / metallic / roughness로 URP Lit 재질 생성.
   metallic(R)과 1 − roughness(A)를 합쳐 `<Unit>_MetallicSmoothness.png`로 굽는다. FBX의 원래 재질은 이 재질로 리맵.
3. **배치 (유닛 공간으로 맞추기)** — 아래 "배치" 참고. 결과: 발이 원점(y=0), 정면이 +Z, 게임 크기.
4. **메시 굽기** — 배치된 상태로 메시를 하나로 합쳐 `Rigged/<Unit>_Skinned.asset`으로 저장 (Git LFS 대상).
5. **뼈대** — 측정한 랜드마크 좌표에 뼈를 배치. 모든 뼈의 기본 회전은 identity.
6. **스킨 가중치** — 부위 규칙 + 가장 가까운 뼈 구간 거리로 정점마다 최대 4개 뼈 가중치.
7. **애니메이션** — 대기 / 이동 / 액션 클립을 코드로 생성 (Legacy 클립, 30fps).
8. **프리팹 + 연결** — `Rigged/<Unit>_Rigged.prefab` 저장, `ArtStyleLibrary`의 해당 `VisualId` 오버라이드에 프리팹과 클립 3개 연결.
9. **검증** — 가중치 합 = 1, 바인드 포즈 수 일치, 클립마다 실제로 정점이 움직이는지, 걷기에서 발이 움직이는지 확인 후 리포트 작성.

게임에서는 `UnitAppearance`가 프리팹을 붙이고 `ModelAnimationDriver`가 속도에 따라 대기/이동, `UnitCombat.Attacked`(공격)·`UnitSupport.Pulsed`(회복) 이벤트에 액션 클립을 재생한다.

### 배치

Tripo FBX는 위쪽 축이 +Z로 나온다. 먼저 X축 −90° 회전으로 세운 뒤:

- **인간형** (방패병, 창병, 마법사, 사제, 기사 탑승자) — 바닥 3% 높이의 정점에서 앞뒤로 더 멀리 나간 쪽(발끝)을 정면으로 판단. 높이로 크기 맞춤.
- **사족 보행** (드래곤, 말)
  1. 아래쪽 35% 정점의 주축(PCA)으로 대략적인 몸 방향을 구하고, 양 끝 15% 중 더 높은 쪽을 머리로 판단
  2. 바닥 4% 높이 정점을 격자로 묶어 **발 4개 덩어리**를 찾는다 (길게 바닥에 깔린 꼬리는 크기로 제외)
  3. 앞발 두 개 중점 − 뒷발 두 개 중점 = 정면 방향, 발 4개 중심 = 몸 중심
  - 드래곤은 꼬리가 말려 있어서 1번만으로는 약 30° 틀어졌다. 발 기준으로 바꿔 해결.
- 크기: 드래곤은 코~꼬리 길이 4 m, 말은 높이 2.1 m, 방패병·창병·마법사·사제 1.7 m, 탑승 기사 1.65 m.

### 드래곤 리그 (`DragonRigSetup.cs`)

#### 뼈대 (23개 스킨 뼈 + 비스킨 `Spine`)

좌표는 유닛 공간(m). 시작점 → 끝점.

| 뼈 | 부모 | 시작 → 끝 | 역할 |
|---|---|---|---|
| Spine | root | (0, 1, −0.25) | 몸 전체 출렁임/기울기, 스킨 없음 |
| Hips | Spine | (0, 1, −0.3) → (0, 0.95, −1) | 엉덩이 |
| Chest | Spine | (0, 1.02, −0.2) → (0, 1.2, 0.35) | 가슴 |
| Neck | Chest | (0, 1.2, 0.3) → (0, 1.65, 0.3) | 목 (거의 수직) |
| Head | Neck | (0, 1.65, 0.3) → (0, 1.78, 1.02) | 머리, 자식 `Mouth` (0, 1.72, 1.06) |
| Tail1~5 | Hips → 체인 | (0.1, 0.85, −1) … (0.9, 0.45, −2.8) | +X 쪽으로 말린 꼬리 |
| ○FrontUpper/Lower/Foot | Chest | 어깨 (±0.26, 1.05, 0.42) → 팔꿈치 → 손목 → 발끝 | 앞다리 |
| ○HindUpper/Lower/Foot | Hips | 엉덩이 (±0.18, 0.95, −0.68) → 무릎 → 발목 → 발끝 | 뒷다리 |
| ○Wing | Chest | (±0.35, 1.3, 0) → (±2.5, 1.6, −0.35) | 날개 (한 장 통째로) |

○ = Left / Right. 랜드마크는 배치된 메시를 높이·길이별로 잘라 다리 중심선, 목·머리 윤곽, 날개 범위, 꼬리 경로를 측정해서 정했다.

#### 스킨 가중치

정점마다 먼저 부위를 정하고, 허용된 뼈들 중 **가장 가까운 뼈 구간 거리**로 상위 4개를 고른 뒤
`exp(−(d² − 최소d²) / 0.012)`로 부드럽게 섞는다 (검사는 0.006, 몸집이 커서 더 넓게).

- 왼쪽 정점은 왼쪽 뼈만, 오른쪽 정점은 오른쪽 뼈만
- **날개**: `|x| > 0.45 & y > 1.2`, 또는 `|x| > 0.6 & y > 0.75`, 또는 `|x| > 0.85`, 그리고 `−0.7 < z < 0.4` → 날개 뼈 + Chest만
  - 마지막 `|x| > 0.85` 조건은 날개 끝 발톱이 낮게 늘어져 있어 추가 (없으면 일부 정점이 제자리에 남음)
- **머리**: 날개가 아니고 `y > 1.7 & |x| < 0.45 & z > −0.2` → Head만 (뿔이 머리와 같이 움직이게)
- 다리 뼈는 `y ≤ 1.2`, 꼬리 뼈는 `z ≤ −0.6`, 목·머리 뼈는 `y ≥ 1.15 & z ≥ −0.15`에서만 허용

#### 애니메이션 클립

각 뼈의 회전을 수식으로 계산해 30fps로 키를 찍는다. 기본 회전이 identity라 축은 유닛 축과 같다
(+X 회전 = 늘어진 다리가 뒤로, 머리는 아래로).

| 클립 | 길이 | 내용 |
|---|---|---|
| `Dragon_Idle` | 2.4 s 반복 | 가슴 숨쉬기, 목·머리 천천히 좌우, 꼬리 흔들기, 날개 접은 채 미세하게 |
| `Dragon_Walk` | 1.1 s 반복 | 대각선 걸음 (왼앞+오른뒤 / 오른앞+왼뒤). 윗다리 ±24°(앞)/±20°(뒤), 앞으로 내딛을 때 아랫다리 굽힘, 발은 반대로 돌려 평평하게. 몸 위아래 출렁임, 좌우 기울기, 목 끄덕임, 꼬리 물결 |
| `Dragon_Breath` | 1.0 s 1회 | 0~0.12 목을 뒤로 젖힘 → 0.12~0.25 앞으로 내밂(목 16°, 머리 2°, 코끝 약 20° 아래) → 0.7까지 유지 → 복귀. 날개는 70° 접힘에서 25°까지 펼침, 꼬리 들어올림, 몸 살짝 낮춤 |

- **날개 접기**: 모델 원본은 날개를 양옆으로 활짝 편 자세(폭 5.5 m). 모든 클립에서 날개 뼈를 Y축으로 70° 뒤로 젖혀 몸 옆에 붙인다 (폭 약 1.6 m).
  그래서 프리팹을 애니메이션 없이 보면 날개가 펴져 있는 게 정상이다.
- 이동 클립 기준 속도 3 m/s (`moveClipSpeed`). 실제 속도에 맞춰 재생 속도가 0.5~1.6배로 바뀐다.
- 액션 클립은 공격 간격의 90% 길이로 맞춰 재생된다.

#### 불 뿜기 (`Visuals/Vfx/DragonBreathVfx.cs`)

- 프리팹 루트에 붙어 있고 `UnitCombat.Attacked`를 구독. 피해는 그 순간 이미 들어가고, 이건 연출만 담당.
- 공격 0.12초 후부터 0.5초 동안 `Mouth`에서 대상 조준점 쪽으로 불길 파티클 (입을 따라 움직이고, 입자는 월드 공간).
  입자 속도 = 거리 / 0.45초 → 사거리 8 m까지 도달.
- 불길 도착 시점에 대상 발밑에 불꽃 폭발 (45개).
- 재질 `Rigged/DragonFire.mat` = 기존 `Lightbringer/Glow Particle`(가산) 셰이더. 흰색 → 주황 → 짙은 빨강 → 투명.

### 공용 코드

- `Editor/Rigs/GeneratedRig.cs` — 뼈 생성, 가장 가까운 뼈 구간 기반 스킨(영향도 배율로 부위 사이를 부드럽게 섞기 가능), Legacy 클립 굽기, 검증 도구(가중치 확인, 정점 이동량, 손바닥 중심), 회전체 메시(`Lathe`: 자루·원뿔·크리스털).
- `Editor/Rigs/RobedHumanoid.cs` — 망토·코트·로브를 입은 A 포즈 인간형(방패병·마법사·사제)의 뼈대와 가중치. 유닛마다 `Spec`(측정값)만 다르다.
  - x는 머리 중심선, z는 머리 깊이 기준 (마법사는 코트 자락이 뒤로 길어 몸이 메시 중심보다 앞에 있음).
  - 팔·다리는 뼈 선에서 반경 r 안의 정점을 갖고, r~2r 구간에서 몸통/코트로 점점 넘긴다 → 소매·코트 자락·로브는 가슴·엉덩이에 걸린 채 팔다리가 안에서 움직인다.
  - 코트 뼈: `Tabard`(앞자락), `Cape`(뒷자락, 가슴에서), 좌우 `Skirt`(옆자락, 걸을 때 다리 흔들림의 절반).
  - 손바닥(손목 아래 손 부분)은 손 뼈에 100% → 무기 손잡이와 오차 0.
- 무기는 손 뼈 자식. 클립에서 손 뼈 회전 = (팔 체인 회전)⁻¹ × 목표 방향이라, 팔 자세와 상관없이 무기가 유닛 공간에서 원하는 방향을 향한다.

### 유닛별 리그 요약

| 유닛 | 스크립트 | 뼈 | 무기 | 클립 (대기 / 이동 / 액션) |
|---|---|---|---|---|
| 창병 | `SpearmanRigSetup.cs` | 21 | 창 2.35 m (오른손) | Idle / Run / Thrust — 당겼다가 왼발 내딛으며 찌르기, 창끝 1.94 m 앞 |
| 방패병 | `ShieldbearerRigSetup.cs` | 23 | 타워 실드 0.8×1.4 m (왼손, 몸 앞을 가림) + 플랜지 메이스 (오른손) | Idle / Walk / Bash — 메이스 들어 내려찍으며 방패 밀기, 메이스 0.99 m 앞 |
| 마법사 | `CasterRigSetup.cs` | 23 | 어두운 나무 지팡이 + 금 발톱 + 빛나는 푸른 룬 크리스털 | Idle / Walk / Cast — 지팡이를 뒤로 모았다가 크리스털을 적에게 내밂, 왼손이 주문을 이끔 |
| 사제 | `CasterRigSetup.cs` | 23 | 상아·금 성스러운 지팡이 + 태양 광륜 + 빛나는 성구 | Idle / Walk / Bless — 지팡이를 머리 위로 들고 왼손을 펼쳐 축복 (머리 2.63 m) |
| 기사 | `KnightRigSetup.cs` | 말 19 + 기수 13 | 랜스 3.1 m (손잡이 보호대·제비꼬리 깃발, 오른손) + 연 모양 방패 (왼손) | Idle / Gallop / Charge — 랜스를 겨드랑이에 끼워 수평으로 내리고 돌격, 창끝 2.2 m 앞 |

- 지팡이는 대기 때 바닥을 짚는다(물미 높이 약 0.1 m).
- 사제의 축복 클립은 `UnitSupport.Pulsed`(다친 아군을 실제로 회복시킨 펄스)에 맞춰 재생된다. 회복 간격의 90% 길이로 맞춤.

### 기사 (말 + 기수)

1. **앉은 자세 굽기** (`UnitModelSetup.BuildMounted`) — 말과 기사가 따로 생성되어 있어서, 기사 메시를 앉은 자세로 직접 변형해 저장한다 (`Knight/Mounted/Knight_Seated.asset`).
   - 말 등선(가운데 폭 0.07 m)의 높이를 앞뒤로 훑어, 뒷발~앞발 사이 45~85% 구간에서 가장 낮은 곳 = 안장 자리
   - 안장 아래 몸통 폭(85 퍼센타일)을 재서 무릎이 바깥으로 벌어질 거리 계산
   - 허벅지: 엉덩이(키의 0.47) 기준 60° 앞으로 + 양옆으로 벌림, 정강이: 무릎(0.30)을 따라 이동 후 10° 뒤로
   - `Knight_Mounted.prefab` = 말 FBX + 앉은 기사 (정적, 리그의 소스로 사용)
2. **리그** (`KnightRigSetup`) — 정적 프리팹을 읽어 말과 기수를 각각 스킨한다.
   - 말: 스킨 없는 `Spine`(질주 출렁임·앞뒤 흔들림) → `Hips`/`Chest`, `Neck`→`Head`, `Tail1`→`Tail2`, 다리 4개 × 3마디(윗다리 → 무릎/비절 → 구절 → 발굽). 발굽·구절·무릎 위치는 높이 띠별 정점 중심으로 측정.
   - 기수: `RiderHips`가 말 `Spine`의 자식이라 말과 함께 출렁인다. 앉은 다리와 말 옆구리에 걸친 망토 자락은 `RiderHips`에 고정, 상체·팔만 움직임.
   - 질주: 대각선 쌍(왼앞+오른뒤 / 오른앞+왼뒤), 목 끄덕임, 꼬리 들어 올림, 기수 앞으로 기울임. 돌격 클립은 같은 걸음 2회 + 랜스 수평.
   - 검증: 랜스 물미가 말 몸통에 박히지 않는지(몸통 바깥 또는 등 위), 질주 때 구절 이동량, 돌격 때 창끝 위치.

### 검증 방법

- 자동: 리포트 파일 (가중치 정상화, 클립별 최대 정점 이동량, 무기-손바닥 오차, 무기 끝 위치·지면 관통, 입 위치, 보폭).
- 개발 중 측정: 배치된 메시의 정점을 정면·측면·위에서 ASCII 밀도 맵과 높이별 단면으로 찍어 관절 위치를 정했다 (화면 캡처 아님). 확인 후 임시 스크립트는 삭제.
- 최종 확인은 Play에서 직접.

### 한계와 다음 작업

- 드래곤 날개가 한 장짜리라 접힐 때 막이 접히지 않고 통째로 회전한다. 제대로 접으려면 날개 관절 뼈 추가 필요.
- 드래곤 입 벌림(턱 뼈) 없음.
- 손가락 뼈 없음: A 포즈의 펼친 손 그대로 무기를 쥔다 (가까이서 보면 손가락이 손잡이를 감싸지 않음).
- 관절·소매·로브의 늘어남, 방패병 다리와 코트 겹침은 수동으로 확인 필요.
- 정점 수가 많음 (기사+말 59만, 사제 31만, 방패병 30만, 마법사 28만, 창병 19만, 드래곤 13.5만) → Step 6 벤치마크 때 최적화.

### Higgsfield 리깅 적 (적 궁병)

인간형 적은 자체 리그 대신 Higgsfield `3d_rigging`(Meshy 오토리깅)을 쓴다. 호출당 5 크레딧, 클립 1개 포함 8 크레딧.

1. GPT 원화(무기 없는 A포즈, 주먹 쥔 손) → Tripo → GLB. 원본은 `<Enemy>/Source/`에 보관.
2. GLB를 Higgsfield에 업로드 → `3d_rigging`, `height_meters` 1.7, `enable_animation` + 클립 id (대기 0 `Idle`, 이동 30 `Casual_Walk`).
   호출마다 같은 메시·뼈대에 클립 1개가 든 GLB가 나온다 → `<Enemy>/Animations/Source/<Enemy>_Anim_Idle.glb`, `_Walk.glb`.
   - 대기 GLB로 프리팹 생성, 메시를 군중용으로 축소 (24만 → 약 2만 삼각형, 가중치·UV 유지), 고정 bounds
   - `ArtModelImport.Assign` / `AssignAnimations`로 오버라이드와 대기·이동 클립 연결
   - Meshy 아마추어는 0.01 축척(엉덩이 이동이 cm 단위)이라 `moveClipSpeed`는 직접 지정 (1.25 m/s × 축척 × 1.2)
   - 활: 아군 궁수 활 메시 재사용, 적군 색. `LeftHand`에 `PropStabilizer`로 붙임 (`PropAttachment.follow` 0, `castLean` 0 → 항상 똑바로 서서 정면을 향함, `gripFraction` 0.47). 화살통은 `Spine` 자식
   - 사격 클립 `EnemyArcher_Shoot` (1초, 크레딧 0): 아군 궁수와 같은 타이밍 — 0에서 발사(적 궁병 strike phase 0) → 반동 → 활을 당겨와 시위 걸기 → 밀고 당기며 턱까지 → 유지
     - 척추 3개·목·머리·양팔만 키를 찍음 (엉덩이·다리 없음) → 레거시 레이어 블렌딩으로 다리는 대기/걷기 모션캡처 유지
     - Meshy 뼈는 기본 회전이 identity가 아니므로 모든 변화를 월드 회전으로 bind 위에 곱함 (`bone.rotation = delta * bone.rotation`). 팔은 2본 IK, 손바닥 = 손목 + 전완 방향
     - 시위·화살: 활 프리팹의 `Visuals/BowString` — 오른손바닥이 시위 정지점 근처(0.08 활 단위)에 오면 잡고, 이후 시위·화살이 손을 따라감. `UnitCombat.Attacked`(발사) 또는 손이 당김 영역을 벗어나면 놓음

### 적 5종 자체 리깅 (Raider · Swarmling · Brute · Shaman · Warlord)


- 소스: `Assets/Art/Characters/Enemy<Id>/Source/Enemy<Id>_Source.glb` (Tripo, 1 m, 무기 없는 A포즈, 약 3만 삼각형). 원본은 수정하지 않음
- 배치: 1.7 m로 키우고 발 중심을 원점에. 방향은 발목 기준으로 발끝이 발뒤꿈치보다 훨씬 앞에 있으면 +Z(glTF 기본). 게임 크기는 오버라이드 `scale`로 (Raider 1.7, Swarmling 1.2, Brute 2.1, Shaman 1.7, Warlord 2.3 m, 여기에 부대 배율 1.2)
- **랜드마크 자동 측정** → `RobedHumanoid.Spec`: A포즈라 팔이 몸통과 떨어져 있으므로 높이별 단면에서 가장 넓은 틈으로 몸통/팔을 나눔 → 겨드랑이·손 끝·팔 중심선(어깨 관절까지 연장). 다리는 가운데선이 비는 가장 높은 단면(가랑이), 발목, 발끝
- 로브·갑옷 치마(Shaman, Warlord)만 `Spec.Skirt`를 켬. 나머지는 엉덩이 부분이 Hips와 다리만 따라감
- 무기(코드 생성, 손 뼈 자식): Raider 검+원형 방패, Swarmling 단검, Brute 가시 몽둥이, Shaman 해골 지팡이(붉은 수정), Warlord 대검(붉은 날)
- 클립: 대기 2.4초 / 이동(Swarmling은 달리기 0.7초) / 공격 1초. 공격 타격 시점 = `EnemyCatalog.StrikePhase`. 이동 클립 속도 = 2 × 2 × 다리 길이 × sin(보폭각) / 주기 × 크기
- 군중용 메시로 축소 (약 1.5만, Warlord 2.6만 삼각형), 검증은 원본 메시로
- 검증: 가중치, 클립별 정점 이동, 무기-손바닥 오차, 무기 지면 관통, 공격 시점 무기 끝 위치

### 새 유닛에 적용할 때

1. 소스를 `Assets/Art/Characters/<Unit>/`에 넣고 `UnitModelSetup`에 배치 단계 추가
2. 배치된 메시를 높이별 단면으로 측정해 랜드마크(관절 위치) 결정
3. 코트·로브 인간형이면 `RobedHumanoid.Spec`만 새로 채운다 (`CasterRigSetup` 참고). 그 외 체형은 `GeneratedRig`로 뼈대 / 부위 규칙 / `Pose`를 작성
4. 리포트 확인 → Play 확인


## 무기 3D 제작 파이프라인 (적 무기 · 일시정지 · 진단 도구)

2026-10-12 작업 기록. 적 6종 리그와 무기를 정리하면서 확정한 방식과, 아직 남은 일을 적는다.

### 1. 현재 상태

#### 적 무기

| 무기 | 방식 | 위치 |
|---|---|---|
| Raider 검 | 코드 생성 (레퍼런스 기반) | `WeaponMeshes.SwordSpecRaider` |
| Raider 원형 방패 | 코드 생성 | `WeaponMeshes.RoundShield` |
| 고블린(Swarm) 단검 | 코드 생성 | `WeaponMeshes.DaggerSpec` |
| Brute 가시 몽둥이 | 코드 생성 (0.85배로 사용) | `WeaponMeshes.SpikedClub` |
| Shaman 해골 지팡이 | Tripo 모델 그대로 | `Assets/Art/Characters/EnemyShaman/Weapon/EnemyShaman_Staff.glb` |
| Boss 대검 | Tripo 모델 그대로 | `Assets/Art/Characters/EnemyBoss/Weapon/EnemyBoss_Greatsword.glb` |

- 코드 생성 무기는 `Docs/ArtReference/Enemies/*/*_03_Weapon_*.png` 레퍼런스를 보고 만들었다.
- 가져온 Tripo 무기는 다운로드 폴더의 `warlord+sword+3d+model.glb`, `shaman+staff+3d+model.glb`를 복사한 것이다. 원본은 그대로 둔다.
- 보스 대검은 Tripo 모델을 사용한다. 사용하지 않는 이전 코드 대검 설정은 정리했다.

#### 기타

- 일시정지/재개: Esc 또는 P. `CampaignSession.SetPaused`, `UI/PauseView.cs`. 2026-10-12 실제 Play 자동 검사에서 정지/재개 및 BGM 음량 전환을 확인했다. 키 입력 자체의 수동 검증과는 구분한다.
- 실제 전투에서 휘두를 때 무기 방향과 위치는 Play로 확인하지 않았다. 지금까지는 에디터 안 정지 화면 렌더링으로만 확인했다.

### 2. 코드 생성 무기 (`Assets/Scripts/Editor/Weapons/WeaponMeshes.cs`)

- 좌표: 손잡이 중심이 원점, 날/머리는 +Y. 베는 무기는 **날이 ±Z(휘두르는 앞쪽)**, 넓은 면이 ±X. 원형 방패는 앞면이 +Z.
- 날 계열(검, 단검, 대검)은 `SwordSpec` 수치만 바꿔 같은 생성기 `WeaponMeshes.Sword`로 만든다. 날 단면은 마름모(가운데 능선), 날 끝은 둥글게 모이고, 양쪽 날에 이 빠진 자국이 있다. 가드, 손잡이 링, 감긴 가죽, 폼멜이 같이 나온다.
- 면이 또렷하게 보이도록 평면 셰이딩이다. UV는 박스 매핑이라 타일링 텍스처가 모든 면에서 같게 보인다.
- 텍스처는 코드로 생성해 `Assets/Art/Weapons/Textures/`에 저장한다. `HammeredIron`(망치 자국 철), `WoodGrain`(낡은 판자). 파일이 이미 있으면 다시 만들지 않으므로 바꾸려면 파일을 지운다.
- 진영 차이는 재질로 낸다 (`EnemyRigSetup.Materials`의 `HammeredIron`, `DarkHammered`, `WoodPlank`, `WrapLeather` 등).

### 3. Tripo 무기를 쓰는 법 (`EnemyRigSetup.ImportedWeapon`)

1. GLB를 `Assets/Art/Characters/<Enemy>/Weapon/`에 둔다. Tripo는 높이 1 m로 정규화돼 나온다.
2. 높이별 단면으로 손잡이 위치를 잰다. 대검은 높이의 17.5%, 지팡이는 47%.
3. `length`(실제 길이)로 키우고, 손잡이 점을 원점으로 옮기고, `yaw`로 돌려 메시 에셋으로 저장한다. 재질은 GLB 것을 그대로 쓴다.
4. 날 무기는 GLB에서 넓은 면이 ±Z를 보고 있어 `yaw` 90°로 날을 앞으로 돌린다.

### 4. 반드시 알아야 할 함정

- **NaN 정점이 하나라도 있으면 Unity가 메시를 통째로 버린다.** 날 끝의 `cos(π/2)`가 -4e-8이 되어 거듭제곱에서 NaN이 나왔다. 0 이상으로 고정했다. 메시가 비어 저장되면 정점 수가 0인지부터 본다.
- **이미 쓰는 메시 에셋을 덮어쓸 때는 `EditorUtility.CopySerialized`를 쓰면 안 된다.** 렌더러가 예전 GPU 데이터를 계속 그려서 몸이 사라지거나 예전 모양이 나온다. `RigBuildUtility.SaveAsset`이 Mesh API(`CopyMesh`)로 덮어쓴다. 새 코드는 반드시 `SaveAsset`을 쓴다.
- **에셋을 지우고 같은 GUID로 다시 만들지 않는다.** 에디터 메모리의 참조가 `MISSING`이 되어 재시작 전까지 풀리지 않는다.
- 날이 사라지거나 몸이 안 보일 때 에디터를 재시작하면 디스크에서 새로 읽는다. 파일이 정상이면 재시작으로 풀린다.
- 무기 길이가 늘면 공격 동작에서 바닥을 뚫는 검증(`-0.03 m` 미만)이 실패할 수 있다. Brute 몽둥이가 이래서 0.85배다.

### 5. 진단과 작업 요청 파일

에디터 안에서 렌더링해 보려면 요청 파일을 만든다. 에디터가 놀고 있고(Play 중이 아니고 컴파일 중이 아닐 때) 창이 포커스를 받은 상태여야 처리한다.

| 요청 파일 | 하는 일 | 결과 |
|---|---|---|

- 무기 확대 이미지는 무기마다 두 칸이다: +Z(앞)에서 본 것, +X(옆)에서 본 것. 날 무기는 앞 칸에서 얇은 선, 옆 칸에서 넓은 면이 보이면 맞게 돌아간 것이다.
- 스크립트를 고친 직후에는 Unity 창을 한 번 클릭해야 컴파일된다. Play 중에 스크립트를 고치면 코드가 다시 불려지면서 `EnemyWaveSpawner`가 `IndexOutOfRange`를 반복해서 낸다. Play를 끝내면 사라진다.

### 6. 일시정지 (`Esc` / `P`)

- `CampaignSession`이 `userPaused`와 `Time.timeScale = 0`으로 전투를 멈춘다. 레벨업 카드 선택 때 쓰던 `BattleSimulation` 일시정지를 재사용한다.
- 레벨업 선택 중이나 전투 종료 후에는 일시정지되지 않는다. 플레이 기록의 전투 시간은 멈춘 동안 늘지 않는다.
- 전에는 Esc가 커서만 풀었는데 이제는 일시정지다. `ThirdPersonCamera`는 재개한 그 프레임의 Esc를 무시한다.

### 7. 남은 일

1. 아군 무기를 같은 방식으로 제작. 겹치는 것은 재활용한다. 한손검과 원형 방패(검사와 Raider), 활과 화살과 화살통(두 궁수). 지팡이와 장식 머리는 공유하지 않는다. 날 무기는 `SwordSpec`만 새로 쓰면 된다.
2. Play로 확인: 일시정지/재개, 적 무기의 휘두르는 방향과 위치.
3. Raider 검 다듬기: 철 텍스처를 레퍼런스처럼 넓고 옅게, 가운데 능선을 더 선명하게, 가드 위 V자 문양.
4. Shaman 지팡이에 붉은 수정 발광이 필요하면 별도 파트로 추가 (현재는 Tripo 모델 그대로).


## 본진 장식과 폐허 에셋

사용자가 다운로드한 Tripo FBX 8종을 `Assets/Art/Environment/Props/`에 가져왔다. 다운로드 원본은 보존한다. 모델의 축·크기·바닥 중심점을 맞춘 프리팹과 URP Lit 재질을 사용하며, `ArtStyleLibrary.battlefieldProps`가 배포용 에셋 참조를 가진다.

| 프리팹 | 원본 ZIP | 배치 용도 |
|---|---|---|
| AlliedBeacon | 아군 등대.zip | 아군 성채 양옆의 등대 |
| AlliedMonument | 아군ornate+monument+3d+model.zip | 아군 본진 옆 기념비 |
| AlliedBarrel | 아군barrel.zip | 아군 보급품 군집 |
| AlliedChest | 아군treasure+chest+3d+model.zip | 보급품 옆 보물상자 |
| EnemyBoneFence | 적군spikedbonefence.zip | 적군 진지 측면의 짧은 울타리 |
| EnemyAltar | 적군stone+skull+altar+3d+model.zip | 적군 본진 측면 제단 |
| RuinColumn | ancient+column+3d+model.zip | 전장 양옆 유적의 기둥 |
| RuinArch | ancient+arch+3d+model.zip | 전장 양옆 유적의 아치 |

### 배치 기준

- 캠페인의 아군 본진, 적군 본진, 서쪽 유적, 동쪽 유적 네 군집으로 구성한다.
- 프리팹의 전체 가로·세로 크기로 바닥 점유 반경을 계산한다. 진격로 중심선으로부터 오브젝트 가장자리까지 4.5m 이상 확보한다.
- 본진 건물, 출입구, 다른 장식의 점유 영역을 피한다. 배치가 불가능하면 해당 위치의 소품은 생략한다.
- 나무·수풀·잔디 배치는 먼저 확보한 장식 구역을 피한다. 큰 나무는 추가 여유를 둔다.
- 지형 삼각형의 실제 높이에 맞춰 접지하고, 받침대가 뜰 정도의 경사에는 놓지 않는다.
- 보물상자·제단 등은 환경 장식이다. 기존 Path, 소환, 전투 목표와 콜라이더는 그대로 사용한다.
- 기존 절차적 폐허 생성 코드 및 원경의 단순 성채·첨탑 장식을 제거했다. 지형, 길, 산과 기존 숲 시스템은 유지한다.
- 기존 산림 프로필의 스테이지 범위는 유지한다. 새 본진·유적 장식은 모든 캠페인 스테이지에 적용한다.

### 재질·성능

원본 Base Color와 Normal, Metallic 및 Roughness를 사용한다. URP용으로 Metallic은 R, `1 - Roughness`는 A에 패킹한다. 원본의 중복 RM 텍스처는 가져오지 않는다. GPU 인스턴싱을 켠 공유 재질과 공유 메시를 사용하며, 런타임 메시 복제 및 장식용 Update는 없다. FBX의 Read/Write는 끈다.

원본의 고밀도 메시는 기존 `CrowdMeshOptimizer`와 Unity Mesh LOD 기능으로 게임용 메시를 생성한다. UV·노멀·접선을 보존하며 거리별 Mesh LOD도 포함한다. 원본 FBX는 수정하지 않는다. 원본 모델과 `*_Optimized.asset`은 Git LFS로 관리한다. 외부 플러그인이나 OS 전용 런타임 API는 추가하지 않는다.

### Unity에서 확인

이미 설정된 프로젝트에서는 Play를 다시 시작하면 적용된다.

- `Lightbringer > Art > Set Up Battlefield Props`: 가져온 원본에서 프리팹·재질·프로필을 재생성한다.
- `Lightbringer > Art > Validate and Capture Battlefield Props`: 개인 저장 데이터를 사용하지 않고 1·4·8 스테이지의 배치를 검사하고 스크린샷을 만든다.
- 프리팹은 `Assets/Art/Environment/Props/`의 각 에셋 폴더에서 확인할 수 있다.
- 배치 코드: `BattlefieldPropDressing.cs`. 크기를 수정하면 프로필의 `size`도 실제 프리팹 크기에 맞춰 갱신해야 한다.


### 적용 파일과 배치 검증

- 추가: `BattlefieldPropProfile.cs`, `BattlefieldPropDressing.cs`, `BattlefieldPropSetup.cs`, 8종 에셋 폴더와 `BattlefieldProps.asset`.
- 수정: `BattlefieldEnvironment.cs`, `ForestEnvironmentDressing.cs`, `ArtStyleLibrary.cs` 및 라이브러리 에셋, `CrowdMeshOptimizer.cs`, `.gitattributes`.
- 검붉은 수정 바위는 아트 방향에 맞지 않아 모델·재질·텍스처·배치·레퍼런스에서 제거했다. 최종 보스 핵은 기존 회색상자 모델을 사용한다. 배치 수는 현재 스테이지 구성에 따라 달라진다.
- 전선 수에 따라 유적의 좌우 위치를 조정한다. 초반에는 중심에서 14m, 중반에는 23m, 후반에는 32m 떨어진 곳에 배치한다.
- 개별 에셋과 본진·유적·전체 전장의 캡처를 확인했다. 메시 통계는 추가 LOD의 저장 인덱스를 포함하므로 프레임당 실제 렌더링 삼각형 수와는 다르다.
- 2026-10-12: 기본 검증 315개 항목 및 실제 Play 모드 캠페인 검증을 통과했다. 소환·이동·전투·승패·3개 전선·100/300 유닛 전투를 확인했다. `CampaignPlayValidation.cs`의 드래곤 이름 검사를 한글화된 `UnitCatalog` 이름과 일치하도록 수정했다.


## Stage 1 — Fantasy Worlds Forest

사용 에셋: **Fantasy Worlds: Forest FREE - Stylized Forest Environment Open World**, TriForge Assets, v1.0.
공식 페이지: https://assetstore.unity.com/packages/slug/282610
취득 경로: 사용자가 Unity Asset Store에서 무료 다운로드한 패키지. 패키지에 적용되는 Standard Unity Asset Store EULA를 따른다.

### 임포트와 적용

- 원본 공통 파일과 내장 `_URP Content - Fantasy Worlds - Old Forest DEMO.unitypackage`의 메시·프리팹·재질·셰이더를 사용한다.
- 공급사 경로와 GUID를 보존한다: `Assets/TriForge Assets/`.
- 공급사의 데모 씬, 데모 Terrain/라이트맵, 중복 BiRP 패키지는 임포트하지 않는다. 우리 캠페인 전장에 에셋을 배치한다.
- `Assets/Art/Environment/Forest/`에 게임용 크기로 맞춘 장식 프리팹 6개와 지면 재질, `ForestEnvironment.asset`을 둔다.
- `ArtStyleLibrary.forestEnvironment`가 프로필을 참조한다. 기본 적용 범위는 **Stage 1만**이다. 다른 스테이지는 기존 환경을 유지한다.
- 원본 나무의 LOD와 바람 효과를 사용한다. 게임용 프리팹에서 장식 콜라이더를 제거하고, 기존 지형·Path·소환·본진 충돌 구조를 유지한다.
- 나무는 전장 양옆에 배치하며, 바위·식물·잔디는 Path 및 소환 구역과 거리를 둔다. 지면은 에셋의 풀/흙 텍스처를 기존 길 마스크와 혼합한다.
- 작은 식물과 잔디 오브젝트는 거리와 현재 그래픽 품질에 맞춰 비활성화한다.
- 숲 적용 스테이지는 기존 절차적 잔디를 생성하지 않고, 에셋 잔디를 최대 140개 군집(군집당 24~36회 배치 시도)으로 배치한다. 가장자리는 낮게 하고 군집 사이에 틈을 두며, 각 잔디의 위치에서 길·소환 구역·나무 줄기·급경사를 제외한다. 공유 메시·재질을 사용하며 거리 컬링은 군집 단위로 처리한다.
- 프리팹의 접지점은 바람 효과용 경계 상자가 아닌 실제 메시 정점으로 계산한다. 나무는 지형 삼각형 높이와 뿌리 주변의 낮은 지면을 기준으로 배치한다.
- 바닥은 풀·흙의 색상 및 노멀맵을 2m 간격으로 반복하며, Trilinear/Aniso 8로 필터링한다. 기존 절차적 붓자국과 가짜 굴곡은 줄이고 원본 표면 디테일을 사용한다. 텍스처 해상도와 전체 렌더 해상도 정책은 유지한다.

### Unity에서 보기

1. 패키지 임포트와 스크립트 컴파일이 끝날 때까지 기다린다.
2. `Lightbringer > Prepare and Play Campaign`에서 **Stage 1**을 선택하고 시작한다.
3. `Lightbringer > Validate Campaign in Play Mode`로 실제 아트가 포함된 캠페인 검사를 실행한다. 검사 프로필은 메모리 전용이며 개인 세이브를 덮어쓰지 않는다.

`Lightbringer > Art > Set Up Fantasy Worlds Forest`는 참조와 누락된 게임용 에셋을 생성한다. 기존 프리팹은 메시 최하단이 로컬 Y=0에 놓이도록 자식의 높이만 보정하며, 크기·재질 등 나머지 조정은 유지한다.
공급사 패키지만 다시 임포트한 경우에도 이 메뉴로 연결할 수 있다.

`Lightbringer > Art > Improve Forest Ground Detail`은 바닥 텍스처의 필터와 재질 값을 위 기준으로 다시 적용한다.

### 개발 파일

- `ForestEnvironmentProfile.cs`: 스테이지 범위 및 에셋 참조.
- `ForestEnvironmentDressing.cs`: 결정적 배치와 작은 식물 거리 컬링.
- `ForestEnvironmentSetup.cs`: 크기 정규화·콜라이더 제거·에셋 연결용 Editor 도구.
- `BattlefieldEnvironment.cs`: 기존 전장과 프로필 연결.
- `LightbringerTerrain.shader`: 선택적으로 풀/흙 텍스처 혼합.
- `CampaignPlayValidation.cs`: 실제 ArtStyleLibrary를 연결하여 숲 렌더러·장식 충돌 및 캠페인 루프 검사.

공급사 PNG/HDR와 FBX는 Git LFS로 관리한다. 맥에서 클론 후 `git lfs pull`이 필요하다. macOS Metal 렌더링 검증은 별도로 수행해야 한다.

