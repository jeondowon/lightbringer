# 무료 유닛 모델 파이프라인 (크레딧 최소화)

병사 1종당 비용: 이미지 약 0.5 크레딧(Claude가 생성) + 나머지 무료.

## 1. 방향별 이미지 (Claude)
`Assets/Art/Characters/<Unit>/Source/`에 생성됨:
- `*_1_Front.png` 정면
- `*_2_Side_FacingRight.png` 측면 (오른쪽을 봄)
- `*_3_Back.png` 후면
- `*_4_Side_FacingLeft.png` 측면 (왼쪽을 봄, 좌우 반전본)

## 2. 3D 변환 — Tripo (tripo3d.ai) 또는 Meshy (meshy.ai) 무료 사용량
1. 로그인 → Image to 3D (가능하면 **Multiview / 여러 장** 모드)
2. 슬롯에 이미지 넣기
   - Front ← `1_Front`, Back ← `3_Back`
   - Left ← `4_Side_FacingLeft`, Right ← `2_Side_FacingRight`
   - 결과의 얼굴/앞뒤가 뒤집혀 있으면 Left/Right 이미지를 서로 바꿔 다시 생성
   - 멀티뷰가 없거나 사용량이 부족하면 `1_Front` 한 장만 넣어도 됨 (병사는 작게 보여서 충분)
3. 텍스처 포함으로 생성, 가능하면 면 수 1~2만(병사는 화면에 많이 나오므로 가볍게)
4. **FBX**로 다운로드 (Mixamo용). FBX가 없으면 GLB도 가능(Claude가 변환 경로 안내)
5. **라이선스 확인:** 무료 플랜 결과물의 상업적 사용 조건(CC BY 등)을 확인

## 3. 리깅 + 동작 — Mixamo (mixamo.com, Adobe 무료 계정)
1. Upload Character → 2단계에서 FBX 업로드 → 턱·손목·팔꿈치·무릎·사타구니 마커 배치 → Next
2. 리깅된 캐릭터를 **T-Pose 상태로 Download** (Format FBX, Skin: With Skin)
3. Animations에서 검색해 각각 Download (Format FBX, **Skin: Without Skin**, In Place 체크 가능하면 체크)
   - 대기: `Sword And Shield Idle`
   - 이동: `Sword And Shield Run`
   - 공격: `Sword And Shield Slash`
   - (다른 병종은 Bow / Magic / Spear 계열로 검색)

## 4. 프로젝트에 넣기
- 리깅된 캐릭터 FBX → `Assets/Art/Characters/<Unit>/`
- 동작 FBX → `Assets/Art/Characters/<Unit>/Animations/Source/`
  - 파일 이름에 `idle`, `run`, `attack`(또는 slash) 포함
- 이후 연결(크기·방향·Legacy 설정·동작·무기 부착)은 Claude가 처리
