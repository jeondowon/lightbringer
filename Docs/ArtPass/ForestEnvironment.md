# Stage 1 — Fantasy Worlds Forest

사용 에셋: **Fantasy Worlds: Forest FREE - Stylized Forest Environment Open World**, TriForge Assets, v1.0.
공식 페이지: https://assetstore.unity.com/packages/slug/282610
취득 경로: 사용자가 Unity Asset Store에서 무료 다운로드한 패키지. 패키지에 적용되는 Standard Unity Asset Store EULA를 따른다.

## 임포트와 적용

- 원본 공통 파일과 내장 `_URP Content - Fantasy Worlds - Old Forest DEMO.unitypackage`의 메시·프리팹·재질·셰이더를 사용한다.
- 공급사 경로와 GUID를 보존한다: `Assets/TriForge Assets/`.
- 공급사의 데모 씬, 데모 Terrain/라이트맵, 중복 BiRP 패키지는 임포트하지 않는다. 우리 캠페인 전장에 에셋을 배치한다.
- `Assets/Art/Environment/Forest/`에 게임용 크기로 맞춘 장식 프리팹 6개와 지면 재질, `ForestEnvironment.asset`을 둔다.
- `ArtStyleLibrary.forestEnvironment`가 프로필을 참조한다. 기본 적용 범위는 **Stage 1만**이다. 다른 스테이지는 기존 환경을 유지한다.
- 원본 나무의 LOD와 바람 효과를 사용한다. 게임용 프리팹에서 장식 콜라이더를 제거하고, 기존 지형·Path·소환·본진 충돌 구조를 유지한다.
- 나무는 전장 양옆에 배치하며, 바위·식물·잔디는 Path 및 소환 구역과 거리를 둔다. 지면은 에셋의 풀/흙 텍스처를 기존 길 마스크와 혼합한다.
- 작은 식물과 잔디 오브젝트는 거리와 현재 그래픽 품질에 맞춰 비활성화한다.
- 숲 적용 스테이지는 기존 절차적 잔디를 생성하지 않고, 낮은 에셋 잔디 무리만 배치한다.
- 프리팹의 접지점은 바람 효과용 경계 상자가 아닌 실제 메시 정점으로 계산한다. 나무는 지형 삼각형 높이와 뿌리 주변의 낮은 지면을 기준으로 배치한다.

## Unity에서 보기

1. 패키지 임포트와 스크립트 컴파일이 끝날 때까지 기다린다.
2. `Lightbringer > Prepare and Play Campaign`에서 **Stage 1**을 선택하고 시작한다.
3. `Lightbringer > Validate Campaign in Play Mode`로 실제 아트가 포함된 캠페인 검사를 실행한다. 검사 프로필은 메모리 전용이며 개인 세이브를 덮어쓰지 않는다.

`Lightbringer > Art > Set Up Fantasy Worlds Forest`는 참조와 누락된 게임용 에셋을 생성한다. 기존 프리팹은 메시 최하단이 로컬 Y=0에 놓이도록 자식의 높이만 보정하며, 크기·재질 등 나머지 조정은 유지한다.
공급사 패키지만 다시 임포트한 경우에도 이 메뉴로 연결할 수 있다.

## 개발 파일

- `ForestEnvironmentProfile.cs`: 스테이지 범위 및 에셋 참조.
- `ForestEnvironmentDressing.cs`: 결정적 배치와 작은 식물 거리 컬링.
- `ForestEnvironmentSetup.cs`: 크기 정규화·콜라이더 제거·에셋 연결용 Editor 도구.
- `BattlefieldEnvironment.cs`: 기존 전장과 프로필 연결.
- `LightbringerTerrain.shader`: 선택적으로 풀/흙 텍스처 혼합.
- `CampaignPlayValidation.cs`: 실제 ArtStyleLibrary를 연결하여 숲 렌더러·장식 충돌 및 캠페인 루프 검사.

공급사 PNG/HDR와 FBX는 Git LFS로 관리한다. 맥에서 클론 후 `git lfs pull`이 필요하다. macOS Metal 렌더링 검증은 별도로 수행해야 한다.
