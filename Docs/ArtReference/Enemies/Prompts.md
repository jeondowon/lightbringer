# Enemies 생성 프롬프트·작업 노트

이미지 옆에 있던 프롬프트와 메모를 파일 단위로 모았다. 섹션 제목은 원래 파일명이다.

## Archer_01_Design_v1.prompt

```text
Generation mode: built-in image_gen
Asset: Enemy_UndeadArcher_Tripo_v1.png
Purpose: Lightbringer enemy concept, single-view image-to-3D input.

Use case: stylized-concept. Create one standalone full-body enemy character image for Lightbringer, used as image-to-3D input. Premium stylized fantasy 3D render, clean sculpted volumes and readable game silhouettes, not low-poly, not photorealistic, not chibi. Palette worn charcoal iron, aged leather, muted dark violet cloth, ivory bone where applicable. Pure white empty background, soft even studio lighting, no ground or pedestal, no strong shadows. Entire character and all equipment within frame with generous white margins. Near frontal view, orthographic-like camera at torso height with minimal perspective. Neutral relaxed standing A-pose, feet apart, arms separated from torso, equipment clear of legs, no overlapping limbs. Exactly ONE character ONE view, no collage, no turnaround, no text, no labels, no watermark, no scenery, no aura, no particles, no smoke, no floating pieces, no gore. Solid interpretable geometry. Undead skeletal archer, about 6.5 heads tall, ivory skull clearly visible under a short fitted dark hood, dark eye sockets with subtle purple irises without bloom. Lightweight leather brigandine, small iron shoulder guards, simple arm guards, short split cloth waist panels, solid armored boots. One sturdy curved wooden bow held vertically to the side in left hand, its whole silhouette clear of body and floor; right hand empty slightly open. Compact quiver attached high at the back, a few thick arrow shafts visible above shoulder. No cape, no drawn bow, no arrow crossing torso. Slim quick infantry silhouette.

```

## Archer_02_Body_Apose_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Archer_01_Design_v1.png
Purpose: Weapon-free body input for Tripo.

Use case: precise-object-edit. Input image is the character design identity reference. Preserve EXACT same character identity, face or helmet, species, body proportions, armor shapes, clothing, colors and materials. Make a rigging BODY INPUT render, not an armed design reference. REMOVE ALL weapons, shields, staffs, bows, quivers, arrows, scabbards, and any hand-held objects. Reconstruct unobstructed body and matching empty hands. Full body from head to boot soles. EXACT STRAIGHT FRONT VIEW, head and torso and pelvis facing directly forward, symmetrical neutral A-POSE: both arms straight diagonally DOWN and OUT 40 degrees away from the vertical torso, elbows nearly straight, wrists neutral, both hands CLOSED EMPTY FISTS with no objects inside, fists well away from thighs. Legs straight, feet shoulder-width apart and pointing forward, both feet level. Keep long tunic panels and armor from the reference, but arrange naturally with visible separation between legs. Whole character centered with generous margin above, below and beside fists. Orthographic-like camera at mid torso, no perspective distortion. Single flat LIGHT GRAY background #eeeeee, even soft neutral lighting, NO ground plane, NO cast shadows, NO props, NO weapons anywhere, no text, no inset, no extra views. Premium Stylized Fantasy 3D matching reference, clean sculpted forms, no photorealism. 

```

## Archer_03_Weapon_Bow_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Archer_01_Design_v1.png
Purpose: Standalone equipment reference. Archer equipment is for existing code reuse.

Use case: precise-object-edit. Reference image is a character holding equipment. Extract and reconstruct ONLY the specified equipment as a standalone product render, faithfully preserve its exact design, silhouette, colors and materials from reference. Remove all character, hands, arms, clothing and other props. Entire object visible with generous margin on all sides, centered, flat LIGHT GRAY background #eeeeee, no cast shadow, no floor or stand. Orthographic-like front view showing broad readable surface, no perspective foreshortening. Premium Stylized Fantasy 3D sculpted game asset, clean sturdy forms, no text, no watermark, no collage, no extra views. Only the wooden recurve BOW with dark iron reinforcement bands, dark leather grip and taut string. Whole bow vertical, face-on to the bow's curved profile so bow and string are visible. No arrows, no quiver. Single complete bow. This is a visual guide for recoloring existing procedural equipment.

```

## Archer_03_Weapon_Quiver_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Archer_01_Design_v1.png
Purpose: Standalone equipment reference. Archer equipment is for existing code reuse.

Use case: precise-object-edit. Reference image is a character holding equipment. Extract and reconstruct ONLY the specified equipment as a standalone product render, faithfully preserve its exact design, silhouette, colors and materials from reference. Remove all character, hands, arms, clothing and other props. Entire object visible with generous margin on all sides, centered, flat LIGHT GRAY background #eeeeee, no cast shadow, no floor or stand. Orthographic-like front view showing broad readable surface, no perspective foreshortening. Premium Stylized Fantasy 3D sculpted game asset, clean sturdy forms, no text, no watermark, no collage, no extra views. Only the dark leather QUIVER with its few arrows with muted violet fletching. Reconstruct the whole quiver that was partly hidden behind the character, matching the visible design, cylindrical dark leather container with simple worn iron rim, three arrows carried inside. Single quiver assembly vertical, visible front and slight side depth; no bow, no loose arrows outside quiver. This is equipment reference, not body input.

```

## Brute_01_Design_v1.prompt

```text
Generation mode: built-in image_gen
Asset: Enemy_Brute_Tripo_v1.png
Purpose: Lightbringer enemy concept, single-view image-to-3D input.

Use case: stylized-concept. Create one standalone full-body enemy character image for Lightbringer, used as image-to-3D input. Premium stylized fantasy 3D render, clean sculpted volumes and readable game silhouettes, not low-poly, not photorealistic, not chibi. Palette worn charcoal iron, aged leather, muted dark violet cloth, ivory bone where applicable. Pure white empty background, soft even studio lighting, no ground or pedestal, no strong shadows. Entire character and all equipment within frame with generous white margins. Near frontal view, orthographic-like camera at torso height with minimal perspective. Neutral relaxed standing A-pose, feet apart, arms separated from torso, equipment clear of legs, no overlapping limbs. Exactly ONE character ONE view, no collage, no turnaround, no text, no labels, no watermark, no scenery, no aura, no particles, no smoke, no floating pieces, no gore. Solid interpretable geometry. Large hulking fantasy monster infantry, hairless ash gray creature with a single heavy head, small deep set violet eyes, broad blunt nose, lower tusks, thick neck, powerful shoulders, long muscular arms, thick separated legs. About 5.5 heads tall, monstrous but coherent biped anatomy. Rough simple charcoal iron single shoulder plate, leather chest straps, dark violet short loincloth, iron wrist guards, heavy leather boots. One thick primitive iron-headed club held down and outward clear of leg, empty other hand. Clearly a living monster, not skeletal, not human knight, not a cute cartoon. No horns, no tail, no extra limbs, no gore. Heavy compact silhouette.

```

## Brute_02_Body_Apose_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Brute_01_Design_v1.png
Purpose: Weapon-free body input for Tripo.

Use case: precise-object-edit. Input image is the character design identity reference. Preserve EXACT same character identity, face or helmet, species, body proportions, armor shapes, clothing, colors and materials. Make a rigging BODY INPUT render, not an armed design reference. REMOVE ALL weapons, shields, staffs, bows, quivers, arrows, scabbards, and any hand-held objects. Reconstruct unobstructed body and matching empty hands. Full body from head to boot soles. EXACT STRAIGHT FRONT VIEW, head and torso and pelvis facing directly forward, symmetrical neutral A-POSE: both arms straight diagonally DOWN and OUT 40 degrees away from the vertical torso, elbows nearly straight, wrists neutral, both hands CLOSED EMPTY FISTS with no objects inside, fists well away from thighs. Legs straight, feet shoulder-width apart and pointing forward, both feet level. Keep long tunic panels and armor from the reference, but arrange naturally with visible separation between legs. Whole character centered with generous margin above, below and beside fists. Orthographic-like camera at mid torso, no perspective distortion. Single flat LIGHT GRAY background #eeeeee, even soft neutral lighting, NO ground plane, NO cast shadows, NO props, NO weapons anywhere, no text, no inset, no extra views. Premium Stylized Fantasy 3D matching reference, clean sculpted forms, no photorealism. 

```

## Brute_03_Weapon_Club_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Brute_01_Design_v1.png
Purpose: Standalone equipment reference. Archer equipment is for existing code reuse.

Use case: precise-object-edit. Reference image is a character holding equipment. Extract and reconstruct ONLY the specified equipment as a standalone product render, faithfully preserve its exact design, silhouette, colors and materials from reference. Remove all character, hands, arms, clothing and other props. Entire object visible with generous margin on all sides, centered, flat LIGHT GRAY background #eeeeee, no cast shadow, no floor or stand. Orthographic-like front view showing broad readable surface, no perspective foreshortening. Premium Stylized Fantasy 3D sculpted game asset, clean sturdy forms, no text, no watermark, no collage, no extra views. Only the huge CLUB from reference. Preserve the elongated heavy rough charcoal iron reinforced head with stout angular studs, wooden shaft, iron bands and wrapped leather handle. Vertical, heavy head at top and handle below. Complete weapon including the grip hidden by the reference hand, no hand or fingers. One complete club.

```

## DemonKing_SkyMotion_Test_v1.prompt

```text
Lightbringer — 마왕 움직임·사실성 시험 영상 v1
작성일: 2026-10-13
도구: Higgsfield generate_video
모델: seedance_2_0_mini
설정: 4초, 480p, 16:9, 무음, 1개
사전 견적: 2크레딧
생성 전 조회 잔액: 110.56크레딧
작업 ID: 68df7939-442c-46dc-a4b1-7a8c79e7f615
입력: DemonKing_SkyReveal_Reference_v4.png (start_image)
업로드 미디어 ID: 87632ddc-6143-4c46-baef-efb9972d1cd9
목적: 사용자 요청에 따라 비용을 최소화한 짧은 사실적 3D 영상 시험. 기존 이미지에서 시작하여 눈빛과 어둠의 움직임을 검토한다. 눈이 없는 장면에서 시작하는 전체 등장 영상은 아니다.
상태: 생성 완료(completed), 프로젝트에 MP4 저장 확인. 영상의 실제 화질·동작에 대한 재생 검토는 별도 필요.
결과 파일: DemonKing_SkyMotion_Test_v1.mp4
결과 URL: https://d8j0ntlcm91z4.cloudfront.net/user_3JJUT0MKENj8oBi491QAY0Jiqty/hf_20261012_200414_68df7939-442c-46dc-a4b1-7a8c79e7f615.mp4
생성 후 조회 잔액: 108.56크레딧 (전후 차감 2크레딧)

Final prompt:
Single continuous four-second cinematic shot based precisely on the supplied fortress image. Realistic high-end 3D CGI fantasy, physically plausible light, volumetric storm clouds and smoke, believable depth and atmospheric scattering, no comic or painted animation. Preserve the castle, central spire, bridge, rocky plateau, all architecture and composition without morphing. Camera nearly locked with an extremely slow subtle push forward. The demon king is a vast nonhuman darkness directly above this castle: two crimson eyes remain anchored in the same sky position. Animate the black inky shadow currents so they slowly stream and curl THROUGH the cloud volumes toward the space above the castle; wispy edges split and dissolve like smoke, never solid tentacles, ropes or rubber. Cloud banks billow slowly at different depths, soft wisps cross in front of the eyes; the red eye glow gradually intensifies slightly and holds at the end. Keep castle and eyes readable. Ominous vast scale, restrained movement. No humanoid body, face anatomy, mouth, horns, lightning, beams, explosions, new characters, scene cuts, camera shake, architecture destruction, subtitles or logos. Silent motion-and-realism test, not the full introductory sequence.

```

## DemonKing_SkyReveal_Reference_v1.prompt

```text
Lightbringer — 마왕 등장 영상용 외형 레퍼런스 v1
작성일: 2026-10-13
생성 도구: 내장 imagegen (CLI/API fallback 미사용)
이미지: DemonKing_SkyReveal_Reference_v1.png
상태: 첫 콘셉트 시안. 최종 외형 또는 실제 게임 그래픽 품질을 확정하는 자료가 아니다.
목적: 인체 없는 하늘의 어둠, 붉은 눈, 성채 대비 규모를 검토한다. 성채와 지형은 구도 참고용이며 실제 게임 맵 재현이 아니다.
검토: 구름과 눈의 규모감은 표현되었으나 어두운 사실적 판타지 질감이 강하다. 게임의 스타일라이즈드 아트와 맞추는 정도는 후속 검토한다. 힉스필드 영상은 아직 생성하지 않았다.

Final prompt:
Use case: stylized-concept. Asset type: first visual reference for a demon king reveal cinematic in the PC game Lightbringer, suitable as an image-to-video reference. Create one polished widescreen 16:9 landscape image. Subject: a vast NON-HUMANOID sentient darkness spreading horizontally across the sky above a distant fantasy fortress and battlefield. Two enormous narrow crimson glowing eyes emerge within the layered storm-dark cloud mass, looking down toward the battlefield. The darkness IS the demon king: absolutely no human figure, no head outline, no shoulders, torso, arms, hands, legs, crown, horns, nose, mouth, teeth or skull. No standing giant. Asymmetric broad sweeping banks of charcoal and midnight-indigo clouds with faint muted violet depth; ragged mist trails and slow-looking billows. Preserve readable volumes and intermediate tones rather than crushed black. Wisps partially veil the eye edges so they feel embedded deep within the clouds rather than pasted on. Eyes are the single strongest focal point, restrained crimson cores with subtle diffused glow, no laser beams. Composition: ground-level battlefield view tilted slightly upward, sky occupies roughly upper three quarters, small distant stone fortress and layered hills across lower quarter establish immense scale, dark terrain remains legible. Do not include close characters, army detail or an invented hero/warlord design; this reference establishes the demon king only. Premium stylized fantasy 3D cinematic art direction, refined sculpted cloud shapes and coherent atmospheric depth, painterly material restraint, PC game visual target, not photographic storm footage, not flat comic illustration, not low poly, not mobile game. Ominous restrained reveal moment immediately after eyes have opened, clouds poised for animation. No exploding fire, excessive lightning, neon purple, tentacles, text, UI, labels, collage, borders or watermark.

```

## DemonKing_SkyReveal_Reference_v2.prompt

```text
Lightbringer — 3막 성 위 마왕 등장 레퍼런스 v2
작성일: 2026-10-13
생성 도구: 내장 imagegen (CLI/API fallback 미사용)
이미지: DemonKing_SkyReveal_Reference_v2.png
입력 레퍼런스: Assets/UI/Art/CampaignAtlas_Act3_v1.png
사용자 지정: 마왕은 3막 지도에 있는 성 위에 나타나야 한다.
방향: 지도 오른쪽 위 최종 성채의 중앙 첨탑, 계단식 성벽, 바위 고원과 아치형 진입교를 참고하여 지상에서 보는 영상용 구도로 재해석했다. 마왕의 붉은 눈을 성 바로 위에 배치했다.
상태: 영상 제작 전 검토용 시안. 지도 원본은 수정하지 않았으며, 힉스필드 영상은 아직 생성하지 않았다. 원근 변경에 따른 건축 세부는 생성된 해석으로, 정확한 3D 복원은 아니다.

Final prompt:
Use case: stylized-concept. Asset type: revised cinematic reference for Lightbringer demon king entrance, one 16:9 widescreen frame. INPUT IMAGE 1: the actual ACT THREE CAMPAIGN MAP, strict reference for the LOCATION, fortress architecture, terrain and subdued palette. Translate the specific great citadel in the UPPER RIGHT of that map into a closer cinematic ground-level establishing view. It must recognizably be THAT citadel, not a new random castle. Preserve its dominant very tall narrow central Gothic spire, clustering subsidiary pointed towers, stepped concentric charcoal-grey stone walls, fortified terraces atop a craggy rocky plateau, and arched stone viaduct approaching its ledge. Barren olive-brown land, scattered leafless woodland and weathered grey rocks in the approach. No snowy mountains, ocean or lush valley. Frame the fortress prominently in the LOWER CENTER, with enough distance to show the plateau and approaching bridge, its central spire around the lower-middle of the frame. Above the castle DIRECTLY aligned with its central keep, an enormous sentient cloud darkness is revealing two narrow crimson glowing eyes. The eye pair must straddle the castle centerline: the demon king is ABOVE THIS CASTLE, not off to a side or over another location. He has no physical character body: the darkness spreads broadly and horizontally through the sky, occupying the upper half to two-thirds. No human head silhouette, no nose, mouth, chin, horns, crown, torso, shoulders, arms or hands. Only two crimson eyes within deep layered charcoal-indigo clouds, cloud wisps veil their edges, no floating solid eyeballs. The cloud banks above the fortress merge outward with the wider dark sky; subtle atmospheric layers preserve depth without becoming a detailed humanoid face. Strong scale contrast with the existing castle but keep the castle architecture clear, not obscured by fog or effects. Premium stylized fantasy game cinematic concept, refined painterly matte surfaces and sculpted cloudy volumes, grounded readable architecture from the map, not photographic, not flat overhead map, not comic hatching, not glossy generic fantasy poster. Restrained red glow, no laser attack, no explosions, no bright purple energy, no invented characters. No hero or warlord closeup. No text, UI, map markers, labels, frame or watermark. This is a cinematic view of the map's final destination, not an edit to the playable map UI.

```

## DemonKing_SkyReveal_Reference_v3.prompt

```text
Lightbringer — 프롤로그 어둠의 선을 추가한 마왕 레퍼런스 v3
작성일: 2026-10-13
생성 도구: 내장 imagegen (CLI/API fallback 미사용)
이미지: DemonKing_SkyReveal_Reference_v3.png
편집 대상: Docs/ArtReference/Enemies/DemonKing/DemonKing_SkyReveal_Reference_v2.png
모티프 참고: Docs/ArtReference/Prologue/Prologue_00C_BirthOfDarkness_Storybook_v1.png
사용자 지정: 구름뿐 아니라 프롤로그에서 사용하는 어둠의 선을 함께 표현한다.
관련 기존 구현: Assets/Scripts/UI/PrologueEffects.cs의 DemonKingBirth, Assets/Scripts/UI/SpriteFx.cs의 DarkInkFlow. 코드 수정 없이 형태·흐름을 참고했다.
방향: 3막 성채와 붉은 눈의 구도를 유지하고, 구름 사이에 먹물 같은 검은 흐름과 갈라지는 선을 추가했다.
검토: 성과 눈은 식별 가능하다. 굵은 흐름이 눈에 잘 띄므로, 후속 영상에서는 단단한 촉수처럼 휘두르지 않고 끝이 연무로 풀리고 합쳐지는 흐름으로 표현해야 한다.
상태: 검토용 이미지. 힉스필드 영상은 아직 생성하지 않았으며 지도와 게임 코드도 변경하지 않았다.

Final prompt:
Use case: precise-object-edit. Asset type: Lightbringer demon king reveal cinematic reference v3. INPUT IMAGE 1 is the EDIT TARGET, current fortress-and-cloud-eyes reference. INPUT IMAGE 2 is a MOTIF REFERENCE ONLY: the thin black ink-like flowing shadow currents visible around the villagers and across the street in the prologue illustration. Do not copy the people, village, framing, flat comic style or paper texture of image 2. Preserve image 1's fortress architecture, central tall Gothic spire, rocky plateau, arched bridge, terrain, exact camera framing, lighting palette and position of the two red eyes above the castle. Make ONE targeted addition: visibly incorporate long flowing BLACK SHADOW LINES and layered inky shadow streams into the existing cloud darkness, so the demon king is made of storm clouds PLUS the recognizable dark flowing lines from the prologue. Several elegantly curved asymmetric charcoal-black streams rise around the OUTER sides of the castle plateau and arc behind the fortress into the sky. They split into fine trailing threads and sweep laterally through the cloud banks around the eyes. Keep the central castle facade, spire and eyes unobscured. Important: these are flat-to-volumetric amorphous ink-and-smoke currents with ragged dry-brush edges and tapering ends that dissolve into vapor, not physical tentacles, snakes, cables, roots, fabric strips, arms or rigid tubes. Distinguish their dark cores from the slightly lighter grey cloud volumes; slight muted violet-grey edge light only, no glowing neon outlines. About 5-7 main graceful flowing strands, each accompanied by sparse fine filaments, plenty of breathing room; no dense spiderweb. Some threads are partly hidden in cloud to establish depth. Their movement direction visually gathers toward the darkness directly ABOVE THE CASTLE before spreading across the sky. The sky remains broad, non-humanoid, with only the red eyes, no face anatomy, body, horns or mouth. This is the SAME LOCATION and SAME SHOT as image 1 with inky flowing shadow added, not a redesigned castle or a new composition. Maintain high quality cinematic stylized fantasy appearance. One landscape 16:9 image, no text, UI or watermark.

```

## DemonKing_SkyReveal_Reference_v4.prompt

```text
Lightbringer — 캐릭터 시트의 3D 표현을 기준으로 수정한 마왕 레퍼런스 v4
작성일: 2026-10-13
생성 도구: 내장 imagegen (CLI/API fallback 미사용, Higgsfield 생성 미사용)
이미지: DemonKing_SkyReveal_Reference_v4.png
편집 대상: Docs/ArtReference/Enemies/DemonKing/DemonKing_SkyReveal_Reference_v3.png
주 스타일 참고: Docs/ArtReference/Enemies/Warlord/Warlord_01_Design_v1.png
보조 스타일 참고: Docs/ArtReference/Characters/CharacterSheet_FemaleHero_v1.png
사용자 지정: 영상과 마왕 이미지가 만화 그림체가 아니라 게임 캐릭터들처럼 3D여야 한다.
유지: 3막 지도 성채에 기반한 구도, 성 위 붉은 눈, 비인물형 어둠, 프롤로그의 어둠의 선 모티프.
변경: 삽화의 붓결과 해칭을 줄이고 성의 입체 재질·음영, 구름의 부피와 깊이, 연무 형태의 입체적인 어둠 흐름을 강조했다. 프롤로그는 흐름 모티프만 계승한다.
상태: 3D 렌더 표현의 검토용 생성 이미지. 실제 3D 모델·Unity 렌더·영상이 아니다. 최종 스타일 승인은 별도이며 게임 에셋이나 지도 원본은 변경하지 않았다.

Final prompt:
Use case: style-transfer. Asset type: revised 3D-rendered reference image for the Lightbringer demon king reveal cinematic. Create ONE landscape 16:9 frame. INPUT 1 is the EDIT TARGET and exact composition/architecture reference: castle on plateau, red eyes directly above the central spire, broad dark sky, flowing shadow lines. INPUT 2 is the PRIMARY RENDERING STYLE reference, the existing Warlord design: a convincingly three-dimensional sculpted premium stylized PC game asset with controlled PBR surface detail, clean material shading, tangible volume and soft realistic light response. INPUT 3 is a SECONDARY STYLE reference, the existing female hero character sheet: premium stylized fantasy 3D material quality and polished model presentation only; do NOT copy characters, sheet layout, text, parchment or ornaments. USER CORRECTION: the new image and eventual video MUST look 3D like the game's character assets, NOT comic artwork or a painting. Completely RE-RENDER input 1 as a polished high-end 3D game cinematic frame. Keep the same specific castle design: central tallest Gothic tower, surrounding lesser spires, broad layered stone fortifications, craggy elevated plateau and stone arched approach bridge, same camera and general placement. All architecture and terrain are convincing 3D meshes: clean broad sculpted bevels, physically coherent diffuse stone surfaces, contact shadows and ambient occlusion in recesses, restrained fine detail; no drawn outlines, scratchy hatching, brushstroke grain or painted stone texture. The sky demon has only red eyes, no humanoid head/body, horns or mouth. Make the cloud masses genuinely VOLUMETRIC and softly shaded with light scattering and layers of distance, not densely hatched black brushwork. Existing flowing black shadow lines remain important and clearly visible: translate their prologue ink motif into THREE-DIMENSIONAL VFX streams, with smoky charcoal cores, semi-translucent wispy edges, tapered branching filaments, subtle soft violet-grey edge illumination, and natural occlusion as they weave before/behind cloud volumes. These are ethereal streams of darkness, never solid rubber tentacles, black plastic ropes, skeletal arms or flat brushstrokes pasted over clouds. Retain several broad asymmetrical currents sweeping across the sky, plus fine dissolving trails; reduce visual clutter and keep eyes and castle readable. Two crimson glowing eyes embedded in shadow cloud above castle, subtle red light diffusion through surrounding vapor. Maintain midnight indigo, charcoal, muted violet, neutral stone and restrained crimson; clearly readable middle tones and volumetric depth. Cinematic soft side/rim light, realistic perspective, subtle atmospheric distance, grounded rendered game environment, mature premium stylized forms matching the character assets, not photoreal live action and not toy/chibi/mobile low-poly. No characters, no sheet layout, no text, no UI, no logo. Strongly exclude comic book, ink illustration, oil painting, gouache, watercolor, parchment, stippling, sketch marks, outlines, crosshatching, poster filter. The result should feel like a captured frame from a carefully lit 3D game cinematic with volumetric VFX, not a drawn reinterpretation.

```

## Enemy_Brute_Tripo_v1.prompt

```text
Generation mode: built-in image_gen
Asset: Enemy_Brute_Tripo_v1.png
Purpose: Lightbringer enemy concept, single-view image-to-3D input.

Use case: stylized-concept. Create one standalone full-body enemy character image for Lightbringer, used as image-to-3D input. Premium stylized fantasy 3D render, clean sculpted volumes and readable game silhouettes, not low-poly, not photorealistic, not chibi. Palette worn charcoal iron, aged leather, muted dark violet cloth, ivory bone where applicable. Pure white empty background, soft even studio lighting, no ground or pedestal, no strong shadows. Entire character and all equipment within frame with generous white margins. Near frontal view, orthographic-like camera at torso height with minimal perspective. Neutral relaxed standing A-pose, feet apart, arms separated from torso, equipment clear of legs, no overlapping limbs. Exactly ONE character ONE view, no collage, no turnaround, no text, no labels, no watermark, no scenery, no aura, no particles, no smoke, no floating pieces, no gore. Solid interpretable geometry. Large hulking fantasy monster infantry, hairless ash gray creature with a single heavy head, small deep set violet eyes, broad blunt nose, lower tusks, thick neck, powerful shoulders, long muscular arms, thick separated legs. About 5.5 heads tall, monstrous but coherent biped anatomy. Rough simple charcoal iron single shoulder plate, leather chest straps, dark violet short loincloth, iron wrist guards, heavy leather boots. One thick primitive iron-headed club held down and outward clear of leg, empty other hand. Clearly a living monster, not skeletal, not human knight, not a cute cartoon. No horns, no tail, no extra limbs, no gore. Heavy compact silhouette.

```

## Enemy_CorruptedTreant_Tripo_v1.prompt

```text
Generation mode: built-in image_gen
Asset: Enemy_CorruptedTreant_Tripo_v1.png
Purpose: Lightbringer enemy concept, single-view image-to-3D input.

Use case: stylized-concept. Create one standalone full-body enemy character image for Lightbringer, used as image-to-3D input. Premium stylized fantasy 3D render, clean sculpted volumes and readable game silhouettes, not low-poly, not photorealistic, not chibi. Palette worn charcoal iron, aged leather, muted dark violet cloth, ivory bone where applicable. Pure white empty background, soft even studio lighting, no ground or pedestal, no strong shadows. Entire character and all equipment within frame with generous white margins. Near frontal view, orthographic-like camera at torso height with minimal perspective. Neutral relaxed standing A-pose, feet apart, arms separated from torso, equipment clear of legs, no overlapping limbs. Exactly ONE character ONE view, no collage, no turnaround, no text, no labels, no watermark, no scenery, no aura, no particles, no smoke, no floating pieces, no gore. Solid interpretable geometry. Corrupted forest spirit embodied as a solid humanoid wooden treant infantry, clearly distinct from skeletons and armored monsters. Broad knotted dark bark torso, face carved organically into single solid trunk head, two small muted violet crystal eyes without bloom, short thick broken branch crown, powerful root-like legs with broad solid wooden feet, two solid bark arms and simple thick wooden fingers. About 5.5 heads tall. Thick root joints and a few small moss patches, a few embedded dull purple crystal wedges in chest and shoulder integrated physically. No weapons, no cloth, no armor. No foliage cloud, no thin twigs or fine vines, no floating parts, no tendrils, no effects. Solid sculpted wooden volumes with readable bark grooves; two arms and two legs clearly separated in neutral standing A-pose.

```

## Enemy_UndeadArcher_Tripo_v1.prompt

```text
Generation mode: built-in image_gen
Asset: Enemy_UndeadArcher_Tripo_v1.png
Purpose: Lightbringer enemy concept, single-view image-to-3D input.

Use case: stylized-concept. Create one standalone full-body enemy character image for Lightbringer, used as image-to-3D input. Premium stylized fantasy 3D render, clean sculpted volumes and readable game silhouettes, not low-poly, not photorealistic, not chibi. Palette worn charcoal iron, aged leather, muted dark violet cloth, ivory bone where applicable. Pure white empty background, soft even studio lighting, no ground or pedestal, no strong shadows. Entire character and all equipment within frame with generous white margins. Near frontal view, orthographic-like camera at torso height with minimal perspective. Neutral relaxed standing A-pose, feet apart, arms separated from torso, equipment clear of legs, no overlapping limbs. Exactly ONE character ONE view, no collage, no turnaround, no text, no labels, no watermark, no scenery, no aura, no particles, no smoke, no floating pieces, no gore. Solid interpretable geometry. Undead skeletal archer, about 6.5 heads tall, ivory skull clearly visible under a short fitted dark hood, dark eye sockets with subtle purple irises without bloom. Lightweight leather brigandine, small iron shoulder guards, simple arm guards, short split cloth waist panels, solid armored boots. One sturdy curved wooden bow held vertically to the side in left hand, its whole silhouette clear of body and floor; right hand empty slightly open. Compact quiver attached high at the back, a few thick arrow shafts visible above shoulder. No cape, no drawn bow, no arrow crossing torso. Slim quick infantry silhouette.

```

## Enemy_UndeadShaman_Tripo_v1.prompt

```text
Generation mode: built-in image_gen
Asset: Enemy_UndeadShaman_Tripo_v1.png
Purpose: Lightbringer enemy concept, single-view image-to-3D input.

Use case: stylized-concept. Create one standalone full-body enemy character image for Lightbringer, used as image-to-3D input. Premium stylized fantasy 3D render, clean sculpted volumes and readable game silhouettes, not low-poly, not photorealistic, not chibi. Palette worn charcoal iron, aged leather, muted dark violet cloth, ivory bone where applicable. Pure white empty background, soft even studio lighting, no ground or pedestal, no strong shadows. Entire character and all equipment within frame with generous white margins. Near frontal view, orthographic-like camera at torso height with minimal perspective. Neutral relaxed standing A-pose, feet apart, arms separated from torso, equipment clear of legs, no overlapping limbs. Exactly ONE character ONE view, no collage, no turnaround, no text, no labels, no watermark, no scenery, no aura, no particles, no smoke, no floating pieces, no gore. Solid interpretable geometry. Undead skeletal shaman, tall lean humanoid about 6.5 heads tall, readable ivory skull beneath close fitting violet hood, subtle violet eye pupils no light effects. Layered muted dark violet cloth tunic, leather harness, small bone clasps, simple charcoal iron bracers. Split knee-length robe exposes separated lower legs and solid boots, no floor-length drapery, no cape. Holds one thick carved dark wooden staff vertically beside body with white gap, staff top has a single solid faceted purple stone physically enclosed by a simple iron cage, no floating objects or magic. Other hand relaxed open away from torso. Staff and feet fully within image.

```

## Enemy_UndeadShieldbearer_Tripo_v1.prompt

```text
Generation mode: built-in image_gen
Asset: Enemy_UndeadShieldbearer_Tripo_v1.png
Purpose: Lightbringer enemy concept, single-view image-to-3D input.

Use case: stylized-concept. Create one standalone full-body enemy character image for Lightbringer, used as image-to-3D input. Premium stylized fantasy 3D render, clean sculpted volumes and readable game silhouettes, not low-poly, not photorealistic, not chibi. Palette worn charcoal iron, aged leather, muted dark violet cloth, ivory bone where applicable. Pure white empty background, soft even studio lighting, no ground or pedestal, no strong shadows. Entire character and all equipment within frame with generous white margins. Near frontal view, orthographic-like camera at torso height with minimal perspective. Neutral relaxed standing A-pose, feet apart, arms separated from torso, equipment clear of legs, no overlapping limbs. Exactly ONE character ONE view, no collage, no turnaround, no text, no labels, no watermark, no scenery, no aura, no particles, no smoke, no floating pieces, no gore. Solid interpretable geometry. Undead skeletal shield infantry, stout broad humanoid about 6 heads tall, ivory skull face under open-face battered iron helmet. Heavy simple worn charcoal plate armor, broad modest pauldrons, muted violet short split cloth skirt, reinforced knee guards and armored boots. One thick medium rectangular iron shield held out to character's left side, facing camera, away from torso so chest and legs stay visible; one short blunt iron mace held down outward in right hand. Shield simple embossed angular motif, no text. Common heavy infantry, not a king or boss. No cape or huge spikes.

```

## Enemy_UndeadSwordsman_Tripo_v1.prompt

```text
Generation mode: built-in image_gen
Asset: Enemy_UndeadSwordsman_Tripo_v1.png
Purpose: Lightbringer enemy concept, single-view image-to-3D input.

Use case: stylized-concept. Asset type: single-character input image for image-to-3D reconstruction for the PC fantasy game Lightbringer. Create ONE undead basic melee infantry soldier, premium stylized fantasy 3D character render, polished sculpted forms, convincing broad material surfaces, not photorealistic, not low-poly, not chibi. A clearly skeletal ivory skull with readable dark eye sockets and a restrained violet color inside them, no bloom; sturdy humanoid proportions about 6.5 heads tall. Worn dark iron simple breastplate, modest angular shoulder plates, leather belt, short ragged muted cloth waist panels, armored forearms and boots. Skeleton face and a few visible bone joints identify it as undead, but limbs have solid simple armored volumes. Common rank-and-file enemy, no ornate boss details, no crown, no huge horns, no cape. One thick simple chipped iron shortsword in right hand, held pointing down and slightly outward clear of the leg, left hand empty. Full body from skull to soles and entire sword visible, centered with generous margins. Near frontal view with just a slight 10 degree turn, orthographic-like camera at torso height without foreshortening. Neutral relaxed A-pose, arms separated from torso, straight legs separated, feet planted, clear negative spaces between limbs, no action pose. Plain pure white background, no environment, no pedestal, no ground plane, no dramatic cast shadow. Soft even studio light showing all armor shapes, moderate contrast. Detailed but clean production concept suitable for interpreting three-dimensional volumes. Exactly one character and one view, NO turnaround sheet, no inset, no text, no labels, no watermark, no particle effects, no aura, no smoke, no floating parts, no gore.

```

## Raider_01_Design_v1.prompt

```text
Generation mode: built-in image_gen
Purpose: Armed design reference only. Do not use as Tripo body input.

Premium Stylized Fantasy 3D for a high quality PC fantasy game, polished stylized sculpted shapes, broad readable forms and controlled material detail, not photorealistic, not low-poly, not mobile/chibi. Faction palette worn charcoal iron, dark leather, muted violet fabric and aged ivory bone. Soft even studio lighting, single flat very light gray background, no cast shadows, no environment, no ground props, no text, no labels, no watermark. Edit the reference into the final Raider design reference. Preserve the exact skeletal character, skull, armor, clothing, materials, proportions and chipped iron sword. Add a modest ROUND iron-rimmed dark wooden shield held in the left hand away from the body. Full-body single near-frontal view, sword and shield and feet entirely visible with margins. Armed neutral standing presentation. No other equipment.

```

## Raider_02_Body_Apose_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Raider_01_Design_v1.png
Purpose: Weapon-free body input for Tripo.

Use case: precise-object-edit. Input image is the character design identity reference. Preserve EXACT same character identity, face or helmet, species, body proportions, armor shapes, clothing, colors and materials. Make a rigging BODY INPUT render, not an armed design reference. REMOVE ALL weapons, shields, staffs, bows, quivers, arrows, scabbards, and any hand-held objects. Reconstruct unobstructed body and matching empty hands. Full body from head to boot soles. EXACT STRAIGHT FRONT VIEW, head and torso and pelvis facing directly forward, symmetrical neutral A-POSE: both arms straight diagonally DOWN and OUT 40 degrees away from the vertical torso, elbows nearly straight, wrists neutral, both hands CLOSED EMPTY FISTS with no objects inside, fists well away from thighs. Legs straight, feet shoulder-width apart and pointing forward, both feet level. Keep long tunic panels and armor from the reference, but arrange naturally with visible separation between legs. Whole character centered with generous margin above, below and beside fists. Orthographic-like camera at mid torso, no perspective distortion. Single flat LIGHT GRAY background #eeeeee, even soft neutral lighting, NO ground plane, NO cast shadows, NO props, NO weapons anywhere, no text, no inset, no extra views. Premium Stylized Fantasy 3D matching reference, clean sculpted forms, no photorealism. 

```

## Raider_03_Weapon_Shield_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Raider_01_Design_v1.png
Purpose: Standalone equipment reference. Archer equipment is for existing code reuse.

Use case: precise-object-edit. Reference image is a character holding equipment. Extract and reconstruct ONLY the specified equipment as a standalone product render, faithfully preserve its exact design, silhouette, colors and materials from reference. Remove all character, hands, arms, clothing and other props. Entire object visible with generous margin on all sides, centered, flat LIGHT GRAY background #eeeeee, no cast shadow, no floor or stand. Orthographic-like front view showing broad readable surface, no perspective foreshortening. Premium Stylized Fantasy 3D sculpted game asset, clean sturdy forms, no text, no watermark, no collage, no extra views. Only the ROUND wooden SHIELD with riveted dark iron rim and central iron boss. Front view, perfectly round outline, not oval perspective. Same dark violet stained wooden planks and metal wear. No sword. One complete shield.

```

## Raider_03_Weapon_Sword_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Raider_01_Design_v1.png
Purpose: Standalone equipment reference. Archer equipment is for existing code reuse.

Use case: precise-object-edit. Reference image is a character holding equipment. Extract and reconstruct ONLY the specified equipment as a standalone product render, faithfully preserve its exact design, silhouette, colors and materials from reference. Remove all character, hands, arms, clothing and other props. Entire object visible with generous margin on all sides, centered, flat LIGHT GRAY background #eeeeee, no cast shadow, no floor or stand. Orthographic-like front view showing broad readable surface, no perspective foreshortening. Premium Stylized Fantasy 3D sculpted game asset, clean sturdy forms, no text, no watermark, no collage, no extra views. Only the chipped iron one-handed SWORD. Upright vertical sword with point upward, grip downward, broad flat blade face visible. No shield. Single complete sword.

```

## Shaman_01_Design_v1.prompt

```text
Generation mode: built-in image_gen
Purpose: Armed design reference only. Do not use as Tripo body input.

Premium Stylized Fantasy 3D for a high quality PC fantasy game, polished stylized sculpted shapes, broad readable forms and controlled material detail, not photorealistic, not low-poly, not mobile/chibi. Faction palette worn charcoal iron, dark leather, muted violet fabric and aged ivory bone. Soft even studio lighting, single flat very light gray background, no cast shadows, no environment, no ground props, no text, no labels, no watermark. Edit this reference into the final Shaman design reference. Preserve the exact hooded skull character, robes, clothing, colors, belt and body proportions. Change only the staff head: replace the caged purple gemstone with a chunky ivory skull totem, dark iron collar underneath and a small inset dull violet stone in the forehead. The skull is a solid carving, no dangling trinkets. Keep the wooden staff shaft and wrapped grip. One armed character full body, front view, entire staff visible, all edges inside frame with margins.

```

## Shaman_02_Body_Apose_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Shaman_01_Design_v1.png
Purpose: Weapon-free body input for Tripo.

Use case: precise-object-edit. Input image is the character design identity reference. Preserve EXACT same character identity, face or helmet, species, body proportions, armor shapes, clothing, colors and materials. Make a rigging BODY INPUT render, not an armed design reference. REMOVE ALL weapons, shields, staffs, bows, quivers, arrows, scabbards, and any hand-held objects. Reconstruct unobstructed body and matching empty hands. Full body from head to boot soles. EXACT STRAIGHT FRONT VIEW, head and torso and pelvis facing directly forward, symmetrical neutral A-POSE: both arms straight diagonally DOWN and OUT 40 degrees away from the vertical torso, elbows nearly straight, wrists neutral, both hands CLOSED EMPTY FISTS with no objects inside, fists well away from thighs. Legs straight, feet shoulder-width apart and pointing forward, both feet level. Keep long tunic panels and armor from the reference, but arrange naturally with visible separation between legs. Whole character centered with generous margin above, below and beside fists. Orthographic-like camera at mid torso, no perspective distortion. Single flat LIGHT GRAY background #eeeeee, even soft neutral lighting, NO ground plane, NO cast shadows, NO props, NO weapons anywhere, no text, no inset, no extra views. Premium Stylized Fantasy 3D matching reference, clean sculpted forms, no photorealism. 

```

## Shaman_03_Weapon_Staff_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Shaman_01_Design_v1.png
Purpose: Standalone equipment reference. Archer equipment is for existing code reuse.

Use case: precise-object-edit. Reference image is a character holding equipment. Extract and reconstruct ONLY the specified equipment as a standalone product render, faithfully preserve its exact design, silhouette, colors and materials from reference. Remove all character, hands, arms, clothing and other props. Entire object visible with generous margin on all sides, centered, flat LIGHT GRAY background #eeeeee, no cast shadow, no floor or stand. Orthographic-like front view showing broad readable surface, no perspective foreshortening. Premium Stylized Fantasy 3D sculpted game asset, clean sturdy forms, no text, no watermark, no collage, no extra views. Only the complete wooden SKULL TOTEM STAFF, same carved ivory skull with tiny violet forehead stone, iron collar, twisted wooden shaft, dark leather wrapped grip and metal bands. Upright vertical, skull at top, whole shaft and bottom tip visible, skull front facing viewer. No character, no hands, no robes, no floating elements. One complete staff.

```

## Swarmling_01_Design_v1.prompt

```text
Generation mode: built-in image_gen
Purpose: Armed design reference only. Do not use as Tripo body input.

Premium Stylized Fantasy 3D for a high quality PC fantasy game, polished stylized sculpted shapes, broad readable forms and controlled material detail, not photorealistic, not low-poly, not mobile/chibi. Faction palette worn charcoal iron, dark leather, muted violet fabric and aged ivory bone. Soft even studio lighting, single flat very light gray background, no cast shadows, no environment, no ground props, no text, no labels, no watermark. Create a design reference of one Swarmling, an adult small wiry goblin-like humanoid enemy, about 5.5 heads tall, lean limbs with standard humanlike joint layout, two arms two legs, small ash-gray head with short pointed ears and a sharp angular face, tiny violet eyes, no horns or tail. Quick weak swarm infantry silhouette. Minimal simple dark leather vest, short violet waistcloth over fitted trousers, wrist wraps, simple leather boots, no cape, no large armor, no tiny dangling ornaments. One short thick chipped iron dagger with plain brown leather grip in its right hand pointed down and out, left hand empty. Full body and entire dagger, front view neutral stance, no attack pose, generous margin. One character one view.

```

## Swarmling_02_Body_Apose_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Swarmling_01_Design_v1.png
Purpose: Weapon-free body input for Tripo.

Use case: precise-object-edit. Input image is the character design identity reference. Preserve EXACT same character identity, face or helmet, species, body proportions, armor shapes, clothing, colors and materials. Make a rigging BODY INPUT render, not an armed design reference. REMOVE ALL weapons, shields, staffs, bows, quivers, arrows, scabbards, and any hand-held objects. Reconstruct unobstructed body and matching empty hands. Full body from head to boot soles. EXACT STRAIGHT FRONT VIEW, head and torso and pelvis facing directly forward, symmetrical neutral A-POSE: both arms straight diagonally DOWN and OUT 40 degrees away from the vertical torso, elbows nearly straight, wrists neutral, both hands CLOSED EMPTY FISTS with no objects inside, fists well away from thighs. Legs straight, feet shoulder-width apart and pointing forward, both feet level. Keep long tunic panels and armor from the reference, but arrange naturally with visible separation between legs. Whole character centered with generous margin above, below and beside fists. Orthographic-like camera at mid torso, no perspective distortion. Single flat LIGHT GRAY background #eeeeee, even soft neutral lighting, NO ground plane, NO cast shadows, NO props, NO weapons anywhere, no text, no inset, no extra views. Premium Stylized Fantasy 3D matching reference, clean sculpted forms, no photorealism. 

```

## Swarmling_03_Weapon_Dagger_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Swarmling_01_Design_v1.png
Purpose: Standalone equipment reference. Archer equipment is for existing code reuse.

Use case: precise-object-edit. Reference image is a character holding equipment. Extract and reconstruct ONLY the specified equipment as a standalone product render, faithfully preserve its exact design, silhouette, colors and materials from reference. Remove all character, hands, arms, clothing and other props. Entire object visible with generous margin on all sides, centered, flat LIGHT GRAY background #eeeeee, no cast shadow, no floor or stand. Orthographic-like front view showing broad readable surface, no perspective foreshortening. Premium Stylized Fantasy 3D sculpted game asset, clean sturdy forms, no text, no watermark, no collage, no extra views. Only the short thick chipped iron DAGGER with simple brown leather grip and short iron guard. Upright vertical blade pointing up, handle down, broad blade face visible. One complete dagger.

```

## Tripo_Input_Guide_v1

```text
Lightbringer 적군 원화 v1 — 2026-10-12

현재 상태: 원화 준비 완료. GLB 생성, 리깅, Unity 모델 연결은 아직 수행하지 않음.
제작 도구: 내장 image_gen. 각 PNG 옆의 .prompt.txt에 생성 프롬프트 기록.
정식 병종: Raider, Archer, Swarmling, Brute, Shaman, Warlord.
코드 대응: Swarmling = EnemyKind.Swarm, Warlord = EnemyKind.Boss.

파일 구분
01_Design: 무기를 든 디자인 참고용. Tripo 몸체 입력에 사용하지 않음.
02_Body_Apose: 무기 없는 정면 전신, 빈 주먹 A포즈. Tripo 몸체 입력용.
03_Weapon: 캐릭터와 손이 없는 개별 장비 이미지.
검과 방패, 활과 화살통을 분리했으므로 6종 각 3장 기준에 2장이 추가됨.
총 20 PNG: 디자인 6장, 몸체 6장, 장비 8장.
상위 Enemies 폴더의 기존 Enemy_* 이미지는 이전 후보 시안. 이번 제작 입력은 병종별 하위 폴더를 사용.

Tripo 몸체 입력
Raider/Raider_02_Body_Apose_v1.png
Archer/Archer_02_Body_Apose_v1.png
Swarmling/Swarmling_02_Body_Apose_v1.png
Brute/Brute_02_Body_Apose_v1.png
Shaman/Shaman_02_Body_Apose_v1.png
Warlord/Warlord_02_Body_Apose_v1.png

장비
Raider: 검 Sword + 둥근 방패 Shield. 각각 별도 생성.
Archer: 활 Bow + 화살통 Quiver. 결정한 기존 코드 재사용/색 변경용 참고.
Swarmling: 단검 Dagger. 코드 제작용 참고.
Brute: 큰 몽둥이 Club. Tripo 별도 생성.
Shaman: 해골 토템 지팡이 Staff. 전체 Tripo 생성 또는 코드 지팡이 제작 참고.
Warlord: 대검 Greatsword. Tripo 별도 생성.

후속 모델 조건 (사용자 결정)
몸체: GLB, 20,000~30,000 삼각형 목표.
무기: GLB, 2,000~5,000 삼각형 목표.
원화만으로 삼각형 수나 자동 리깅 성공 여부를 검증할 수 없음.
몸체와 장비는 합치지 않고, 손에 드는 장비는 리깅 후 손 뼈에 장착.
화살통은 등/몸통 부착 장비로 분리 관리.
GLB 저장 위치: Assets/Art/Characters/Enemy<이름>/
예: Assets/Art/Characters/EnemyRaider/EnemyRaider_Body.glb
예: Assets/Art/Characters/EnemyRaider/EnemyRaider_Sword.glb
예: Assets/Art/Characters/EnemyRaider/EnemyRaider_Shield.glb

리깅 계획 (사용자 결정)
Raider, Archer, Brute, Warlord: Higgsfield 대기/이동, 공격은 자체 스크립트.
Swarmling: 자체 스크립트.
Shaman: 로브형 RobedHumanoid 자체 스크립트.
Higgsfield 업로드/리깅은 사용자 진행.
무기 장착 후 대기/이동 클립의 간섭 및 부착 각도 확인.
GeneratedRig 변환 코드 등 게임 코드는 이번 원화 작업에서 변경하지 않음.


```

## Warlord_01_Design_v1.prompt

```text
Generation mode: built-in image_gen
Purpose: Armed design reference only. Do not use as Tripo body input.

Premium Stylized Fantasy 3D for a high quality PC fantasy game, polished stylized sculpted shapes, broad readable forms and controlled material detail, not photorealistic, not low-poly, not mobile/chibi. Faction palette worn charcoal iron, dark leather, muted violet fabric and aged ivory bone. Soft even studio lighting, single flat very light gray background, no cast shadows, no environment, no ground props, no text, no labels, no watermark. Create the final Warlord enemy BOSS design reference. A tall imposing HUMAN male dark warlord, human anatomy, about 7 heads tall. Face enclosed in a stern angular charcoal iron helmet with narrow violet visor slits, no skull face, no horns. Broad layered black iron plate armor with restrained aged bronze trims, strong pauldrons, armored gauntlets and boots, dark purple short split tabard, no cape. Far more commanding and high ranking than ordinary infantry, ornate but robust readable large sculpted details, no tiny filigree. One massive straight double-edged greatsword held in right hand angled down and outward beside body, broad dark steel blade with strong central bevel, worn bronze crossguard and dark purple wrapped two-handed grip. Full body including entire sword and feet within generous margins. Front view neutral standing presentation, ONE character ONE view.

```

## Warlord_02_Body_Apose_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Warlord_01_Design_v1.png
Purpose: Weapon-free body input for Tripo.

Use case: precise-object-edit. Input image is the character design identity reference. Preserve EXACT same character identity, face or helmet, species, body proportions, armor shapes, clothing, colors and materials. Make a rigging BODY INPUT render, not an armed design reference. REMOVE ALL weapons, shields, staffs, bows, quivers, arrows, scabbards, and any hand-held objects. Reconstruct unobstructed body and matching empty hands. Full body from head to boot soles. EXACT STRAIGHT FRONT VIEW, head and torso and pelvis facing directly forward, symmetrical neutral A-POSE: both arms straight diagonally DOWN and OUT 40 degrees away from the vertical torso, elbows nearly straight, wrists neutral, both hands CLOSED EMPTY FISTS with no objects inside, fists well away from thighs. Legs straight, feet shoulder-width apart and pointing forward, both feet level. Keep long tunic panels and armor from the reference, but arrange naturally with visible separation between legs. Whole character centered with generous margin above, below and beside fists. Orthographic-like camera at mid torso, no perspective distortion. Single flat LIGHT GRAY background #eeeeee, even soft neutral lighting, NO ground plane, NO cast shadows, NO props, NO weapons anywhere, no text, no inset, no extra views. Premium Stylized Fantasy 3D matching reference, clean sculpted forms, no photorealism. 

```

## Warlord_03_Weapon_Greatsword_v1.prompt

```text
Generation mode: built-in image_gen
Reference: Warlord_01_Design_v1.png
Purpose: Standalone equipment reference. Archer equipment is for existing code reuse.

Use case: precise-object-edit. Reference image is a character holding equipment. Extract and reconstruct ONLY the specified equipment as a standalone product render, faithfully preserve its exact design, silhouette, colors and materials from reference. Remove all character, hands, arms, clothing and other props. Entire object visible with generous margin on all sides, centered, flat LIGHT GRAY background #eeeeee, no cast shadow, no floor or stand. Orthographic-like front view showing broad readable surface, no perspective foreshortening. Premium Stylized Fantasy 3D sculpted game asset, clean sturdy forms, no text, no watermark, no collage, no extra views. Only the large GREATSWORD with broad straight double-edged charcoal steel blade, central bevel, angular aged bronze guard with central star medallion, dark violet wrapped long grip and metal pommel. Upright with tip up, grip down, broad flat blade face visible. Complete sword from tip to pommel. No character, no gauntlet. One complete greatsword.

```
