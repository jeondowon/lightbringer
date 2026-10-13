# 8스테이지 — 본진 붕괴와 군주전

2026-10-12 사용자 요청에 따른 변경. 기존의 ‘마지막 공세에 보스 등장 → 보스 처치로 본진 방어막 해제 → 본진 파괴’ 순서를 교체한다.

군주는 봉인되어 있지 않다. 성채 함락에 직접 개입하며, 아군을 휩쓰는 에너지파는 군주의 공격이다. 성채 파괴가 봉인을 풀거나 군주를 해방하는 설정은 사용하지 않는다.

## 진행

1. 모든 정규 공세를 소환하고, 대기 중인 적과 생존한 정규 적을 모두 처리한다. 그때까지 적 본진은 보호된다.
2. 보호가 해제된 적 본진을 파괴한다. 이 시점에는 승리나 스테이지 보상을 지급하지 않는다.
3. 성채가 터지며 돌 파편과 화염에 비친 연기 기둥이 솟고, 군주의 **보라·남색 충격파**가 영웅의 충격파 스킬처럼 둥근 고리로 퍼져 4.2초 동안 전장 끝까지 나아간다(빠르게 출발해 점차 감속, 카메라 진동). 밝은 보랏빛 선두 고리, 남색 잔광, 낮게 솟는 어둠의 빛 장막, 보랏빛 불꽃으로 구성하며 지형을 따라가고 전장 밖 언덕에서 흩어진다. 기존 먼지·잿빛 파도는 고리와 함께 유지하되 전장 안쪽 구간에만 뿌린다. 충격파가 닿는 소환 병력은 전멸하며, 쓰러지는 병사는 그 순간의 자세 그대로 뒤로 날아가 떨어진 뒤 빛으로 사라진다. 카메라는 폭발을 보여준 뒤 충격파 바로 앞에서 성채 쪽을 바라보며 아군 본진 방향으로 따라간다. 영웅과 아군 본진은 살아남는다. (2026-10-13 요청. `BossShockwaveVfx.cs`, `Resources/BossShockwave.shader`)
4. 충격파가 끝나기 전부터 폐허 앞에 짙은 연기 장막이 모이고, 카메라가 폐허로 전환되면 군주는 이미 연기 속에 서 있다. 1초 뒤 돌풍이 연기를 바깥으로 걷어내며 군주가 드러난다(지면에서 솟아오르는 연출은 2026-10-13 요청으로 삭제). 등장 구간은 3초다. (`BossArrivalVfx.cs`의 Reveal, `Resources/BossArrival.shader`) 이후 프롤로그 화풍의 전체 화면 일러스트 3장으로 한국어 대화 네 줄을 표시한다. 1~2번째 대사는 대면 전경, 3번째는 군주 클로즈업, 4번째는 영웅의 결의다. 장면은 0.35초 교차 페이드로 전환하며 전장 카메라는 움직이지 않는다.
5. Enter / Space 또는 다음 버튼으로 대화를 진행한다. 마지막 대사 이후 이동·공격·소환이 복구된다. 남은 Food와 MP를 그대로 사용한다.
6. 군주의 HP가 0이 되면 즉시 승리하지 않고, 부상한 군주 → 균열로 퇴각 → 군주가 사라진 전장의 일러스트 3장과 대사 세 줄을 표시한다. Enter / Space 또는 버튼으로 읽은 뒤 1.2초 페이드가 끝나면 승리와 클리어 보상을 확정한다. 적 격퇴 EXP/골드는 기존대로 한 번 지급하지만, 이에 따른 레벨업 창은 퇴각이 끝날 때까지 보류한다. 증원이 남아 있어도 승리한다. 전투 중 영웅이나 아군 본진이 파괴되면 패배한다.

8스테이지는 첫 군주전이다. 등장 대사는 마왕의 존재를 예고한다. 2026-10-13 요청에 따라 대화와 퇴각은 실시간 모델 연기 대신 프롤로그 방식의 정지 일러스트로 전달한다. 군주 모델의 제거와 스테이지 승리 판정은 분리했다. 재등장과 최종 군주·마왕 동시전 구상은 [CampaignStoryProgression.md](CampaignStoryProgression.md)에 정리했다. 현재 플레이 범위는 8스테이지다.

등장/대화 중 전투 시뮬레이션과 자원 회복은 정지한다. 해당 구간에서 영웅·본진·군주에게 피해를 줄 수 없다. 본진 파괴로 발생한 레벨업 선택은 대화 종료 직후 처리한다. 기존 영구 성장과 저장 구조는 유지한다.

## 공격

기존 근접 공격(40 피해, 1.8초 간격)에 다음 여섯 패턴을 순환하여 추가한다. 표의 피해는 방어 적용 전이다. 체력 절반 이하에서는 특수 공격 사이 대기만 4초에서 2.6초로 줄고, 회피 예고 시간은 유지된다.

| 패턴 | 예고 / 피해 | 회피 방법 |
|---|---|---|
| 암흑 초승달 | 1.25초 / 34 | 고정 방향으로 날아가는 검기의 옆으로 이동. 속도 14m/s, 최대 35m, 같은 대상은 한 번만 타격 |
| 어둠의 휩쓸기 | 1.25초 / 45 | 반경 6m, 전방 110도 부채꼴 밖이나 군주 뒤로 이동 |
| 암흑 낙뢰 | 1.6초 / 36 | 시전 시작 시 고정된 영웅 위치의 반경 3.2m에서 이탈 |
| 파멸의 돌진 | 1.25초 / 42 | 고정된 직선 진로를 피함. 최대 18m 이동, 같은 대상은 한 번만 타격 |
| 어둠의 충격파 | 1.5초 / 32 | 군주를 중심으로 반경 8m 밖으로 이동 |
| 심연의 부름 | 1.25초 / 직접 피해 없음 | 습격자·궁수·군집병을 최대 3기 소환. 생존 증원 수 제한 적용 |

현재 수치는 첫 구현값이며 실제 플레이로 조정한다. 대사는 변경 가능한 첫 초안이며 별도 음성은 포함하지 않는다. 시각 효과는 외부 에셋 없이 URP 셰이더와 절차적 메시로 구성한다.

## 구현 파일

- 대화 삽화: `Assets/UI/Resources/BossStory/Confrontation_v1.png`, `Retreat_v1.png`. 내장 imagegen 도구로 제작했으며 전체 프롬프트는 `Docs/ArtReference/BossStoryPrompts.md`에 기록했다.
- 2026-10-13 삽화/퇴각 검증: 시스템 380개, Play 182개 PASS. `Docs/Validation/BossStoryChecks.txt`, `BossDialogue.png`, `BossRetreat.png` 참고.
- 전환/목표: `BossEncounter.cs`, `StageObjective.cs`, `EnemyWaveSpawner.cs`, `PrototypeBattleBuilder.cs`
- 공격: `BossCombat.cs`, `DarkCrescent.cs`, `ModelAnimationDriver.cs`
- 연출: `BossVfx.cs`, `BossArrivalVfx.cs`, `BossShockwaveVfx.cs`, `Assets/Resources/BossRift.shader`, `BossArrival.shader`, `BossShockwave.shader`
- 대화/HUD: `BossDialogueView.cs`, `CampaignHUD.cs`, `BattleHudView.cs`, `LightbringerHUD.uss`
- 일시정지/입력: `CampaignSession.cs`, `BattleSimulation.cs`, `HeroAbilities.cs`
- 검증: `EnemyRosterValidation.cs`, `HudValidation.cs`, `CampaignPlayValidation.cs`

Unity에서는 `Lightbringer > Prepare and Play Campaign`으로 캠페인을 실행하고 해금된 8스테이지에 진입한다. 새 컴포넌트와 연출은 전장 생성 시 자동 설정되므로 Inspector 작업이 필요 없다.

검증: 시스템 359개 / 실제 Play 106개 PASS. 상세 범위는 `Docs/Validation/BossEncounterChecks.txt`에 기록했다. 등장 장면은 `BossRupture.png`, 대화 화면은 `BossDialogue.png`, 검기는 `BossCrescent.png`로 확인할 수 있다.
