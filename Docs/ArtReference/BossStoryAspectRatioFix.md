# 보스 이야기 이미지 비율 수정

2026-10-13. 게임에서 그림 전체가 가로/세로로 늘어나는 문제.

보스 그림 6장의 TextureImporter에 `nPOTScale: 1`(ToNearest)이 설정되어 있었다. 원본은 1672×941인데, 각 축을 2의 거듭제곱 크기로 바꾸면 원본 비율이 손상된다. `BossDialogueView`의 `ScaleToFit`은 이미 올바르게 설정되어 있어 화면 배치 코드는 변경하지 않았다.

수정:
- `Assets/UI/Resources/BossStory/*.png.meta` 6개: NPOT None, mipmap 끔, 압축 끔. PNG와 GUID 유지.
- `Assets/Scripts/Editor/BossStoryArtImport.cs`: 해당 폴더에 한정해 같은 임포트 정책 자동 적용. 압축 블록 크기에 맞추기 위한 크기 보정도 피한다.
- 메뉴 `Lightbringer > Boss Story > Import and Validate Artwork`: 재임포트 후 원본/실제 Texture2D 크기 비교. 결과는 `Docs/Validation/BossStoryArtChecks.txt`.

Unity 공식 설정 설명: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/textureimporternpotscale
