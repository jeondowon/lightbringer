# 플레이테스트 가이드

목적: **"3D 공간에서 병력을 한 명씩 투입하고, 영웅이 직접 전선에 합류하는 것이 재미있는가"** 를 확인하고 밸런스·조작 문제를 찾는다.

## 준비
1. `Lightbringer > Validate Greybox Systems`가 모두 PASS인지 확인한다.
2. 처음부터 하려면 저장을 백업/삭제한다: `Application.persistentDataPath/Lightbringer/campaign-v1.json`
   (Windows `%USERPROFILE%\AppData\LocalLow\DefaultCompany\ProjectLightbringer\Lightbringer\`, macOS `~/Library/Application Support/DefaultCompany/ProjectLightbringer/Lightbringer/`).
3. `Lightbringer > Prepare and Play Campaign`으로 시작한다.

## 자동 기록
전투가 끝나고 **준비로 돌아가기**를 누르면 한 줄이 기록된다(Play 중단은 `abandoned`).

- 기록: `persistentDataPath/Lightbringer/playtest/playtest-log.jsonl` (`Lightbringer > Playtest > Open Playtest Log Folder`)
- 요약: `Lightbringer > Playtest > Summarize Playtest Log` → `Docs/Playtest/PlaytestSummary.md` (로컬 생성물, Git 제외)
- 항목: 전투 시간·승패, Food 생산/사용/상한 초과 폐기량, 병종·Path별 소환과 손실, Path별 영웅 체류 시간, 오라 내 평균 아군 수, 영웅 최저 HP, 재미 점수(1–5)와 메모.

## 체크 포인트
- **1 Path 스테이지(1–2)**: 카메라와 이동이 편한가, Food 속도·비용이 "고민하며 투입"하는 느낌인가, 자동 진격·교전이 읽히는가.
- **2 Path(3–5)**: Tab으로 Path를 바꿔 소환하는 판단이 생기는가, 영웅이 밀리는 전선으로 달려가는 순간이 재미있는가, 오라가 체감되는가.
- **3 Path(6–8 이후)**: 3개 전선이 RTS처럼 부담스럽지 않은가, 상위 병종(기사·드래곤)을 모아 쓰는 선택이 매력적인가.
- **공통**: 레벨업 3택1과 기술 슬롯 조합의 의미, 패배 후 재도전 의욕.

결과 화면에서 재미 점수와 한 줄 메모를 남기고, 요약의 "Automatic hints"는 참고용으로만 쓴다(플레이어 메모 우선).
