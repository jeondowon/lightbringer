# 에필로그 삽화 작업 기록

## 연출 방향

- 마왕 처치 후 3막 맵의 어둠과 안개가 걷히고 날씨가 밝아진다. 잿빛 지형과 고사목, 성채는 유지한다.
- 사용자 확인: 어둠의 군주 → 과거 빛의 용사 모습으로 전환 → 발광과 함께 소멸 → 주인공만 남음.
- 외형 전환·발광·소멸은 추후 코드로 구현한다. 삽화에 소멸 효과를 고정하지 않는다.
- 후속 이미지는 첫 이미지의 카메라, 배경, 주인공 위치와 자세를 유지하는 편집으로 제작할 예정. 아직 첫 이미지 외에는 제작하지 않았다.

## Epilogue_01_BeforeRestoration_Storybook_v1.png

- 도구: built-in image_gen. 게임 미연결, 코드 변경 없음.
- 첫 장면: 밝아진 3막 최종 성채를 배경으로 주인공과 온전한 어둠의 군주가 마주 선다. 복원 전 상태.
- 참조: `Assets/UI/Art/CampaignAtlas_Act3_v2.png`(3막 지형·성채), `Assets/UI/Resources/BossStory/WarlordClose_v1.png`(군주), `Assets/UI/Resources/BossStory/HeroResolve_v1.png`(성인 주인공).
- 두 인물 전신과 분리된 실루엣. 발광·입자·소멸·자막 없음.

```text
Use case: illustration-story.
Create ONE full-bleed 16:9 landscape storybook illustration for Lightbringer epilogue, first frame BEFORE the dark lord transforms back into the knight of light. The demon king has been defeated off-screen. This is a quiet reunion, NOT combat.
References: 1 CampaignAtlas_Act3_v2 is ENVIRONMENT ONLY: its distinctive final gothic citadel on craggy raised ground, grey barren basin, winding river, charcoal rocky ridges, bare dead trees, ruined defensive stone walls. Translate this map into a ground-level scene, NOT an overhead map. Remove the map's black mist, evil eyes, supernatural darkness, storm oppression. 2 WarlordClose is exact dark lord armor/closed helmet identity and hand-inked matte storybook style. 3 HeroResolve is exact adult heroine identity, hair, commander outfit and staff. Preserve their designs.
Composition: fixed cinematic eye-level full-body two-shot, adult heroine at left third in three-quarter side view facing right, the tall dark lord at right third in three-quarter view facing left toward her. They stand on the same stone terrace with a clear gap of about a person's width between them. Both complete figures including boots comfortably within frame. Never overlap either silhouette with the other or the staff. Heroine's face is readable in profile with cautious relief and quiet wonder. She holds her recognizable dark-shaft gold armillary blue-orb staff vertically on her outer left side. Dark lord stands calmly upright, shoulders relaxed, arms lowered, no threatening raised weapon; his familiar sheathed sword rests at his outer hip. His intact physical body is fully opaque from helmet to boots, helmet fully closed, face unseen, no bright purple eyes. Blackened steel armor with restrained antique gold edging and star badges, torn deep purple cape/tabard exactly matching reference. His posture should be suitable for a later same-pose match transition into the original mentor's armor.
Environment: foreground weathered grey stone terrace at the Act Three final battlefield, fragments of fallen siege walls to the sides. In middle distance the recognizable tall gothic citadel with clustered needle towers, monumental facade and arched stone bridge sits on rocky elevated ground. Beyond it grey barren hills, bare trees, and a faint visible river valley. Preserve the scarred Act Three landscape: no instant green forest, no flowers, no pristine courtyard. Weather has cleared after victory: departing grey clouds opening to pale blue sky, mild warm daylight softly illuminating the two figures and the citadel, clear air and long visibility, no thick fog. Brighter and calmer than previous battle illustrations but restrained natural color, not oversaturated or heavenly.
CRITICAL for future code animation: this is a CLEAN STATIC BEFORE-TRANSFORMATION BASE FRAME. No character glow, halo, bloom, light rays from the body, particles, sparks, floating motes, ash dissolution, transparency, magical circles, mixed half-transformed armor, white fade, or motion blur. Staff blue orb is a solid non-emissive blue gemstone. Only natural daylight. No corpse or demon king anywhere. Keep simple readable terrain behind the lord to support future compositing. Leave lower edge relatively quiet for future UI text, no blank panel.
Style must match established ActOne and ActTwo: hand-inked fine dark outlines, layered matte gouache, broken brush strokes, subtle mottled paint and paper texture, illustrated faces, muted navy/ivory/antique gold against grey stone and pale blue sky. NOT photorealism, NOT glossy 3D render. No captions, text, speech bubbles, borders, UI or watermark. One finished illustration, not a storyboard or multi-panel.
```

