# Lightbringer 문서 안내

기준일: 2026-10-14 · Unity 6000.3 + URP · 설계의 최우선 기준은 `GameDesign/3D_Paladog_Project_Handoff_v0.3.docx`.

## 문서 구성

| 경로 | 내용 |
|---|---|
| `GameDesign/Campaign.md` | 24스테이지(3막) 구조, 성장·레벨 곡선, 이야기 진행, 첫 군주전 |
| `GameDesign/Skills.md` | 영웅 기술 5종, 슬롯, 습득·강화, 구현 요약과 남은 일 |
| `GameDesign/FinalBattle.md` | 3막 최종전(마왕) 기획안 — 미구현 |
| `Art/ArtDirection.md` | 그림책(Storybook) UI 아트 방향 |
| `Art/Pipelines.md` | 모델 임포트·리깅·무기·소품·숲 환경 파이프라인과 Editor 메뉴 |
| `ArtReference/` | 캐릭터·적·환경·프롤로그 레퍼런스 이미지, `Prompts.md`(생성 프롬프트 기록) |
| `ThirdParty/` | 음악 크레딧과 라이선스 원문 (Asset Store/기타 에셋 라이선스는 아래 참고) |
| `Playtest/PlaytestGuide.md` | 플레이테스트 방법과 기록 도구 |

## 현재 구현

- 핵심 루프: 3인칭 영웅 이동·공격, Food 생성, 병종 개별 소환, Path 자동 진격·전투, 오라, 본진 승패.
- 캠페인: 16스테이지(1막 1~8, 2막 9~16), Path 1~3개, 병종 해금, EXP·레벨업 3택1 성장, 빛의 조각(단일 재화), 저장 버전 4.
- 아군 8종(검사·궁수·방패병·창병·사제·마법사·기사·드래곤), 적 6역할, 영웅 기술 5종(빛 광선·치유·충격파·얼음·메테오).
- 보스: 8·16 군주전(등장·대화·퇴각 연출), 3막은 준비 중.
- 화면: UI Toolkit 지도·준비·전투 HUD·레벨업·결과·일시정지, 18장 프롤로그, 미니맵.
- 성능: 모델 Mesh LOD, 애니메이션·미니맵·체력바 갱신 절감, 품질 3단계와 자동 품질 조절.
- 음악·효과음: 전투/프롤로그 BGM, 효과음 풀(월드 20·UI 4), 음량 설정.

## 검증 도구 (Editor 메뉴)

| 메뉴 | 용도 |
|---|---|
| `Lightbringer > Validate Greybox Systems` | 컴포넌트·물리·진행 시스템 검사 (결과는 로컬 `Docs/Validation/`, Git 제외) |
| `Lightbringer > Validate Campaign in Play Mode` (+ Act Two / Act Three) | 실제 Play 모드 캠페인 검사 |
| `Lightbringer > Diagnostics > Benchmark Performance` | 렌더링 벤치마크 (결과는 로컬 `Docs/Diagnostics/`, Git 제외) |
| `Lightbringer > Prepare and Play Campaign` | 캠페인 씬 준비 후 재생 |

자동 검증은 재미·밸런스·최종 아트 품질을 보장하지 않는다. Editor 측정은 출시 빌드 성능을 보장하지 않는다.

## 다음 작업

1. 맥에 같은 Unity 버전과 Git LFS를 설치해 컴파일·Metal 렌더링·전투 성능 검증.
2. 직접 플레이하며 Food 속도/비용, 병종 역할, 기사 돌격(즉사로 넉백이 안 보이는 문제), 지상 드래곤 생존력, 보스·기술 수치 조정.
3. 3막(17~24)과 최종 동시전, 스테이지별 지형·환경 다양화(현재는 Path 수·환경 테마 위주).
4. 빙결·기절 시각 표시, 메테오 효과음, 공통 마법·병력 사망·레벨업 효과음 교체.
5. 남성 영웅은 시스템 안정화 이후.

하지 않을 것: 병사 개별 선택·드래그 선택·진형 관리·전군 집결, 병종 덱 제한, 스테이지 단위 로그라이크 초기화.

## 서드파티 에셋

- Fantasy Worlds: Forest FREE (TriForge Assets) — `Assets/TriForge Assets/`, Unity Asset Store EULA. 1막 숲 환경.
- Dry Trees (DigitalAntichrist) — `Assets/Dry_Trees/`, Asset Store EULA, URP로 변환해 사용.
- Ruined Tower Free (icescreen), Altar Ruins Free (Underhill Labz) — `Assets/Art/Environment/ActTwo/Sources/`, Asset Store EULA. 데모 씬·스크립트는 가져오지 않았다.
- 음악: `ThirdParty/MusicCredits.txt` (CC BY — 게임 내 크레딧 표시, 영상 게시 시 설명란에 표기).
- 효과음 출처: `Assets/Audio/SoundEffectsNotes.txt`.
- 폰트 라이선스: `Assets/UI/Fonts/*-OFL.txt`.

공급사 PNG/HDR/FBX/GLB는 Git LFS로 관리하므로 클론 후 `git lfs pull`이 필요하다.
