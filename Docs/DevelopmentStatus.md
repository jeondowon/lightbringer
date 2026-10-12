# Lightbringer 개발 현황 / 환경 이전

기준: 2026-10-12, Unity 6000.3.20f1 + URP. 구현 여부와 최종 품질 검증은 구분한다.

## 현재 구현

- 핵심 루프: 3인칭 영웅 이동/공격, Food 생성, 개별 소환, 신규 병력의 Path 배정, 자동 진격/전투, 오라, 본진 승패.
- 캠페인: 프로토타입 8개 스테이지, 1~3개 Path, 병종 해금, Mana/장비, EXP 및 레벨업 3택1 영구 성장, 저장/불러오기.
- 아군 8종: 검사, 궁수, 방패병, 창병, 사제, 마법사, 기사, 드래곤. 여성 영웅 및 전 병종 모델/리깅 적용.
- 적 6역할: Raider, Archer, Swarmling, Brute, Shaman, Warlord. 마지막 스테이지 보스/본진 방어막.
- 화면: UI Toolkit 캠페인 지도/출전 준비/전투/레벨업/결과/일시정지 화면, 18장 프롤로그, 미니맵, 체력바, 자원/병종/장비 표시.
- 음악: 전투 및 프롤로그 BGM, 음량 조절, 일시정지·종료 페이드, 게임 내 크레딧. 사용하지 않는 음악 데모와 전용 제작 도구는 제거했다.
- 연출: 공격 타이밍, 화살/마법탄, 피격 효과, 드래곤 화염, 회복/오라 축복/본진 효과, 전장 환경 및 셰이더.
- 성능: 군중용 경량 메시 및 LOD, 애니메이션 상태 전환·미니맵·체력바 갱신 비용 절감, 품질 3단계, 자동 품질 하향/해상도 조정, 벤치마크 도구.

## 검증과 한계

- 2026-10-12 정리 후 검증: 시스템 검사 328개, 실제 Play 검사 95개, 프롤로그 검사 53개 PASS. 삭제 전에 Unity 의존성 검사와 코드 경로 확인을 수행했다. 삭제 목록과 검증 범위는 `Docs/Validation/CleanupChecks.txt`에 기록했다.
- 2026-10-11 재검증: Unity 스크립트 컴파일 후 컴포넌트/물리 검사 315개, 실제 Play 모드 검사 28개 PASS. 소환 8종, 영구 성장, 승패 전환, 드래곤 접지, 100/300기 전투 및 준비 화면 복귀를 확인했다.
- 이번 Play 검사에서 300기 렌더링 프레임 평균 36.805 ms, p95 50.220 ms였다. 해당 Windows Editor 실행 조건의 결과이며, 목표 기기에서 60 FPS가 확보되었다는 의미는 아니다. 프레임 제한/Editor 대기를 포함한 원인 분리와 빌드 측정이 필요하다.
- `Docs/Validation/GreyboxChecks.txt`: 로컬에서 재생성되는 컴포넌트/물리 검사 결과. Git에는 포함되지 않는다.
- `Docs/Validation/CampaignPlayChecks.txt`: 실제 Play 모드 검사 결과. 첫 줄의 PASS/FAIL 및 시간을 확인한다.
- 과거 Play 검사에는 지상형으로 변경한 드래곤의 비행 높이를 요구하는 오래된 조건이 있었다. 현재 조건은 실제 접지와 Path 배정을 확인한다.
- `Docs/Validation/CombatScaleChecks.txt`: 100/300기 시뮬레이션 측정. 렌더링·모델·애니메이션 비용을 포함하지 않으므로 FPS 근거로 사용하지 않는다.
- 렌더링 벤치마크는 `Lightbringer > Diagnostics > Benchmark Performance`. 출력 `Docs/Diagnostics/Performance.txt`는 기기별 로컬 파일로 Git에서 제외된다. Editor 측정은 출시 빌드 성능을 보장하지 않는다.
- 맥에서의 컴파일/Metal 렌더링/실제 전투 성능은 별도 검증해야 한다.
- 자동 검증은 재미, 밸런스, 최종 아트 완성도를 보장하지 않는다.

## 다음 작업 순서

1. 맥에 동일 Unity 버전과 Git LFS를 설치하고 클론한 뒤 캠페인 실행 및 자동 검증. 대표 전투를 품질 단계별로 측정한다.
2. `Docs/Playtest/PlaytestGuide.md`에 따라 Stage 1~8 실제 플레이. Food 속도/비용, 병종 역할, 기사 돌격, 지상 드래곤 생존력, 보스 난이도, 3개 전선 가독성을 우선 조정한다.
3. 타격/소환/오라/승패 SFX를 추가한다. 전투·프롤로그 BGM은 구현되어 있다.
4. 레인 수 차이를 넘어 Path 형태/지형/환경 테마가 다른 스테이지를 만든다.
5. 적 3D 모델, 장비 등급 보상/강화 여부, 병종 해금 및 드래곤 등장 연출을 정리한다. 남성 영웅은 시스템 안정화 이후다.

## 맥 인수인계

1. 이전 PC에서 원하는 변경을 커밋하고 push한다.
2. Unity Hub 및 Unity 6000.3.20f1을 설치한다. Apple Silicon 맥은 해당 아키텍처 에디터를 사용한다.
3. Git LFS 설치 후 `git lfs install`, 저장소 클론, 클론 폴더에서 `git lfs pull`을 실행한다.
4. Unity Hub에 프로젝트 폴더를 추가하고 패키지 다운로드와 임포트 완료를 기다린다. `Library/`, `Temp/`, `Logs/`, `obj/`는 이전하지 않는다.
5. `Lightbringer > Validate Greybox Systems`, `Lightbringer > Validate Campaign in Play Mode`로 확인한다.
6. `Lightbringer > Prepare and Play Campaign` 또는 `Assets/Scenes/CampaignPrototype.unity`에서 직접 플레이한다. F9는 품질 변경, F10은 성능 표시다.

`AGENTS.md`는 Git에 포함되어 함께 내려받는다. 로컬 도구 설정과 인증 정보는 저장소에 추가하지 않는다.
기존 캠페인 진행을 유지하려면 `Application.persistentDataPath/Lightbringer/`의 저장 파일도 별도로 이전해야 한다. 클론만으로는 개인 세이브와 플레이테스트 기록이 이동하지 않는다.

기획서 v0.3 이후 결정으로 로드맵에 기록된 예외는 골드 제거와 드래곤 지상 유닛화다. 이 문서는 추가 설계 변경을 도입하지 않는다.
