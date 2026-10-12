# 군주 이야기 — 영웅 얼굴·군주 자세 수정

후속 지팡이 교정 및 퇴각 장면 수정: [BossStoryStaffAndRetreatCorrections.md](BossStoryStaffAndRetreatCorrections.md).

2026-10-13. 사용자 요청: 영웅은 아시아인이며 캐릭터 시트의 얼굴을 따라야 한다. 군주의 격퇴 자세는 부상과 체중 지지가 분명해야 한다.

내장 imagegen 도구로 다음 두 파일을 편집·교체했다. 기존 파일명과 Unity .meta/GUID는 유지하여 현재 대화 연출이 수정된 그림을 그대로 사용한다. 두 결과 모두 1672×941이며 직접 시각 확인했다. 게임 로직은 변경하지 않았다.

- `Assets/UI/Resources/BossStory/HeroResolve_v1.png`: `CharacterSheet_FemaleHero_v1.png`의 동아시아 여성 얼굴을 기준으로 수정.
- `Assets/UI/Resources/BossStory/WarlordDefeated_v1.png`: 한쪽 무릎과 반대쪽 발을 디디고 검으로 몸을 지탱하는 자세로 수정.
- 이전 그림: `Docs/ArtReference/BossStoryPrevious/HeroResolve_before_face_correction.png`, `WarlordDefeated_before_pose_correction.png`.

## 영웅 편집 프롬프트

Use case: precise-object-edit / character identity correction. EDIT image 1, using image 2 ONLY as the authoritative identity reference for the female hero's face. Deliver the same 16:9 full-screen illustration, 1672x941 landscape. The user explicitly defines this fictional adult hero as Asian. Replace ONLY her facial appearance with the young adult EAST ASIAN woman depicted in the large face portrait of the character sheet: match her facial proportions, soft oval contour, eyes, cheeks, nose and lips naturally and closely. Dark brown eyes and dark brown hair. Preserve a determined, solemn expression, gaze to the right, and the current three-quarter head direction. Keep the bun, flyaway hair, armor, staff, blue orb, all background architecture, camera framing, character placement and every other scene element unchanged. Integrate the corrected face in the exact painterly inked storybook technique and warm/cool lighting of image 1; do not paste a photorealistic or glossy 3D face into the painting. No caricature or exaggerated ethnic markers. No text, no labels, no borders. Keep existing opaque background.

## 군주 편집 프롬프트

Use case: precise-object-edit / pose correction. EDIT the supplied fictional Dark Lord illustration. Preserve exact painterly inked storybook style, 16:9 landscape 1672x941 framing, ruined fortress and steps, gold dusk lighting, sparse violet wisps, empty shields, all armor details, black closed crown helmet and violet visor, gold star trim, purple tabard and greatsword identity. Replace the awkward standing hunch with a clear, physically convincing exhausted combat defeat pose: ONE armored knee firmly on a broad stone step, opposite boot planted solidly forward with knee bent roughly 90 degrees, torso leaning forward from hips over a stable triangular base. One gauntlet firmly grips the hilt of his greatsword, its blade planted vertically into the stone beside the forward boot, visibly bearing some of his weight; other hand clutches the wounded side of his torso. Shoulders heavy and lowered, elbows and wrists anatomically natural. His head is still lifted toward the heroine OFFSCREEN LEFT, violet visor defiant: injured and forced to withdraw, alive and resisting, not dead and not surrendering. Make the full kneeling silhouette, planted boot, supporting sword and knee contact immediately readable. Slightly raise the supporting step if needed so the essential pose is above the bottom 24% subtitle area. Preserve subject on right-center; leave left negative space and bottom mist/ground. No new character, no open portal, no seals or chains, no gore, no text or UI. Do not crop off the sword or feet.
