# Lightbringer 플레이테스트 가이드 (Greybox → Art Pass 전)

목적: 기획서 11장의 질문 — **"3D 공간에서 병력을 한 명씩 투입하고, 영웅이 직접 전선에 합류하는 것이 재미있는가"** — 에 답하고,
Art Pass 전에 고쳐야 할 밸런스·조작 문제를 찾는다. 수치 조정은 이 결과를 보고 결정한다.

## 1. 준비

1. Unity에서 `Lightbringer > Validate Greybox Systems` 실행 → 모두 PASS 확인 (Playtest 검사 포함).
2. 처음부터 테스트하려면 기존 저장을 백업/삭제한다:
   `Application.persistentDataPath/Lightbringer/campaign-v1.json` (Windows: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\ProjectLightbringer\Lightbringer\`,
   macOS: `~/Library/Application Support/DefaultCompany/ProjectLightbringer/Lightbringer/`).
   Playtest 기록(`playtest/` 폴더)은 캠페인 저장과 별개이므로 지우지 않아도 된다.
3. `Lightbringer > Prepare and Play Campaign`으로 시작.

## 2. 자동으로 기록되는 것

전투가 끝나면 결과 화면에 요약이 나오고, **RETURN TO PREPARATION**을 누를 때 한 줄이 기록된다.
전투 중 Play를 중지하면 `abandoned`로 기록된다.

- 기록 파일: `persistentDataPath/Lightbringer/playtest/playtest-log.jsonl`
- 폴더 열기: `Lightbringer > Playtest > Open Playtest Log Folder`
- 요약 생성: `Lightbringer > Playtest > Summarize Playtest Log` → `Docs/Playtest/PlaytestSummary.md`

| 항목 | 무엇을 알려주나 |
|---|---|
| 전투 시간, 승/패/중단 | 스테이지 길이와 난이도 곡선 |
| Food 생산/사용/**상한에서 버려진 양**, 상한 도달 시간 | Food가 남아도는지(소환이 지루함) 부족한지(답답함) |
| 병종별·Path별 소환 수, 아군 손실 | 어떤 병종이 쓸모없거나 과한지, 전선 배분이 일어나는지 |
| 영웅이 각 Path 근처에 있던 시간 | 영웅이 실제로 전선을 오가며 지원하는지 |
| 오라 안 평균 아군 수 | 오라가 체감되는 규모인지 |
| 영웅 최저 HP, 패배 시 적 본진 잔여 HP | 위험도와 "아깝게 졌다"는 감각 |
| 재미 점수(1–5)와 메모 | 주관적 평가 — 가장 중요 |

## 3. 플레이 순서와 체크 포인트

각 스테이지는 **최소 1회**, 가능하면 스테이지 1·3·6은 2회 플레이한다.
결과 화면에서 재미 점수와 한 줄 메모를 꼭 남긴다.

**스테이지 1–2 (1 Path) — 기본 루프**
- 이동·카메라가 편한가? 전황을 보기에 카메라 거리/높이가 적절한가?
- Food가 차는 속도와 소환 비용이 "고민하며 투입"하는 느낌인가, 그냥 F 연타인가?
- 병사가 알아서 진격·교전하는 모습이 읽히는가?

**스테이지 3–5 (2 Paths) — 전선 배분**
- Tab으로 Path를 바꿔 소환하는 판단이 실제로 생기는가?
- 한 전선이 밀릴 때 영웅이 달려가 막는 순간이 재미있는가, 이동이 너무 먼가?
- 오라 안에 아군이 충분히 들어오는가? 오라 효과가 체감되는가?

**스테이지 6–8 (3 Paths) — 과부하 여부**
- 3개 전선이 관리 가능한가, RTS처럼 부담스러운가? (기획서: 많아도 3개)
- 상위 병종(기사·드래곤)을 모아 쓰는 선택이 저비용 다수보다 매력 있는가?
- 드래곤 소환이 "캠페인 정점"처럼 느껴지는가 (연출 없이 수치만으로도)?

**전체**
- 레벨업 3택1: 선택지가 의미 있는가, 항상 같은 것을 고르게 되는가?
- 장비 3슬롯: 준비 화면에서 조합을 바꿀 이유가 있는가?
- 패배 후 재도전 의욕이 생기는가?

## 4. 결과 전달

플레이가 끝나면 `Summarize Playtest Log`를 실행하고 `Docs/Playtest/PlaytestSummary.md`와
추가로 느낀 점(가장 재미있던 순간 / 가장 답답했던 순간 3개씩)을 전달하면,
그 결과로 밸런스 조정안을 만든 뒤 Art Pass(여성 영웅 → 병종 실루엣 → 적 → 환경)로 넘어간다.

요약의 "Automatic hints"는 기준값에 따른 참고용이며, 결정은 플레이어 메모를 우선한다.
