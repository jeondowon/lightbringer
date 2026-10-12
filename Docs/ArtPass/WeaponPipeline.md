# 무기 3D 제작 파이프라인 (적 무기 · 일시정지 · 진단 도구)

2026-10-12 작업 기록. 적 6종 리그와 무기를 정리하면서 확정한 방식과, 아직 남은 일을 적는다.

## 1. 현재 상태

### 적 무기

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
- 아군 무기는 모두 코드 생성 메시로 교체했다 (2026-10-12). 검사: Raider 검/방패 재사용(`SwordSpecAllied`), 나머지는 `WeaponMeshes.Allied.cs`의 Spear, TowerShield, KiteShield, Mace, Lance, Arrow, Quiver, BowFittings, MageStaff, PriestStaff. 확인용: `Docs/Diagnostics/alliedweapons.request` → `AlliedWeaponSnapshot.png`. 아군 재질은 강철·금·법랑·가죽·목재 텍스처를 사용하며, 참조가 끊긴 이전 단색/광택 재질은 정리했다.

### 기타

- 일시정지/재개: Esc 또는 P. `CampaignSession.SetPaused`, `UI/PauseView.cs`. 2026-10-12 실제 Play 자동 검사에서 정지/재개 및 BGM 음량 전환을 확인했다. 키 입력 자체의 수동 검증과는 구분한다.
- 실제 전투에서 휘두를 때 무기 방향과 위치는 Play로 확인하지 않았다. 지금까지는 에디터 안 정지 화면 렌더링으로만 확인했다.

## 2. 코드 생성 무기 (`Assets/Scripts/Editor/WeaponMeshes.cs`)

- 좌표: 손잡이 중심이 원점, 날/머리는 +Y. 베는 무기는 **날이 ±Z(휘두르는 앞쪽)**, 넓은 면이 ±X. 원형 방패는 앞면이 +Z.
- 날 계열(검, 단검, 대검)은 `SwordSpec` 수치만 바꿔 같은 생성기 `WeaponMeshes.Sword`로 만든다. 날 단면은 마름모(가운데 능선), 날 끝은 둥글게 모이고, 양쪽 날에 이 빠진 자국이 있다. 가드, 손잡이 링, 감긴 가죽, 폼멜이 같이 나온다.
- 면이 또렷하게 보이도록 평면 셰이딩이다. UV는 박스 매핑이라 타일링 텍스처가 모든 면에서 같게 보인다.
- 텍스처는 코드로 생성해 `Assets/Art/Weapons/Textures/`에 저장한다. `HammeredIron`(망치 자국 철), `WoodGrain`(낡은 판자). 파일이 이미 있으면 다시 만들지 않으므로 바꾸려면 파일을 지운다.
- 진영 차이는 재질로 낸다 (`EnemyRigSetup.Materials`의 `HammeredIron`, `DarkHammered`, `WoodPlank`, `WrapLeather` 등).

## 3. Tripo 무기를 쓰는 법 (`EnemyRigSetup.ImportedWeapon`)

1. GLB를 `Assets/Art/Characters/<Enemy>/Weapon/`에 둔다. Tripo는 높이 1 m로 정규화돼 나온다.
2. 높이별 단면으로 손잡이 위치를 잰다. 대검은 높이의 17.5%, 지팡이는 47%.
3. `length`(실제 길이)로 키우고, 손잡이 점을 원점으로 옮기고, `yaw`로 돌려 메시 에셋으로 저장한다. 재질은 GLB 것을 그대로 쓴다.
4. 날 무기는 GLB에서 넓은 면이 ±Z를 보고 있어 `yaw` 90°로 날을 앞으로 돌린다.

## 4. 반드시 알아야 할 함정

- **NaN 정점이 하나라도 있으면 Unity가 메시를 통째로 버린다.** 날 끝의 `cos(π/2)`가 -4e-8이 되어 거듭제곱에서 NaN이 나왔다. 0 이상으로 고정했다. 메시가 비어 저장되면 정점 수가 0인지부터 본다.
- **이미 쓰는 메시 에셋을 덮어쓸 때는 `EditorUtility.CopySerialized`를 쓰면 안 된다.** 렌더러가 예전 GPU 데이터를 계속 그려서 몸이 사라지거나 예전 모양이 나온다. `RigBuildUtility.SaveAsset`이 Mesh API(`CopyMesh`)로 덮어쓴다. 새 코드는 반드시 `SaveAsset`을 쓴다.
- **에셋을 지우고 같은 GUID로 다시 만들지 않는다.** 에디터 메모리의 참조가 `MISSING`이 되어 재시작 전까지 풀리지 않는다.
- 날이 사라지거나 몸이 안 보일 때 에디터를 재시작하면 디스크에서 새로 읽는다. 파일이 정상이면 재시작으로 풀린다.
- 무기 길이가 늘면 공격 동작에서 바닥을 뚫는 검증(`-0.03 m` 미만)이 실패할 수 있다. Brute 몽둥이가 이래서 0.85배다.

## 5. 진단과 작업 요청 파일

에디터 안에서 렌더링해 보려면 요청 파일을 만든다. 에디터가 놀고 있고(Play 중이 아니고 컴파일 중이 아닐 때) 창이 포커스를 받은 상태여야 처리한다.

| 요청 파일 | 하는 일 | 결과 |
|---|---|---|
| `Docs/Validation/EnemyRigs.request` | 적 리그 전체 다시 빌드 (메뉴: `Lightbringer/Art/Build Enemy Rigs`) | `Docs/Validation/EnemyRigChecks.txt` |
| `Docs/Diagnostics/enemyrigsnapshot.request` | 적 6종 정면/옆면 렌더링과 무기 확대 렌더링 (메뉴: `Lightbringer/Art/Snapshot Enemy Rigs`) | `Docs/Diagnostics/EnemyRigSnapshot.png/.txt`, `EnemyWeaponSnapshot.png` |

- 무기 확대 이미지는 무기마다 두 칸이다: +Z(앞)에서 본 것, +X(옆)에서 본 것. 날 무기는 앞 칸에서 얇은 선, 옆 칸에서 넓은 면이 보이면 맞게 돌아간 것이다.
- 스크립트를 고친 직후에는 Unity 창을 한 번 클릭해야 컴파일된다. Play 중에 스크립트를 고치면 코드가 다시 불려지면서 `EnemyWaveSpawner`가 `IndexOutOfRange`를 반복해서 낸다. Play를 끝내면 사라진다.

## 6. 일시정지 (`Esc` / `P`)

- `CampaignSession`이 `userPaused`와 `Time.timeScale = 0`으로 전투를 멈춘다. 레벨업 카드 선택 때 쓰던 `BattleSimulation` 일시정지를 재사용한다.
- 레벨업 선택 중이나 전투 종료 후에는 일시정지되지 않는다. 플레이 기록의 전투 시간은 멈춘 동안 늘지 않는다.
- 전에는 Esc가 커서만 풀었는데 이제는 일시정지다. `ThirdPersonCamera`는 재개한 그 프레임의 Esc를 무시한다.

## 7. 남은 일

1. 아군 무기를 같은 방식으로 제작. 겹치는 것은 재활용한다. 한손검과 원형 방패(검사와 Raider), 활과 화살과 화살통(두 궁수). 지팡이와 장식 머리는 공유하지 않는다. 날 무기는 `SwordSpec`만 새로 쓰면 된다.
2. Play로 확인: 일시정지/재개, 적 무기의 휘두르는 방향과 위치.
3. Raider 검 다듬기: 철 텍스처를 레퍼런스처럼 넓고 옅게, 가운데 능선을 더 선명하게, 가드 위 V자 문양.
4. Shaman 지팡이에 붉은 수정 발광이 필요하면 별도 파트로 추가 (현재는 Tripo 모델 그대로).
