# Lightbringer Art Pass 가이드 (1단계: 교체 구조 · 셰이더 · 오라)

기준 자료: `Docs/GameDesign/3D_Paladog_Project_Handoff_v0.3.docx` 9장, `Docs/ArtReference/Characters/CharacterSheet_FemaleHero_v1.png`.
게임 로직·밸런스·충돌 판정은 바꾸지 않았다. 외형만 교체된다.

## 1. 시작하기

1. Unity 창을 활성화해 스크립트/셰이더를 컴파일한다.
2. `Lightbringer > Art > Build Art Style Assets` 실행 (최초 1회).
   - `Assets/Art/ArtStyleLibrary.asset`, `Assets/Art/Materials/LB_*.mat`, `Assets/Art/LB_BattlePostProcess_v2.asset` 생성
   - `CampaignPrototype.unity`의 `CampaignSession > Art Style`에 자동 연결
   - `Prepare and Play Campaign`도 라이브러리가 없으면 최초 1회 자동 생성·연결한다.
3. `Lightbringer > Prepare and Play Campaign`으로 확인.
4. 비교용으로 그레이박스 캡슐을 보고 싶으면 `CampaignSession`의 `Art Style` 필드를 비우면 된다.

## 2. 구성

| 요소 | 위치 | 내용 |
|---|---|---|
| 스타일 라이브러리 | `Assets/Art/ArtStyleLibrary.asset` | 팔레트 색, 진영 머티리얼, 조명·안개, 오라 색, 후처리, **모델 교체 슬롯** |
| 툰 셰이더 | `Assets/Art/Shaders/LightbringerToon.shader` | 2톤 램프, 그림자 틴트, 림라이트, 금속 하이라이트, 외곽선, 발광, **오라 안 아군 발광** |
| 오라 룬 셰이더 | `LightbringerAuraRunes.shader` | 회전하는 룬 띠·내부 별·외곽 링 (절차적, 텍스처 없음) |
| 파티클 셰이더 | `LightbringerGlow.shader` | 오라 안에서 떠오르는 빛 입자 |
| 실루엣 | `Scripts/Visuals/SilhouetteFactory.cs` | 영웅, 아군 8종, 적 2종, 적 본진의 역할이 보이는 임시 형태 |
| 적용 | `Scripts/Visuals/UnitAppearance.cs` | 소환/스폰 시 캡슐 → 스타일 외형 교체 |

성능 설계:
- 실루엣은 유닛당 **메시 1개 · 머티리얼 1개**로 구워진다. 팔레트는 버텍스 컬러에 들어 있다.
- 오라 반응은 셰이더가 전역 값으로 계산하므로 병사 수와 무관하다.

## 3. 실제 모델로 교체하는 방법

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

## 4. 조정 포인트

- 색감: 라이브러리 팔레트 색. 플레이 중 변경은 **새로 소환되는 유닛부터** 반영된다.
- 외곽선 굵기·그림자 색·림: `LB_Allied`, `LB_Enemy` 머티리얼.
- 오라 밝기: 라이브러리 `Aura Color`(HDR), `LB_AuraRunes`의 `Fill Strength`/`Pulse`.
- 블룸·톤매핑·채도: `LB_BattlePostProcess_v2`.
- 안개·환경광: 라이브러리 `Battlefield lighting`.

## 5. 다음 단계 후보

- 전장 환경 그레이박스(고저차, 폐허·성채 블록, 원경)
- 여성 영웅 3D 시험 생성 (Higgsfield, 비용 확인 후) → `Hero` 교체 슬롯에 연결
- 애니메이션 연결, 투사체·마법 VFX (Presentation 단계)
