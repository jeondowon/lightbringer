# 본진 장식과 폐허 에셋

사용자가 다운로드한 Tripo FBX 9종을 `Assets/Art/Environment/Props/`에 가져왔다. 다운로드 원본은 보존한다. 모델의 축·크기·바닥 중심점을 맞춘 프리팹과 URP Lit 재질을 사용하며, `ArtStyleLibrary.battlefieldProps`가 배포용 에셋 참조를 가진다.

| 프리팹 | 원본 ZIP | 배치 용도 |
|---|---|---|
| AlliedBeacon | 아군 등대.zip | 아군 성채 양옆의 등대 |
| AlliedMonument | 아군ornate+monument+3d+model.zip | 아군 본진 옆 기념비 |
| AlliedBarrel | 아군barrel.zip | 아군 보급품 군집 |
| AlliedChest | 아군treasure+chest+3d+model.zip | 보급품 옆 보물상자 |
| EnemyCrystal | 적군crystal+rock+formation+3d+model.zip | 적군 본진 양옆의 수정 바위 |
| EnemyBoneFence | 적군spikedbonefence.zip | 적군 진지 측면의 짧은 울타리 |
| EnemyAltar | 적군stone+skull+altar+3d+model.zip | 적군 본진 측면 제단 |
| RuinColumn | ancient+column+3d+model.zip | 전장 양옆 유적의 기둥 |
| RuinArch | ancient+arch+3d+model.zip | 전장 양옆 유적의 아치 |

## 배치 기준

- 캠페인의 아군 본진, 적군 본진, 서쪽 유적, 동쪽 유적 네 군집으로 구성한다.
- 프리팹의 전체 가로·세로 크기로 바닥 점유 반경을 계산한다. 진격로 중심선으로부터 오브젝트 가장자리까지 4.5m 이상 확보한다.
- 본진 건물, 출입구, 다른 장식의 점유 영역을 피한다. 배치가 불가능하면 해당 위치의 소품은 생략한다.
- 나무·수풀·잔디 배치는 먼저 확보한 장식 구역을 피한다. 큰 나무는 추가 여유를 둔다.
- 지형 삼각형의 실제 높이에 맞춰 접지하고, 받침대가 뜰 정도의 경사에는 놓지 않는다.
- 보물상자·제단 등은 환경 장식이다. 기존 Path, 소환, 전투 목표와 콜라이더는 그대로 사용한다.
- 기존 절차적 폐허 생성 코드 및 원경의 단순 성채·첨탑 장식을 제거했다. 지형, 길, 산과 기존 숲 시스템은 유지한다.
- 기존 산림 프로필의 스테이지 범위는 유지한다. 새 본진·유적 장식은 모든 캠페인 스테이지에 적용한다.

## 재질·성능

원본 Base Color와 Normal, Metallic 및 Roughness를 사용한다. URP용으로 Metallic은 R, `1 - Roughness`는 A에 패킹한다. 원본의 중복 RM 텍스처는 가져오지 않는다. GPU 인스턴싱을 켠 공유 재질과 공유 메시를 사용하며, 런타임 메시 복제 및 장식용 Update는 없다. FBX의 Read/Write는 끈다.

원본의 고밀도 메시는 기존 `CrowdMeshOptimizer`와 Unity Mesh LOD 기능으로 게임용 메시를 생성한다. UV·노멀·접선을 보존하며 거리별 Mesh LOD도 포함한다. 원본 FBX는 수정하지 않는다. 원본 모델과 `*_Optimized.asset`은 Git LFS로 관리한다. 외부 플러그인이나 OS 전용 런타임 API는 추가하지 않는다.

## Unity에서 확인

이미 설정된 프로젝트에서는 Play를 다시 시작하면 적용된다.

- `Lightbringer > Art > Set Up Battlefield Props`: 가져온 원본에서 프리팹·재질·프로필을 재생성한다.
- `Lightbringer > Art > Validate and Capture Battlefield Props`: 개인 저장 데이터를 사용하지 않고 1·4·8 스테이지의 배치를 검사하고 스크린샷을 만든다.
- 프리팹은 `Assets/Art/Environment/Props/`의 각 에셋 폴더에서 확인할 수 있다.
- 배치 코드: `BattlefieldPropDressing.cs`. 크기를 수정하면 프로필의 `size`도 실제 프리팹 크기에 맞춰 갱신해야 한다.

검증 결과는 `Docs/Validation/BattlefieldPropImportChecks.txt`와 `BattlefieldPropPlacementChecks.txt`, 스크린샷은 `Docs/ArtPass/Preview/Prop_*.png`와 `Props_*.png`에 저장된다.

## 적용 파일과 배치 검증

- 추가: `BattlefieldPropProfile.cs`, `BattlefieldPropDressing.cs`, `BattlefieldPropSetup.cs`, 9종 에셋 폴더와 `BattlefieldProps.asset`.
- 수정: `BattlefieldEnvironment.cs`, `ForestEnvironmentDressing.cs`, `ArtStyleLibrary.cs` 및 라이브러리 에셋, `CrowdMeshOptimizer.cs`, `.gitattributes`.
- 1·4 스테이지는 각각 21개, 8 스테이지는 20개를 배치하며 모두 9종을 포함한다. 지면 접지, 진격로 여유, 장식 간 겹침 및 불필요한 콜라이더가 없는지 검사했다.
- 전선 수에 따라 유적의 좌우 위치를 조정한다. 초반에는 중심에서 14m, 중반에는 23m, 후반에는 32m 떨어진 곳에 배치한다.
- 개별 에셋과 본진·유적·전체 전장의 캡처를 확인했다. 메시 통계는 추가 LOD의 저장 인덱스를 포함하므로 프레임당 실제 렌더링 삼각형 수와는 다르다.
- 2026-10-12: 기본 검증 315개 항목 및 실제 Play 모드 캠페인 검증을 통과했다. 소환·이동·전투·승패·3개 전선·100/300 유닛 전투를 확인했다. `CampaignPlayValidation.cs`의 드래곤 이름 검사를 한글화된 `UnitCatalog` 이름과 일치하도록 수정했다.
