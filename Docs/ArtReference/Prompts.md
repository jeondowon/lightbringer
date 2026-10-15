# 이미지 생성 프롬프트 기록

Higgsfield 등으로 생성한 아트의 프롬프트와 보정 기록이다. 원본 이미지는 같은 폴더의 하위 폴더에 있다.


## 아군 유닛 아이콘 프롬프트 (GPT imggen)

병력 준비 화면 왼쪽 목록에 쓸 유닛 상징 아이콘 8종.
포맷은 기존 `Assets/UI/Art/Skill_*.png`의 금테 메달리온을 그대로 따른다.

### 작업 규칙

- 레퍼런스로 `Assets/UI/Art/Skill_Heal.png`, `Assets/UI/Art/Skill_Ice.png`를 먼저 첨부한다.
- 첫 장(검병)으로 톤을 확정한 뒤, 그 결과 이미지를 레퍼런스에 추가해서 나머지를 생성한다.
- 글로우 색은 이미지에 넣지 않는다. 코드(USS)에서 아이콘 뒤에 깔아 입힌다.
- 배경은 **완전 투명 PNG**로 받는다. 바깥 배경과 메달리온 안쪽 원 배경 모두 투명이어야
  뒤에 깐 글로우가 비쳐 보인다.
- 저장 위치와 이름: `Assets/UI/Art/UnitIcon_<Unit>.png`
  (`Swordsman`, `Archer`, `Shieldbearer`, `Spearman`, `Priest`, `Mage`, `Knight`, `Dragon`)

### 공통 프롬프트

```
A single game UI icon medallion, in the exact same style as the attached reference icons.

Frame: circular ornate medallion, worn antique gold border with four pointed diamond
ornaments at top, bottom, left and right, each set with a small dark sapphire gem; a deep
navy-blue ring with subtle engraved texture just inside the gold border; a pair of golden
laurel branches curving along the lower left and right sides.

Inner field: the circular area inside the navy ring is fully TRANSPARENT (alpha 0), with no
fill, no gradient, no glow, no light rays, no vignette. Only the central emblem floats in
the transparent center.

Style: premium stylized fantasy painting, hand-painted look, rich but controlled lighting,
soft rim light on the gold, subtle metallic scratches, high detail, clean readable
silhouette at small sizes. The emblem is a physical object, not a spell effect.

Composition: centered, a single emblem only, the emblem occupies about 60% of the inner
circle. No text, no letters, no numbers, no characters, no hands, no extra icons.

Output: transparent PNG background (alpha channel), square 1024x1024, the medallion fills
about 92% of the canvas with even margins, no drop shadow outside the frame.
```

### 유닛별 문구 (공통 프롬프트 뒤에 이어서 붙인다)

#### Swordsman (검병)
```
The central emblem: an upright longsword with a simple steel crossguard and leather-wrapped
grip, polished blade with a faint highlight along the edge.
```

#### Archer (궁수)
```
The central emblem: a recurve wooden bow with a single arrow nocked and drawn slightly,
small green leaf accents carved on the limbs.
```

#### Shieldbearer (방패병)
```
The central emblem: a sturdy kite shield with a steel rim, a subtle sun-rune engraving on
the face, slightly dented and worn.
```

#### Spearman (창병)
```
The central emblem: a long spear pointing upward with a leaf-shaped steel head and a small
red pennant fluttering below the blade.
```

#### Priest (사제)
```
The central emblem: a holy sigil of a radiant sun-cross, golden metal with a soft inner
light, short ornamental rays around it.
```

#### Mage (마법사)
```
The central emblem: a crystal-tipped magic staff topped with a violet orb held in a small
golden claw, faint engraved runes along the shaft.
```

#### Knight (기사)
```
The central emblem: a crowned knight's helmet in three-quarter profile with a feathered
plume, polished plate steel with gold trim.
```

#### Dragon (드래곤)
```
The central emblem: a dragon head in profile with swept-back horns, scaled hide and
glowing amber eyes, mouth slightly open. Physical sculpted emblem, no flames.
```

### 문제 생길 때 보정 문구

| 증상 | 추가할 문구 |
|---|---|
| 프레임이 장마다 달라짐 | `Keep the frame identical to the first icon. Change only the central emblem.` |
| 글자가 생김 | `Absolutely no text, no letters, no symbols that resemble writing.` |
| 안쪽 배경이 채워져 나옴 | `The inner circle must be fully transparent, remove any background fill or glow inside the ring.` |
| 상징이 너무 이펙트 같음 | `Physical object only, no magic effects, no energy, no light beams.` |
| 투명 PNG를 못 줌 | 흰색(#FFFFFF) 단색 배경으로 받는다. 안쪽 원도 흰색이면 제가 키잉으로 제거한다. |

### 게임 쪽 처리 계획

- 목록 타일(`.lb-upg-art`)은 `UnitIcon_*`로 교체하고 `scale-to-fit`으로 표시한다.
- 아이콘 뒤에 `Prep_Glow.png`를 깔고 `-unity-background-image-tint-color`로 역할별 색을 입힌다.
  (근접 / 원거리 / 지원 정도로 클래스 3개)
- 선택된 타일은 글로우 불투명도를 올려 강조, 잠긴 타일은 기존처럼 tint로 어둡게 처리한다.
- 오른쪽 큰 일러스트는 유지하고, 크기는 고정 캔버스로 정리한다(별도 작업).


## SkillIconPrompts

Lightbringer skill icon generation
Tool: Higgsfield generate_image, model gpt_image_2_5, aspect 1:1, 2 variants per skill; the best variant is kept
Date: 2026-10-13
Background: plain solid white; keyed to transparency by PreparationArtBuilder.Illustration (Lightbringer > UI > Build Skill Icons)

Shared style suffix (same family as BlessingIconPrompts.txt):
Style: beautifully hand-painted 3D fantasy inventory illustration, convincing sculpted volume, refined bevels, brushed antique gold, carefully painted highlights and rich shadows, royal navy ivory gold visual family. Centered isolated object fills 80% of a square canvas, generous clean margin, clear silhouette readable at 150 pixels. Three-quarter dimensional view. Consistent soft upper-left key light, restrained magical glow. Plain solid flat pure white background. No text, no border, no scene, no flat vector shapes, no emoji, no cartoon sticker, no excessive particles. One object composition only.

Docs/ArtReference/Skills/Skill_LightBeam_v1.png (variant b)
Single premium PC fantasy RPG skill icon for Lightbringer. A round sculpted antique-gold medallion frame with navy enamel inner ring, containing a blinding lance of pure white-gold holy light striking diagonally downward from the upper left, ending in a radiant four-point starburst impact with a thin golden halo ring. Represents a light beam spell.

Docs/ArtReference/Skills/Skill_Heal_v1.png (variant a)
... containing a luminous emerald and ivory healing light: two graceful glowing hands of light cupping a rising golden-green spiral of restorative energy with a few drifting sparkles, small gold laurel leaves on the frame. Represents a healing spell. No medical plus sign, no red cross.

Docs/ArtReference/Skills/Skill_Ice_v1.png (variant b)
... containing a jagged cluster of translucent glacial blue ice crystals erupting upward, a frozen intricate snowflake hovering above with pale cyan frost glow, frost rime creeping onto the gold frame. Represents a freezing ice spell.

Docs/ArtReference/Skills/Skill_Shockwave_v1.png (variant a)
... containing the base of an ornate golden staff driven into cracked stone ground, a powerful circular shockwave of ivory-gold light rippling outward in concentric rings with shattered stone fragments flying up. Represents a ground-slam shockwave spell.

Docs/ArtReference/Skills/Skill_Meteor_v1.png (variant a)
... containing a massive molten meteor with glowing cracked volcanic rock and a roaring tail of orange and gold fire falling diagonally from the upper left, a few embers trailing. Represents a meteor strike spell.


## BlessingIconPrompts

Lightbringer blessing icon generation
Tool: built-in image_gen; transparent_background=true
Date: 2026-10-03

Assets/UI/Art/Growth_AuraSize.png
Use case: stylized-concept. Asset type: single premium PC fantasy RPG blessing icon for Lightbringer. An ornate circular gold astrolabe relic enclosing a radiant ivory four-point star, two expanding concentric arcs of warm light. Represents expanding aura radius. Style: beautifully hand-painted 3D fantasy inventory illustration, convincing sculpted volume, refined bevels, brushed antique gold, carefully painted highlights and rich shadows, royal navy ivory gold visual family. Centered isolated object fills 80% of a square canvas, generous clean margin, clear silhouette readable at 150 pixels. Three-quarter dimensional view. Consistent soft upper-left key light, restrained magical glow. Transparent background. No text, no border, no scene, no checkerboard, no flat vector shapes, no emoji, no cartoon sticker, no excessive particles. One object composition only.

Assets/UI/Art/Growth_FoodProduction.png
Use case: stylized-concept. Asset type: single premium PC fantasy RPG blessing icon for Lightbringer. A generous sheaf of ripe golden wheat tied with a deep blue silk ribbon and small engraved gold clasp, several full dimensional grain heads. Represents food production. Style: beautifully hand-painted 3D fantasy inventory illustration, convincing sculpted volume, refined bevels, brushed antique gold, carefully painted highlights and rich shadows, royal navy ivory gold visual family. Centered isolated object fills 80% of a square canvas, generous clean margin, clear silhouette readable at 150 pixels. Three-quarter dimensional view. Consistent soft upper-left key light, restrained magical glow. Transparent background. No text, no border, no scene, no checkerboard, no flat vector shapes, no emoji, no cartoon sticker, no excessive particles. One object composition only.

Assets/UI/Art/Growth_ManaRecovery.png
Use case: stylized-concept. Asset type: single premium PC fantasy RPG blessing icon for Lightbringer. An elegant gold-mounted sapphire crystal chalice, rising luminous blue magical water curling back into the bowl. Represents mana regeneration. Style: beautifully hand-painted 3D fantasy inventory illustration, convincing sculpted volume, refined bevels, brushed antique gold, carefully painted highlights and rich shadows, royal navy ivory gold visual family. Centered isolated object fills 80% of a square canvas, generous clean margin, clear silhouette readable at 150 pixels. Three-quarter dimensional view. Consistent soft upper-left key light, restrained magical glow. Transparent background. No text, no border, no scene, no checkerboard, no flat vector shapes, no emoji, no cartoon sticker, no excessive particles. One object composition only.

Assets/UI/Art/Growth_Capacity.png
Use case: stylized-concept. Asset type: single premium PC fantasy RPG blessing icon for Lightbringer. A handsome open navy leather supply satchel with engraved gold fittings, wheat and one luminous sapphire potion vial securely inside. Represents food and mana reserves. Style: beautifully hand-painted 3D fantasy inventory illustration, convincing sculpted volume, refined bevels, brushed antique gold, carefully painted highlights and rich shadows, royal navy ivory gold visual family. Centered isolated object fills 80% of a square canvas, generous clean margin, clear silhouette readable at 150 pixels. Three-quarter dimensional view. Consistent soft upper-left key light, restrained magical glow. Transparent background. No text, no border, no scene, no checkerboard, no flat vector shapes, no emoji, no cartoon sticker, no excessive particles. One object composition only.

Assets/UI/Art/Growth_Leadership.png
Use case: stylized-concept. Asset type: single premium PC fantasy RPG blessing icon for Lightbringer. A flowing royal navy and muted violet silk command banner on a short ornate gold crossbar, embroidered ivory four-point star and branching gold laurel insignia. Represents leadership. Style: beautifully hand-painted 3D fantasy inventory illustration, convincing sculpted volume, refined bevels, brushed antique gold, carefully painted highlights and rich shadows, royal navy ivory gold visual family. Centered isolated object fills 80% of a square canvas, generous clean margin, clear silhouette readable at 150 pixels. Three-quarter dimensional view. Consistent soft upper-left key light, restrained magical glow. Transparent background. No text, no border, no scene, no checkerboard, no flat vector shapes, no emoji, no cartoon sticker, no excessive particles. One object composition only.

Assets/UI/Art/Growth_AuraBuff.png
Use case: stylized-concept. Asset type: single premium PC fantasy RPG blessing icon for Lightbringer. An upright silver sword with engraved gilded guard, embraced by two elegant golden wings and a restrained warm halo. Represents allied attack blessing. Style: beautifully hand-painted 3D fantasy inventory illustration, convincing sculpted volume, refined bevels, brushed antique gold, carefully painted highlights and rich shadows, royal navy ivory gold visual family. Centered isolated object fills 80% of a square canvas, generous clean margin, clear silhouette readable at 150 pixels. Three-quarter dimensional view. Consistent soft upper-left key light, restrained magical glow. Transparent background. No text, no border, no scene, no checkerboard, no flat vector shapes, no emoji, no cartoon sticker, no excessive particles. One object composition only.

Assets/UI/Art/Growth_HeroPower.png
Use case: stylized-concept. Asset type: single premium PC fantasy RPG blessing icon for Lightbringer. A diagonal elegant navy and gold mage staff, large luminous sapphire orb cradled in ornate golden curved prongs, a concentrated arc of ivory magic. Represents hero spell power. Style: beautifully hand-painted 3D fantasy inventory illustration, convincing sculpted volume, refined bevels, brushed antique gold, carefully painted highlights and rich shadows, royal navy ivory gold visual family. Centered isolated object fills 80% of a square canvas, generous clean margin, clear silhouette readable at 150 pixels. Three-quarter dimensional view. Consistent soft upper-left key light, restrained magical glow. Transparent background. No text, no border, no scene, no checkerboard, no flat vector shapes, no emoji, no cartoon sticker, no excessive particles. One object composition only.

Assets/UI/Art/Growth_HeroVitality.png
Use case: stylized-concept. Asset type: single premium PC fantasy RPG blessing icon for Lightbringer. A dimensional faceted deep ruby heart gemstone set in an elegant sculpted gold reliquary with protective silver wing-shaped sides, precious heirloom craftsmanship. Represents increased health. No medical plus sign. Style: beautifully hand-painted 3D fantasy inventory illustration, convincing sculpted volume, refined bevels, brushed antique gold, carefully painted highlights and rich shadows, royal navy ivory gold visual family. Centered isolated object fills 80% of a square canvas, generous clean margin, clear silhouette readable at 150 pixels. Three-quarter dimensional view. Consistent soft upper-left key light, restrained magical glow. Transparent background. No text, no border, no scene, no checkerboard, no flat vector shapes, no emoji, no cartoon sticker, no excessive particles. One object composition only.


## CampaignAtlas_v1.prompt

Tool: built-in image_gen (imagegen skill)
Asset: Assets/UI/Art/CampaignAtlas_v1.png
Prompt:
Create a premium PC fantasy campaign MAP BACKGROUND asset, landscape 16:9. No text, no letters, no UI, no icons, no markers, NO beacons or lights: interactive markers will be overlaid by the game. Top-down hand-painted cartographic relief map of a forgotten fantasy land, exquisite muted blue-grey ink, charcoal, desaturated moss and ivory-grey contours on dark matte paper, refined fine engraved contour lines with painterly terrain, sophisticated restrained collector's atlas aesthetic. Geographic composition: lower-left coastal grassy peninsula, woods and river valleys through center, rugged mountains upper-right, a distant ruined citadel at (80% width,25% height). Broad navigable land runs from (18%,74%) via (29%,61%), (39%,69%), (46%,49%), (57%,55%), (64%,36%), (74%,43%) to (80%,25%). Keep these eight positions uncluttered for small future beacon UI. Ocean along left and bottom edges, gently inked coastline, subtle cloud shadow in upper right. Map should fill canvas edge to edge, no frame, no decorative compass, no parchment beige. Very dark unobtrusive leftmost 10% and rightmost 8%, atmospheric negative space across top 15% and bottom 15% for overlay typography. Beautiful legible land shapes, quietly cinematic, handcrafted, no glossy mobile game aesthetic, no purple gradients, no glowing trails. Final game background, high detail.


## CampaignAtlas_Acts_v1.prompt

Tool: built-in image_gen (imagegen skill)
Date: 2026-10-13
Style reference: Assets/UI/Art/CampaignAtlas_v1.png
Scope: Three separate map images. Campaign UI integration is not part of this image generation task.
Direction: No snow biomes. Act 1 coastal woodland; Act 2 ancient hero ruins and warlord supply roads; Act 3 withered woodland and the demon king's domain.

ACT 1
Output: Assets/UI/Art/CampaignAtlas_Act1_v1.png

Use case: stylized-concept. Asset type: Lightbringer desktop fantasy game campaign atlas background. Input image 1 is the EXISTING CAMPAIGN MAP, a strict visual STYLE reference. Create ONE standalone landscape 16:9 map image, ideally 2560x1440. Match the reference's exquisite hand-painted cartographic bird's-eye relief, miniature detailed geography, engraved terrain details, muted blue-grey ink, charcoal, desaturated moss green, dark teal rivers, matte finish, realistic readable land shapes and restrained cinematic atmosphere. Same artist, same atlas series. Edge-to-edge geography, soft dark vignette, no parchment beige, no frame, no compass, no typography, no letters, no numbers, no stage markers, no UI, no dotted route overlays, no glowing trails. Geographic roads and bridges are appropriate. NO SNOW ANYWHERE, no white mountain caps, no glaciers, no ice, no desert, no lava biome. Stay with temperate woodland, grassland, bare rock, rivers and ruined medieval fantasy masonry so environments plausibly reuse the same game assets. Wide navigable land across the middle, small fine-scale landmarks rather than giant foreground buildings. Atmospheric low-detail top 15% and bottom 15% for future UI. Eight future UI positions measured from top-left must remain traversable open ground and uncluttered: (19%,72%), (30%,60%), (40%,67%), (46%,47%), (57%,54%), (64%,34%), (73%,42%), (80%,24%); DO NOT draw these markers. A destination fortress may sit just beyond the final position. THIS IMAGE IS ACT I, STAGES 1-8: the extinguished frontier. A coastal temperate forest kingdom, dark blue sea along left and lower margins, fertile muted green peninsula lower left, rolling wooded hills, wide meandering river valleys, modest old stone bridges, scattered tiny abandoned hamlets and watchtowers. Inland to the upper right, a FIRST WARLORD FORTRESS on a low rocky wooded ridge, smaller and more grounded than the reference's alpine citadel. All snowy alpine terrain is replaced by snow-free forested foothills and weathered grey rock. The visual journey goes from the lower-left frontier inland toward the upper-right fortress. Quiet melancholy and remaining natural life, restrained overcast daylight. This is one region, not a whole multi-biome world. Preserve the sophisticated visual richness and fine scale of the original.

----------------------------------------

ACT 2
Output: Assets/UI/Art/CampaignAtlas_Act2_v1.png

Use case: stylized-concept. Asset type: Lightbringer desktop fantasy game campaign atlas background. Input image 1 is the EXISTING CAMPAIGN MAP, a strict visual STYLE reference. Create ONE standalone landscape 16:9 map image, ideally 2560x1440. Match the reference's exquisite hand-painted cartographic bird's-eye relief, miniature detailed geography, engraved terrain details, muted blue-grey ink, charcoal, desaturated moss green, dark teal rivers, matte finish, realistic readable land shapes and restrained cinematic atmosphere. Same artist, same atlas series. Edge-to-edge geography, soft dark vignette, no parchment beige, no frame, no compass, no typography, no letters, no numbers, no stage markers, no UI, no dotted route overlays, no glowing trails. Geographic roads and bridges are appropriate. NO SNOW ANYWHERE, no white mountain caps, no glaciers, no ice, no desert, no lava biome. Stay with temperate woodland, grassland, bare rock, rivers and ruined medieval fantasy masonry so environments plausibly reuse the same game assets. Wide navigable land across the middle, small fine-scale landmarks rather than giant foreground buildings. Atmospheric low-detail top 15% and bottom 15% for future UI. Eight future UI positions measured from top-left must remain traversable open ground and uncluttered: (19%,72%), (30%,60%), (40%,67%), (46%,47%), (57%,54%), (64%,34%), (73%,42%), (80%,24%); DO NOT draw these markers. A destination fortress may sit just beyond the final position. THIS IMAGE IS ACT II, STAGES 9-16: traces of the ancient hero of light, and the warlord's military supply corridor. Depict a DIFFERENT INLAND REGION, not the same coastal peninsula and not an island: wooded rolling hills and wide grass-and-earth river valleys filling nearly all the canvas, with a river entering lower-left and bending east. Recognizable ancient hero heritage: a roofless old sanctuary with worn stone columns near lower-left approach, a small broken knight monument on a grassy knoll, fragments of an old paved pilgrimage road, collapsed watchtowers and mossy shrine courtyards. Blend these as small geographic landmarks into the fine-scale map. Through the center and toward upper right, the warlord has occupied old roads: a few restrained palisaded supply depots, timber storeyards, tiny wagon yards, guarded stone bridges and fortified crossroads, all linked by thin natural earth roads. End at a substantial warlord fortress beyond upper-right destination. Journey changes from forgotten sacred ruins to an organized military frontier. Keep open valleys between wooded ridges, earthy desaturated olive grass, weathered grey masonry, restrained thin river mist. No snow or ice. No huge mountains, no ocean rim. Same muted painterly atlas style and scale as the supplied original, fine-grained detail, buildings small and grounded.

----------------------------------------

ACT 3
Output: Assets/UI/Art/CampaignAtlas_Act3_v1.png

Use case: stylized-concept. Asset type: Lightbringer desktop fantasy game campaign atlas background. Input image 1 is the EXISTING CAMPAIGN MAP, a strict visual STYLE reference. Create ONE standalone landscape 16:9 map image, ideally 2560x1440. Match the reference's exquisite hand-painted cartographic bird's-eye relief, miniature detailed geography, engraved terrain details, muted blue-grey ink, charcoal, desaturated moss green, dark teal rivers, matte finish, realistic readable land shapes and restrained cinematic atmosphere. Same artist, same atlas series. Edge-to-edge geography, soft dark vignette, no parchment beige, no frame, no compass, no typography, no letters, no numbers, no stage markers, no UI, no dotted route overlays, no glowing trails. Geographic roads and bridges are appropriate. NO SNOW ANYWHERE, no white mountain caps, no glaciers, no ice, no desert, no lava biome. Stay with temperate woodland, grassland, bare rock, rivers and ruined medieval fantasy masonry so environments plausibly reuse the same game assets. Wide navigable land across the middle, small fine-scale landmarks rather than giant foreground buildings. Atmospheric low-detail top 15% and bottom 15% for future UI. Eight future UI positions measured from top-left must remain traversable open ground and uncluttered: (19%,72%), (30%,60%), (40%,67%), (46%,47%), (57%,54%), (64%,34%), (73%,42%), (80%,24%); DO NOT draw these markers. A destination fortress may sit just beyond the final position. THIS IMAGE IS ACT III, STAGES 17-24: the demon king's domain and the final advance. A DIFFERENT inland region, geographically related to Act II: once-green temperate river basin and rocky wooded uplands now withered by prolonged war and darkness. The lower-left approach retains a little moss-green forest, then wooded ridges become sparse dead woods and brown-grey grass, ruined villages, abandoned fields, shattered old sanctuaries, broken boundary walls. A few outer military strongpoints and stone bridges lead across broad WALKABLE valleys toward a great black-grey final citadel on a modest rocky plateau beyond the upper-right destination. This remains ordinary solid earth, rivers, forests, ruins and stone terrain, not a volcanic or magical new biome. Make the fortress imposing but retain atlas scale and fine geographical detail. Thin cold blue-grey river, weathered charcoal rock without snow, muted olive-brown ground, heavy ominous cloud shadows increasing toward upper right. Over and behind the final citadel, a restrained bank of immense charcoal storm darkness suggests the demon king's presence; a very faint pair of tiny dull crimson eyes may be discernible within the far upper-right cloud bank, integrated subtly rather than a giant face or character portrait. Ground must remain readable across the entire map, do not crush terrain into black. No bright magic, lava, volcanoes, ice, snow, purple fantasy corruption, floating islands or giant chasms. Match original reference painterly cartographic atlas style, NOT a cinematic ground-level landscape. No ocean rim. One complete standalone map, not panels.


## CampaignAtlas_Acts_v2.prompt

Tool: built-in image_gen (imagegen skill)
Date: 2026-10-13
Scope: Image generation only; saved as sibling v2 assets. Campaign UI references unchanged.

ACT 2
Edit target: Assets/UI/Art/CampaignAtlas_Act2_v1.png
Output: Assets/UI/Art/CampaignAtlas_Act2_v2.png
Use case: precise-object-edit. Asset: Lightbringer premium hand-painted fantasy campaign atlas background. Edit the supplied map, preserving its exact wide 16:9 framing, elevated cartographic perspective, geographical layout, scale, river courses, road network, landmark positions and intricate painterly engraved terrain style. This is an environmental art revision, not a new map composition. Maintain readable midtone terrain and paths for stage UI. No text, markers, interface, borders, watermarks, snow, ice, lava, neon magic or new characters. Preserve the original fine detail and atmospheric dark vignette. ACT II: Make this landscape significantly more desolate and darker than the original, a war-ravaged land in decline but not yet completely consumed. Turn most lush forest into sparse dying olive-grey woodland with bare trees, scorched patches and stumps. Change green grass to exhausted ash-brown grey earth, abandoned fields and worn roads. Increase ruined masonry, broken roofs and collapsed outlying watchtowers while preserving recognizable lower-left sanctuary columns and hero statue, central military supply corridor and upper-right warlord fortress. Add restrained soot haze and low cold river mist, heavy overcast shadows, muted slate-blue water. The fortress remains standing and readable. Make devastation tangible through physical environmental changes, not simply a dark filter. Keep some sparse surviving vegetation so Act III can be decisively more ruined. ONE standalone full map image.

ACT 3
Edit target: Assets/UI/Art/CampaignAtlas_Act3_v1.png
Output: Assets/UI/Art/CampaignAtlas_Act3_v2.png
Use case: precise-object-edit. Asset: Lightbringer premium hand-painted fantasy campaign atlas background. Edit the supplied map, preserving its exact wide 16:9 framing, elevated cartographic perspective, geographical layout, scale, river courses, road network, landmark positions and intricate painterly engraved terrain style. This is an environmental art revision, not a new map composition. Maintain readable midtone terrain and paths for stage UI. No text, markers, interface, borders, watermarks, snow, ice, lava, neon magic or new characters. Preserve the original fine detail and atmospheric dark vignette. ACT III: Transform the entire basin into a fully devastated realm engulfed in palpable supernatural darkness, considerably more ruined than Act II. Nearly all greenery is dead, skeletal black forests and shattered stumps across ash-grey barren ground. Villages and sanctuaries reduced to broken walls and rubble, abandoned fields erased, shattered outlying towers, fractured earth with narrow dark fissures that do not sever the traversable main route. Rivers cold dark slate, banks lifeless. Heavy rolling charcoal storm clouds, tendrils of black mist lying in valleys and creeping around the citadel create a lurking malevolent presence. Retain and emphasize the original imposing final gothic citadel in its exact upper-right location and silhouette. CRITICAL: preserve the ORIGINAL CONCEPT of the demon king's PAIR OF SMALL CRIMSON EYES in the storm darkness BEHIND and ABOVE the castle at far upper right. Keep their original location, pair arrangement and subtle scale, slightly more discernible in the murk, no full face, no giant eyeball, no character body. Total environmental devastation while the final citadel still stands as the seat of evil. No green thriving forest remains. Moody charcoal and cold slate tones, restrained crimson exclusively at eyes. Terrain and route details remain legible, no black crush. ONE standalone full map image.


## CampaignBeacon_Storybook_v2.prompt

캠페인 봉화 아이콘 — Storybook v2

제작: built-in image_gen, 2026-10-14
형태 및 그림체 기준: Docs/ArtReference/Prologue/Prologue_03_Beacons_Storybook_v5.png

기존 화로/불꽃 아이콘을 세계관의 석조 기둥, 남색 첨탑 지붕, 금색 고딕 등실,
마름모 수정, 별 문양으로 교체한다. 기존 프롤로그의 먹선과 무광 과슈 질감,
남색/석재색/금색을 유지하며 작은 지도 표식으로 단순화한다.

Assets/UI/Art/Chrome/Map_BeaconDark_Storybook_v2.png: 꺼진 회색 수정.
Assets/UI/Art/Chrome/Map_BeaconReady_Storybook_v2.png: 꺼진 수정, 외부 별 표식과 선택 후광.
Assets/UI/Art/Chrome/Map_BeaconLit_Storybook_v2.png: 금빛으로 점화된 수정과 국소 후광.

첫 점화 이미지를 기준으로 소등 상태를 편집하고, 소등 이미지에서 탈환 가능 상태를 편집했다.
세 이미지 모두 투명 PNG이며 원본 생성 해상도와 알파 채널을 보존했다.
LightbringerHUD.uss의 기존 74x88 표식 크기와 상태별 선택자를 유지한다.
기존 절차 생성기는 이전 파일명만 출력하므로 새 이미지에 영향을 주지 않는다.

검증: 생성 이미지 형태/색감 육안 확인, PNG 32-bit ARGB 및 모서리 알파 확인,
USS 3개 상태별 경로 확인. C# 변경 없음. Unity 실제 지도 렌더/Play 확인은 미실행.
Unity: 이미지 임포트 후 캠페인 지도를 다시 열어 미해방/탈환 가능/점화 완료 상태를 확인한다.


## Lightbringer_MainMenu_HeroBeacon_v1.prompt

Generator: built-in image_gen
Asset: Assets/Art/UI/MainMenu/Lightbringer_MainMenu_HeroBeacon_v1.png

Use case: stylized-concept.
Asset type: finished 16:9 landscape main-menu background illustration for the desktop fantasy game Lightbringer, preferably 3840x2160.
Create a new original image inspired by the supplied Paladog title screen's clear adventurous composition, using the visible Lightbringer female hero character sheet as character design guidance. Do not reproduce the Paladog logo or character.
Scene: on a high grassy rocky hill overlooking a vast layered pine forest valley at blue-hour dusk, a beautiful adult female human mage commander stands beside a lit medieval signal beacon. The beacon is a substantial weathered circular stone pedestal with an iron brazier and a vivid golden-orange fire, clearly a hilltop signal beacon. Wind carries a few glowing embers into the sky.
Hero: dark brown hair in a loose high bun with face-framing strands, expressive confident face, elegant white and royal-blue mage commander outfit with refined gold armor trim, dark fitted waist armor, tall dark boots, flowing long blue-and-white cape, gold-framed orb staff with a luminous blue star core. Preserve the reference's recognizable costume and silhouette. Full-body three-quarter view, facing the distant valley while her face remains partly visible. A restrained fine blue rune aura on the ground hints at her protective magic.
Composition: wide cinematic title-screen key art, hero and beacon together on the left third to left half atop an elevated rocky hill, the hero clearly readable at menu size, beacon slightly behind and to her left. Landscape slopes diagonally into a deep distant valley. Keep the upper center and upper right mostly calm atmospheric sky with subtle clouds for a future game title; lower right has quiet dark atmospheric landscape for future menu buttons. These areas must still be painted naturally. Strong foreground/midground/background separation, broad elegant shapes, balanced warm fire and cool magic. No other people.
Style: premium stylized fantasy 3D key art with painterly polish, rich crafted materials and rounded sculptural forms, cinematic environmental lighting, sophisticated PC adventure-game presentation. Not photorealistic, not low-poly, not flat mobile cartoon. Atmospheric, hopeful, quietly heroic, sense of embarking on a grand journey.
Constraints: one continuous edge-to-edge finished landscape background. NO text, NO lettering, NO logo, NO buttons, NO UI, NO watermark, NO frame, NO black bars. The image itself is the background asset and future UI will be added separately. Avoid huge spell effects, excessive particles, clutter, collage, character sheet layout.


## 군주 대화 일러스트

현재는 등장 3장·퇴각 3장으로 확장했다. 추가 4장의 파일·배치·프롬프트는 아래 "군주 이야기 — 장면 확장"을 참고한다. 아래는 유지한 최초 2장의 제작 기록이다.

2026-10-13. `imagegen` 스킬의 기본 내장 도구로 생성. 프롤로그의 정지 일러스트 연출을 요청받아 제작했다. 생성 결과를 프로젝트로 복사했고 원본은 유지했다.

- 대면: `Assets/UI/Resources/BossStory/Confrontation_v1.png`
- 퇴각: `Assets/UI/Resources/BossStory/Retreat_v1.png`
- 참조: `Assets/UI/Prologue/Prologue_08_FirstStep_Storybook_v1.png` (화풍), `Docs/ArtReference/Enemies/Warlord/Warlord_01_Design_v1.png` (군주), `Docs/ArtReference/Characters/CharacterSheet_FemaleHero_v1.png` (영웅).
- 그림에는 글자를 넣지 않으며 한국어 대사와 이름은 UI가 표시한다.

### 대면 프롬프트

Use case: illustration-story. Asset: full-screen 16:9 narrative illustration for Lightbringer, 1536x864 or 2048x1152 landscape. References ONLY: image1 painterly illustrated storybook texture/ink contours and muted palette; image2 EXACT Warlord armor identity: closed crowned black steel helmet, narrow violet visor, antique bronze star trim, purple tabard, massive greatsword; image3 EXACT adult female mage commander: dark brown hair in bun, white/blue/gold armored coat, thigh-high black armored boots, blue orb staff. Draw entirely as finely inked hand-painted dark fantasy storybook, textured brushwork, NOT a 3D screenshot. No letters, captions, UI, watermarks, panels or borders. Keep main faces and action within upper 72%, bottom 24% quiet dark mist/ground for dialogue overlay. The warlord is a living armored commander, NEVER sealed, chained or resurrected. Scene: first confrontation immediately after an enemy fortress is destroyed. Hero at left foreground seen in readable three-quarter profile, grief turning to resolve, gripping her glowing staff. Warlord on right middle ground standing commanding on broken stone steps with greatsword angled down. Broken dark fortress behind him, violet and teal aurora remnants in smoky dusk sky; scattered empty blue/gold shields and helmets imply lost army, no gore. Strong diagonal eye contact between hero and Warlord, threatening stillness, not midattack. Preserve readable silhouettes, golden staff rim light against dark purple enemy. Wide composition.

### 퇴각 프롬프트

Use case: illustration-story. Asset: full-screen 16:9 narrative illustration for Lightbringer, 1536x864 or 2048x1152 landscape. References ONLY: image1 painterly illustrated storybook texture/ink contours and muted palette; image2 EXACT Warlord armor identity: closed crowned black steel helmet, narrow violet visor, antique bronze star trim, purple tabard, massive greatsword; image3 EXACT adult female mage commander: dark brown hair in bun, white/blue/gold armored coat, thigh-high black armored boots, blue orb staff. Draw entirely as finely inked hand-painted dark fantasy storybook, textured brushwork, NOT a 3D screenshot. No letters, captions, UI, watermarks, panels or borders. Keep main faces and action within upper 72%, bottom 24% quiet dark mist/ground for dialogue overlay. The warlord is a living armored commander, NEVER sealed, chained or resurrected. Scene: same destroyed fortress AFTER the hero has defeated Warlord in combat. Warlord on right is clearly alive and deliberately retreating, stepping backward through a tall narrow vertical tear of violet-black smoke, half body still visible, damaged armor, one hand braced against side and greatsword held lowered in other, looks back toward hero with defiant violet visor. Hero stands left foreground with blue orb staff, exhausted but victorious, not attacking. In distant left background a restored beacon gives soft warm gold light. Warlord silhouette readable against portal, no corpse, no kneeling execution, no new demon, no seals, no chains. Hand-painted narrative still conveys retreat and promised return.


## 군주 이야기 — 장면 확장

2026-10-13 후속 수정: `HeroResolve_v1.png`의 얼굴을 캐릭터 시트의 동아시아 여성 외형으로, `WarlordDefeated_v1.png`의 자세를 한쪽 무릎과 검으로 몸을 지탱하는 형태로 교체했다. 편집 프롬프트는 아래 "군주 이야기 — 영웅 얼굴·군주 자세 수정" 참고. 아래 최초 생성 프롬프트는 제작 이력이다.

2026-10-13, 내장 imagegen 도구 사용. 기존 대면/퇴각 그림을 참조해 4장을 추가했다. 원본 2장은 유지한다. 저장 위치: `Assets/UI/Resources/BossStory/`.

| 연출 | 순서 | 파일 | 대사 |
|---|---|---|---|
| 등장 | 1 | Confrontation_v1.png | 기존 1~2줄: 봉화를 밝힌 지휘관과 군주의 대면 |
| 등장 | 2 | WarlordClose_v1.png | 나는 그분의 검이다… |
| 등장 | 3 | HeroResolve_v1.png | 누가 네 뒤에 있든… |
| 퇴각 | 1 | WarlordDefeated_v1.png | 이 빛은… 너를 얕보았군. |
| 퇴각 | 2 | Retreat_v1.png | 이것으로 끝이라 생각하지 마라… |
| 퇴각 | 3 | Aftermath_v1.png | 물러났군… 모두의 희생을 잊지 않겠어… |

모든 장면은 정지 그림이며, 전환에만 교차 페이드를 적용한다. 글자는 그림에 굽지 않고 UI에서 표시한다. 새 그림의 참조는 기존 `Confrontation_v1.png`, `Retreat_v1.png` 순서다.

### WarlordClose_v1.png

Use case: illustration-story. Create ONE new full-screen 16:9 landscape narrative illustration, 1672x941 or 2048x1152. This is a distinct sequential shot for the same Lightbringer story, not a collage, no panels. Reference1 establishes EXACT painterly storybook style, ink contours, texture, palette, character designs and ruined battlefield. Reference2 establishes continuity of the subsequent retreat. Adult female commander: brown hair bun, white and blue coat, gold armor, blue glowing orb staff. Dark Lord: closed crown-crested black steel helmet, purple eye slit, antique gold star ornaments on armor, purple tabard and greatsword. Keep identities and costumes consistent. Painterly illustrated dark fantasy, NOT 3D, not photoreal. No text, letters, logos or UI. Lower 24% quiet dark ground or mist reserved for subtitles, faces and primary action in upper 70%. No seals or imprisonment, no new characters or demons, no gore. SHOT 2 OF THE INTRO. Tight dramatic waist-up three-quarter view of the undefeated Dark Lord at right-center, staring toward the hero offscreen left. Large helmet and shoulders readable, one gauntlet resting on greatsword hilt, threatening calm. Broken enemy fortress silhouettes behind, violet and teal aurora remnants in smoke, bronze rim light. The heroine does not appear in this shot. Use the same crowned helmet geometry as reference, absolutely no exposed face. This accompanies 'I am his sword. The road to the Demon King ends here.' No text.

### HeroResolve_v1.png

Use case: illustration-story. Create ONE new full-screen 16:9 landscape narrative illustration, 1672x941 or 2048x1152. This is a distinct sequential shot for the same Lightbringer story, not a collage, no panels. Reference1 establishes EXACT painterly storybook style, ink contours, texture, palette, character designs and ruined battlefield. Reference2 establishes continuity of the subsequent retreat. Adult female commander: brown hair bun, white and blue coat, gold armor, blue glowing orb staff. Dark Lord: closed crown-crested black steel helmet, purple eye slit, antique gold star ornaments on armor, purple tabard and greatsword. Keep identities and costumes consistent. Painterly illustrated dark fantasy, NOT 3D, not photoreal. No text, letters, logos or UI. Lower 24% quiet dark ground or mist reserved for subtitles, faces and primary action in upper 70%. No seals or imprisonment, no new characters or demons, no gore. SHOT 3 OF THE INTRO. Medium close-up of the female commander from chest to head, left-center in three-quarter profile facing right toward offscreen Dark Lord. Show her entire face clearly: young adult human, dark brown eyes, tied brown hair, determined grief rather than smiling. Blue orb staff alongside shoulder; white/blue/gold armored coat. Ruined battlefield, fallen empty blue shields in soft background, pale blue rim light on face and warm subdued sky. No visible Dark Lord. Same storybook ink and dense brush texture as reference, NOT anime cel shading. Her posture and gaze vow to reclaim this land.

### WarlordDefeated_v1.png

Use case: illustration-story. Create ONE new full-screen 16:9 landscape narrative illustration, 1672x941 or 2048x1152. This is a distinct sequential shot for the same Lightbringer story, not a collage, no panels. Reference1 establishes EXACT painterly storybook style, ink contours, texture, palette, character designs and ruined battlefield. Reference2 establishes continuity of the subsequent retreat. Adult female commander: brown hair bun, white and blue coat, gold armor, blue glowing orb staff. Dark Lord: closed crown-crested black steel helmet, purple eye slit, antique gold star ornaments on armor, purple tabard and greatsword. Keep identities and costumes consistent. Painterly illustrated dark fantasy, NOT 3D, not photoreal. No text, letters, logos or UI. Lower 24% quiet dark ground or mist reserved for subtitles, faces and primary action in upper 70%. No seals or imprisonment, no new characters or demons, no gore. SHOT 1 OF THE OUTRO. Dark Lord has just been defeated in battle but is ALIVE. Medium-wide three-quarter view of armored Dark Lord alone near right-center on ruined fortress stone steps, slightly hunched and bracing heavily on planted greatsword with one gauntleted hand, other hand pressing damaged side armor. Scratched black armor, purple tabard torn, closed crown helmet looks toward hero offscreen left with narrow violet eyes. Clearly wounded and forced to withdraw, not kneeling, not a corpse. Thin violet smoke begins gathering behind him but NO open portal yet. Empty fallen shields, moody dusk, precise silhouette. Same battlefield/style as reference. Preserve star armor trim.

### Aftermath_v1.png

Use case: illustration-story. Create ONE new full-screen 16:9 landscape narrative illustration, 1672x941 or 2048x1152. This is a distinct sequential shot for the same Lightbringer story, not a collage, no panels. Reference1 establishes EXACT painterly storybook style, ink contours, texture, palette, character designs and ruined battlefield. Reference2 establishes continuity of the subsequent retreat. Adult female commander: brown hair bun, white and blue coat, gold armor, blue glowing orb staff. Dark Lord: closed crown-crested black steel helmet, purple eye slit, antique gold star ornaments on armor, purple tabard and greatsword. Keep identities and costumes consistent. Painterly illustrated dark fantasy, NOT 3D, not photoreal. No text, letters, logos or UI. Lower 24% quiet dark ground or mist reserved for subtitles, faces and primary action in upper 70%. No seals or imprisonment, no new characters or demons, no gore. SHOT 3 OF THE OUTRO, AFTER the Dark Lord has escaped. WIDE quiet aftermath of SAME ruined fortress battlefield. Heroine left-center seen from behind in readable three-quarter full figure, staff lowered, surveying empty stone steps at right where the Dark Lord stood. He is COMPLETELY ABSENT. Portal CLOSED, only very faint violet wisps dispersing near steps, gold sunrise breaks through the smoke. In far distance left, a modest restored beacon emits warm gold light. Scattered empty allied blue/gold shields and helmets convey sacrifice without bodies or gore. Solemn earned victory, not celebration. White-blue-gold cloak, brown bun, blue orb staff remain identical to reference. Atmospheric depth, narrative closure.


## 군주 이야기 — 영웅 얼굴·군주 자세 수정

후속 지팡이 교정 및 퇴각 장면 수정: 아래 "영웅 지팡이와 군주 퇴각 장면 수정".

2026-10-13. 사용자 요청: 영웅은 아시아인이며 캐릭터 시트의 얼굴을 따라야 한다. 군주의 격퇴 자세는 부상과 체중 지지가 분명해야 한다.

내장 imagegen 도구로 다음 두 파일을 편집·교체했다. 기존 파일명과 Unity .meta/GUID는 유지하여 현재 대화 연출이 수정된 그림을 그대로 사용한다. 두 결과 모두 1672×941이며 직접 시각 확인했다. 게임 로직은 변경하지 않았다.

- `Assets/UI/Resources/BossStory/HeroResolve_v1.png`: `CharacterSheet_FemaleHero_v1.png`의 동아시아 여성 얼굴을 기준으로 수정.
- `Assets/UI/Resources/BossStory/WarlordDefeated_v1.png`: 한쪽 무릎과 반대쪽 발을 디디고 검으로 몸을 지탱하는 자세로 수정.
- 이전 그림: `Docs/ArtReference/BossStoryPrevious/HeroResolve_before_face_correction.png`, `WarlordDefeated_before_pose_correction.png`.

### 영웅 편집 프롬프트

Use case: precise-object-edit / character identity correction. EDIT image 1, using image 2 ONLY as the authoritative identity reference for the female hero's face. Deliver the same 16:9 full-screen illustration, 1672x941 landscape. The user explicitly defines this fictional adult hero as Asian. Replace ONLY her facial appearance with the young adult EAST ASIAN woman depicted in the large face portrait of the character sheet: match her facial proportions, soft oval contour, eyes, cheeks, nose and lips naturally and closely. Dark brown eyes and dark brown hair. Preserve a determined, solemn expression, gaze to the right, and the current three-quarter head direction. Keep the bun, flyaway hair, armor, staff, blue orb, all background architecture, camera framing, character placement and every other scene element unchanged. Integrate the corrected face in the exact painterly inked storybook technique and warm/cool lighting of image 1; do not paste a photorealistic or glossy 3D face into the painting. No caricature or exaggerated ethnic markers. No text, no labels, no borders. Keep existing opaque background.

### 군주 편집 프롬프트

Use case: precise-object-edit / pose correction. EDIT the supplied fictional Dark Lord illustration. Preserve exact painterly inked storybook style, 16:9 landscape 1672x941 framing, ruined fortress and steps, gold dusk lighting, sparse violet wisps, empty shields, all armor details, black closed crown helmet and violet visor, gold star trim, purple tabard and greatsword identity. Replace the awkward standing hunch with a clear, physically convincing exhausted combat defeat pose: ONE armored knee firmly on a broad stone step, opposite boot planted solidly forward with knee bent roughly 90 degrees, torso leaning forward from hips over a stable triangular base. One gauntlet firmly grips the hilt of his greatsword, its blade planted vertically into the stone beside the forward boot, visibly bearing some of his weight; other hand clutches the wounded side of his torso. Shoulders heavy and lowered, elbows and wrists anatomically natural. His head is still lifted toward the heroine OFFSCREEN LEFT, violet visor defiant: injured and forced to withdraw, alive and resisting, not dead and not surrendering. Make the full kneeling silhouette, planted boot, supporting sword and knee contact immediately readable. Slightly raise the supporting step if needed so the essential pose is above the bottom 24% subtitle area. Preserve subject on right-center; leave left negative space and bottom mist/ground. No new character, no open portal, no seals or chains, no gore, no text or UI. Do not crop off the sword or feet.


## 영웅 지팡이와 군주 퇴각 장면 수정

2026-10-13. 내장 imagegen 편집 도구 사용. 기존 얼굴 교정 이후 후속 요청.

### 교체 대상과 기준

- `Assets/UI/Resources/BossStory/HeroResolve_v1.png`: 수정된 동아시아 여성 얼굴과 의상을 유지하며 지팡이를 전용 원화에 맞춘다.
- `Assets/UI/Resources/BossStory/Retreat_v1.png`: `WarlordDefeated_v1.png`의 한쪽 무릎·검으로 몸을 지탱하는 자세를 이어가며 등 뒤 포탈이 열린다. 영웅은 치맛자락과 불투명한 하의로 하체를 가린다. 지팡이도 같은 원화로 맞춘다.
- 지팡이 기준: `Assets/Art/Characters/FemaleHero/Props/Source/HeroStaff_Concept.png`.
- 얼굴·의상 기준: `Docs/ArtReference/Characters/CharacterSheet_FemaleHero_v1.png`, `Assets/Art/Characters/FemaleHero/Source/FemaleHero_View_v2_front.png`.
- 교체 전 백업: `Docs/ArtReference/BossStoryPrevious/HeroResolve_before_staff_correction.png`, `Retreat_before_pose_correction.png`.
- 기존 파일명과 `.meta`를 유지한다. `BossDialogueView`의 이미지 연결 및 게임 로직은 변경하지 않는다.

### 확인 결과

두 최종 이미지를 위 Assets 경로에 교체 저장했다. 모두 1672×941. 얼굴·의상 유지, 푸른 구체와 상단/좌우 수정이 있는 지팡이, 군주의 무릎·검 지지 자세, 등 뒤 포탈, 영웅의 하체 의복 덮임을 직접 시각 확인했다. 두 `.meta`의 SHA256이 교체 전과 같음을 확인했다. 이미지 전용 변경으로 컴파일/플레이 테스트는 실행하지 않았다. Unity에서 Assets > Refresh 후 재생하여 대화 화면에서 확인한다.

### HeroResolve 편집 프롬프트

Use case: precise-object-edit. Edit image 1 HeroResolve, a 16:9 illustrated game dialogue still, preserving its composition, painted/inked storybook technique, ruined fortress sunset, adult East Asian woman's face, expression, dark brown bun and loose strands, ALL white/blue/gold clothing, dark corset, shoulder armor, clasp and pose exactly. Image 2 is the authoritative STAFF PROP reference. Image 3 character sheet is identity reference only; do not change her existing corrected face or costume. Replace ONLY the inaccurate staff in image 1 with the EXACT staff design from image 2, adapted into the same painterly technique and existing left-of-face placement. Key geometry: dimensional deep sapphire BLUE FACETED SPHERE, not a flat luminous disk; concentric sculpted gold circular frame; a prominent blue crystal spear tip at 12 o'clock; matching blue pointed crystal tips at 3 and 9 o'clock; small diagonal gold star points; sculpted lower gold diamond ornament with blue inlay and gold/blue neck; dark navy straight shaft and restrained gold collars. Match the relative proportions and ornament silhouettes directly to image 2. Keep enough blue glow for the story scene but do not wash out the spherical gem or crystal tips. Remove the old giant solid-gold top spike, needle-like oversized lower spike, excessive fine star ornaments and flat glowing target disk. Staff must be recognizably the reference prop. Preserve original framing and all pixels outside the staff and its immediate glow as closely as possible. No text, UI, borders, extra hands, redesign of face or clothing. Opaque landscape 1672x941.

### Retreat 자세·의복 편집 프롬프트

Use case: compositing / precise-object-edit. Edit image 1 Retreat, preserving its 16:9 landscape painted inked storybook style, ruined stone fortress, dusk, distant beacon, female hero on left and armored Dark Lord on right. Image 2 WarlordDefeated is the exact POSE AND LORD IDENTITY reference. This scene is the next story beat after image 2: the living injured lord remains in that SAME kneeling posture while a dark portal opens DIRECTLY BEHIND his back. Reproduce image 2 pose closely: one armored knee on stone, opposite boot planted forward, one hand raised and resting on the pommel of the greatsword planted vertically beside that boot, other gauntlet clutching his torso, shoulders lowered, closed crowned helmet lifted defiantly toward heroine at left. Keep the identical black/gold armor, violet tabard, helmet and sword. No standing pose, no walking into portal, no arm reaching backward. Put his kneeling body on a raised broad stone step so knee, boot and vertical sword remain readable above the bottom subtitle area. Behind him an ominous vertical oval rift with near-black center and restrained violet aurora/smoky edges, clearly separate behind his silhouette; do not obscure the knee or supporting sword. Keep hero left facing lord in vigilant neutral stance. User specifically requests modest clothing coverage: white coat skirt/tabard and blue-lined white cape must drape over hips and ALL upper thighs, closing any open slit; opaque dark leggings under the coat and existing armored boots below, no exposed upper-thigh skin, underwear, buttocks or crotch, no transparent fabric or provocative framing. Keep her character's established costume palette, gold motifs, bun and adult East Asian facial identity. Image 3 is authoritative staff reference: her staff must have a blue spherical gem, gold circular frame, blue crystal spear tip at top and matching left/right blue crystal points, dark shaft, matching this prop rather than inventing another head. Image 4 is hero face/outfit identity reference; preserve its upper costume and use the requested modest coverage for the lower costume. Maintain a readable balanced heroic scene, bottom 20-24 percent ground/mist for later UI overlay. No text, labels, UI, seals, chains, blood or new characters. Opaque 1672x941 landscape.

### Retreat 지팡이 후속 편집 프롬프트

Use case: precise-object-edit. EDIT image 1 only in the small staff-head area held by the heroine at upper left-center. Image 2 is the EXACT authoritative staff design; image 3 shows the correct same staff in the companion story shot. Replace image 1's old staff head with that same prop: blue faceted spherical gem inside gold circular rings, a blue spear-shaped crystal at top and matching blue pointed crystals on LEFT AND RIGHT, diagonal gold star points, sculpted lower gold/blue diamond-inlay neck and dark shaft. The orb must read as a sapphire sphere with dimension, not a flat brilliant white star disk. Match image 2 silhouette in the existing slight staff tilt. Use restrained blue glow that leaves the crystals visible. All other image 1 content MUST stay unchanged: exact kneeling Dark Lord pose with one hand atop planted vertical sword and other hand clutching torso, portal behind him, hero's adult East Asian face and bun, costume and modest fully opaque leggings and closed white skirt over upper thighs, cape, feet and posture; architecture, sunset, distant beacon, framing, ground, painted ink style. Do NOT revert the lord to standing or alter the newly corrected clothing. Keep 1672x941 opaque landscape. No text or UI.


## 보스 이야기 이미지 비율 수정

2026-10-13. 게임에서 그림 전체가 가로/세로로 늘어나는 문제.

보스 그림 6장의 TextureImporter에 `nPOTScale: 1`(ToNearest)이 설정되어 있었다. 원본은 1672×941인데, 각 축을 2의 거듭제곱 크기로 바꾸면 원본 비율이 손상된다. `BossDialogueView`의 `ScaleToFit`은 이미 올바르게 설정되어 있어 화면 배치 코드는 변경하지 않았다.

수정:
- `Assets/UI/Resources/BossStory/*.png.meta` 6개: NPOT None, mipmap 끔, 압축 끔. PNG와 GUID 유지.
- `Assets/Scripts/Editor/BossStoryArtImport.cs`: 해당 폴더에 한정해 같은 임포트 정책 자동 적용. 압축 블록 크기에 맞추기 위한 크기 보정도 피한다.

Unity 공식 설정 설명: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/textureimporternpotscale


## BossPortalEscapePlate

2026-10-13 — 군주 포탈 퇴장용 배경

내장 image_gen 편집 도구 사용.
원본: Assets/UI/Resources/BossStory/Retreat_v1.png (유지)
추가: Assets/UI/Resources/BossStory/Retreat_Empty_v1.png
군주와 검을 제거한 포탈/계단 배경. 실제 UI에서는 군주 주변 패치에만 사용.
여성 영웅과 지팡이는 원본 이미지 그대로 표시.
현재 연출 (2026-10-14): 이 plate와 원본의 차이로 군주 컷아웃(Retreat_Lord_v1.png)을 굽고
컷아웃을 변형해 포탈로 끌고 들어간다.
plate는 군주 영역(BossPortalEscape.LordRect)에만 패치로 쓰인다.

Final prompt:
Use case: precise-object-edit. Create a CLEAN BACKGROUND PLATE for animating the kneeling dark armored lord out of this exact Retreat story painting. Remove ONLY the entire kneeling dark lord on the RIGHT, including his helmet, body, both hands, armor, cape, boots and his planted vertical sword. Reconstruct the purple-black portal and stone steps naturally behind where he used to be. Keep the portal fully open in the SAME exact position and shape, near-black center and violet smoky outline, now unobstructed. Preserve ALL other scene elements pixel-aligned as closely as possible: the female heroine at left, her black tights, white/navy/gold clothing and cape, her staff, architecture, steps outside the removed lord, distant beacon, sunset and ground. Do not change female heroine at all. No new people, no ghost of lord, no remaining sword, no silhouette or extra props. Same exact painterly inked fantasy illustration style and same 1672x941 opaque landscape. This plate will be shown only under the removed lord; stable surrounding geometry is critical.


## BossStoryAftermathStockingCorrection

2026-10-13 — Aftermath 복장 통일

내장 image_gen으로 양쪽 다리를 불투명한 검정 스타킹으로 덮도록 편집.
교체: Assets/UI/Resources/BossStory/Aftermath_v1.png
백업: Docs/ArtReference/BossStoryPrevious/Aftermath_before_stocking_correction.png
시각 확인: 허벅지 피부 노출 제거, 그림체 유지.
후속 지팡이 교정: 원본 지팡이 자체가 기준과 다르다는 사용자 지적 반영.
Assets/Art/Characters/FemaleHero/Props/Source/HeroStaff_Concept.png를 기준으로
연속된 금색 동심원 프레임, 입체적인 푸른 구체 및 세 방향 푸른 수정을 교정.
최종 이미지에서 원형 프레임과 검정 스타킹 유지 확인.
1672x941, 기존 .meta 유지. 코드 변경 및 컴파일/플레이 테스트 없음.
Unity: Assets > Refresh 후 Aftermath 대화 화면 확인.
Aftermath는 사건 이후 상황/여파를 뜻하므로 기존 이름 유지.

Stocking edit prompt:
Use case: precise-object-edit. Edit this Aftermath fantasy story illustration ONLY to correct the female heroine's leg clothing continuity. BOTH legs must be completely covered by solid opaque BLACK tights/leggings continuously from hips and upper thighs down through knees to inside the existing black-and-gold boots. In particular replace the visible pale bare upper thigh below the white coat on the viewer's right with opaque black fabric. No bare skin anywhere on either leg, no sheer material, no stocking-top gaps, no garters. Preserve natural leg anatomy and pose. Keep all existing white/navy/gold costume, cape, armor and boots exactly, with black fabric underneath any coat opening. Preserve absolutely everything else as closely as possible: rear-view head and bun, arms and hands, staff and its position, sunset, castle ruins, stairs, banners, purple residual mist, framing, palette and the exact detailed painterly inked storybook art style. This is a minimal clothing correction, no redesign or new objects. Same opaque landscape 1672x941 image. No text or UI.
Final staff correction prompt:
Use case: precise-object-edit. IMAGE 1 is the edit target Aftermath story painting. IMAGE 2 is the AUTHORITATIVE staff prop design reference. Correct ONLY the malformed staff in image 1 to faithfully reproduce image 2 in the same painterly ink story illustration technique. Most important: the gold head frame must form a clean CLOSED CIRCULAR concentric ring around the central blue orb, smooth continuous structurally coherent arcs with even width and a shared center; NOT a broken wonky circle, not loose disconnected golden spikes, not a warped asymmetrical tangle. Preserve reference's gold outer circular band and inner concentric ring, blue faceted SPHERICAL sapphire, three blue crystal spear tips at top and both sides relative to the staff axis, four diagonal small gold points, and gold/blue neck aligned to a straight dark shaft. Rotate the WHOLE reference prop consistently into the heroine's existing lowered staff orientation (hand at upper-left of head, head down-right). The main blue tip therefore points down-right along the shaft direction away from the hand; lateral crystals and circular geometry rotate together. Keep head center and scale close to existing, use only mild perspective so intact circular frame stays legible. No bright white star hiding gem structure. Her grip remains natural. Do not change anything else: same rear-view woman, anatomy, bun, white/navy/gold costume and cape, BOTH legs fully covered in opaque BLACK tights including upper thighs, black/gold boots, castle ruins, sunset, steps, banners, purple mist. Do not restore bare thigh from earlier versions. Match original fine ink lines, brush textures and warm muted palette, do not paste photoreal 3D render. Same 1672x941 opaque landscape, no text.


## BossStoryRetreatAnatomyCorrection

2026-10-13 — Retreat 여성 영웅 뒷모습 교정

도구: 내장 image_gen 이미지 편집.
교체: Assets/UI/Resources/BossStory/Retreat_v1.png
원본 백업: Docs/ArtReference/BossStoryPrevious/Retreat_before_anatomy_correction.png
기존 .meta 및 연결 유지. 코드 변경 없음.
시각 확인: 등 방향에 앞가슴이 표현되던 문제 수정, 상체·골반·다리 방향 정렬,
자연스러운 지팡이 잡는 팔, 기존 그림체·군주·포탈·배경 유지.
Unity 플레이 확인은 수행하지 않음. Assets > Refresh 후 Retreat 대화 장면 확인.

Final prompt:
Use case: precise-object-edit. Edit the supplied BossStory Retreat game illustration. Change ONLY the female heroine at left to correct her malformed anatomy and make her a coherent natural REAR VIEW looking toward the kneeling lord on right. Her head, shoulders, rib cage, waist, pelvis, knees and boots must all agree on the SAME rear-three-quarter orientation toward upper-right; no front-facing breastplate or breasts on a back-facing torso, no twisted waist, reversed knees or feet. Show the BACK of her costume beneath the cape, natural shoulder blades and back silhouette, normal human proportions, two anatomically attached arms and two legs in a stable relaxed vigilant standing stance. Right hand naturally holds the existing staff, elbow and wrist aligned. Keep same adult woman, brown bun, white/navy/gold fantasy outfit, cape blowing to left, opaque dark leggings and boots; coat skirt covers hips and upper thighs. Her rear silhouette may be locally redrawn to achieve correct human anatomy, not merely keep the flawed chest. Preserve original illustrated painterly inked storybook rendering, fine linework and textured brushwork exactly. Preserve everything outside heroine as closely as possible: exact kneeling black-and-gold armored lord with hand atop planted sword and other hand clutching torso, purple portal, stone steps and ruins, star banners, distant illuminated castle and beacon, sunset, framing and lighting. Keep existing blue spherical staff gem, circular gold frame and blue crystal points without redesign. No added people, text, UI or border. Same landscape 1672x941 framing, opaque image.

