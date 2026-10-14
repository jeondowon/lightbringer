# Environment 생성 프롬프트·작업 노트

이미지 옆에 있던 프롬프트와 메모를 파일 단위로 모았다. 섹션 제목은 원래 파일명이다.

## Act2_WarRavagedValley_Panorama_v1.prompt

```text
Mode: built-in image_gen (imagegen skill)
Output: Assets/Art/Environment/Act2_WarRavagedValley_Panorama_v1.png
Reference 1: Assets/Art/Environment/ForestValley_Panorama_v2.png
Reference 2: Assets/UI/Art/CampaignAtlas_Act2_v2.png
Scope: Generate Act II background image for stages 9-16. Runtime assignment is separate.

Use case: stylized-concept.
Asset: Act II distant battlefield background for Lightbringer, a premium stylized fantasy Unity game. Generate ONE production 360-degree equirectangular panorama, 2:1 landscape, ideally 4096x2048, full 360 horizontal by 180 vertical, seamless left-right wrap.
Reference image 1: Act I battlefield panorama. Match its painterly rendering, atmospheric depth, panoramic projection, horizon layout, cloud softness and distant scale. This is style/composition guidance.
Reference image 2: Act II campaign atlas, strict reference for regional geography, architecture and degree of devastation. Translate its world into a surrounding horizon-level panoramic environment, NOT an overhead map.
Scene: a war-ravaged inland river valley, exhausted ash-brown earth, weathered grey rocky hills, sparse dying olive-grey woodland mixed with leafless black trees, collapsed watchtowers and abandoned settlements. A winding muted slate-blue river and distant old arched stone bridges weave through the broad valleys. A small ruined columned sanctuary and broken ancient hero monument sit far away on one low ridge; other directions have small palisaded supply outposts and remote ruined masonry. ONE still-standing warlord fortress with heavy medieval grey stone walls and square towers on a distant rocky ridge to the right of center, echoing the fortress in reference 2. Buildings are modest in angular size, distant, integrated into terrain, never foreground objects. Vary the skyline naturally in all directions. Some sparse surviving olive vegetation, restrained soot haze and low cold river mist. Clearly more physically devastated than Act I, but not yet the completely dead supernatural realm of Act III.
Style: same sophisticated softly painterly matte fantasy environment as reference 1, dimensional believable rock masses and subdued detailed terrain, no heavy engraved map outlines, not photographic, not low-poly. Muted slate blue, ash brown, weathered grey and faded olive. Heavy layered overcast sky with faint soft ivory light leaking through a few cloud gaps; dimmer and less hopeful than reference 1 but readable midtones, no crushed blacks. Low contrast distant scenery to support readable foreground gameplay.
Projection/layout: horizon at exactly half image height, distant skyline mostly in 38%-53% vertical band. Top half is broad sky/clouds smoothly simplifying toward uniform low-detail slate blue zenith. Valley extends below horizon to 70%; bottom 30% dissolves into gently textured grey-brown earth haze with no distinct objects, suitable for nadir hidden by game terrain. Entire scene is distant, no nearby trees, rocks, large monuments, roads coming towards viewer or foreground framing. Left/right edges match in cloud values and misted low hill contours, no edge landmarks; continuous surrounding world.
No ocean, snow, ice, desert, lava, giant alpine peaks, magical eyes, demon faces, supernatural black tendrils, neon glow, giant gothic final citadel, characters, UI, lettering, markers, routes overlay, border, watermark, visible sun disc. Output ONLY one complete panoramic background image.

```

## Act2_WarRavagedValley_Panorama_v2.prompt

```text
Mode: built-in image_gen (imagegen skill)
Output: Assets/Art/Environment/Act2_WarRavagedValley_Panorama_v2.png
Edit target: Assets/Art/Environment/Act2_WarRavagedValley_Panorama_v1.png
Scope: Revised image only; runtime assignment unchanged.

Use case: lighting-weather.
Edit the supplied Lightbringer ACT II battlefield panorama. Preserve exactly the 2:1 panoramic framing, surrounding-world projection, terrain layout, small distant ruined sanctuary and monument, winding rivers, bridges, dead woodland, distant warlord fortress right of center, painterly fantasy rendering and all landmark scale/positions. Make the atmosphere MUCH DARKER and MUCH FOGGIER according to this correction.
Completely cover ALL bright sky openings with a thick unbroken ceiling of dark charcoal and cold slate storm clouds. Remove ALL visible sun, sunlight shafts, god rays, warm gold/ivory glow, bright horizon stripe and sunlit ground. There must be no conspicuous light source anywhere. Very faint diffuse cold ambient illumination only, a deeply gloomy oppressive overcast world. The upper-left sky must be dark heavy cloud, not a bright patch. Uniformly subdued cold lighting around the entire panorama, no dramatic luminous silver lining.
Fill the valley with abundant thick layered cold grey-blue fog banks, rolling among hills, partly veiling ruins, bridges and dead forests, obscuring most distant valley detail. Fortress lower walls disappear into mist while its upper silhouette remains identifiable. Distant hills recede through several dense fog layers. Fog should feel dark and damp, not bright white glowing clouds. Preserve enough low midtone separation for terrain and fortress silhouettes to remain readable behind a game battlefield. Substantially more obscuration than the input, not merely a darker color filter.
Narrative progression: this is ACT II, heavily darkened and fogbound, with small surviving faded olive vegetation. ACT III later will be the most extreme darkness and near-total fog engulfment, so this Act II still has recognizable landmark silhouettes and moderate visibility on nearer hills. Do not render Act III now.
Production texture: one complete seamless 360-degree equirectangular 2:1 panorama, ideally 4096x2048; retain horizon and skyline placement of input, matching left/right edge colors and contours, low-detail zenith and bottom earth-fog fade. No new foreground objects, no characters, no eyes in sky, magic, lightning, fire glow, snow, text, UI, border or watermark. Output only the revised panorama.

```

## Act3_DarkCitadel_Panorama_v1.prompt

```text
Mode: built-in image_gen (imagegen skill)
Output: Assets/Art/Environment/Act3_DarkCitadel_Panorama_v1.png
Reference 1: Assets/Art/Environment/Act2_WarRavagedValley_Panorama_v2.png
Reference 2: Assets/UI/Art/CampaignAtlas_Act3_v2.png
Scope: Image generation only; runtime assignment unchanged.

Use case: stylized-concept.
Asset: Lightbringer ACT III distant battlefield environment panorama. Create ONE full 360-degree equirectangular 2:1 landscape texture, ideally 4096x2048, intended to surround a 3D battlefield.
Reference 1 is approved ACT II panorama: match its premium painterly matte rendering, distant environment scale and projection, but ACT III must be unmistakably DARKER and MUCH MORE densely fogbound.
Reference 2 is ACT III campaign atlas: use its utterly devastated inland basin, dead forests, shattered terrain, rivers and final gothic citadel as the regional content reference. Translate that geography into a horizon-level surrounding environment, NOT a top-down map.
This is the darkest final region of the three acts. Unbroken thick charcoal-black storm clouds smother the entire sky. NO sunlight, NO god rays, NO sun, NO bright horizon band, NO glowing cloud gaps, NO warm light. Extremely subdued cold ambient illumination, dark charcoal and near-black slate blue with very restrained grey midtones. Even darker than reference 1, but keep subtle tonal separation so the asset is not featureless black.
A vast devastated rocky basin, skeletal black dead trees and stumps, shattered villages reduced to walls, crumbling watchtowers, bare ash-grey ridges. No living green vegetation. Barely glimpsed dark winding river and broken ancient bridges in the valley. Dense rolling banks of dark cold fog engulf almost all lowlands, repeatedly veil hills, obscure ruins and distant mountains. Far MORE fog coverage than reference 1, thick layered masses of smoke-like mist crawling up the slopes and wrapping ruined structures, merging with low storm ceiling. Not luminous white fog. Clear sense of oppressive near-total engulfment, with intermittent ruined ridge silhouettes.
One distant final citadel right of center, derived specifically from reference 2: tall narrow central gothic spire, clustering pointed subsidiary towers, tiered concentric charcoal stone walls, rocky plateau and arched stone approach viaduct. Upper spires emerge from very dense fog, lower fortress and cliff partly swallowed. It is recognizably grander and more sinister than the Act II square-towered fortress, but remains distant and integrated into the panorama, not a foreground castle portrait. Above and directly behind this citadel, preserve the map's ominous small PAIR of narrow dim crimson eyes embedded within the cloud darkness, extremely restrained, barely discernible, no face outline, nose, mouth, head, horns, body or solid eyeballs. Red stays confined to these tiny eyes, no red illumination across sky or terrain.
Layout: same surrounding-world panoramic format as reference 1, distant skyline mostly around 40%-53% height; upper half broad dark layered sky simplifying toward low-detail zenith. Below horizon, layered distant devastated landscape and fog to 70%, bottom 30% fades to nearly uniform dark slate earth fog with no identifiable foreground objects. Match left/right edges in clouds, fog and ridge values for continuous 360 wrap, keep landmarks away from seam. Low contrast distant scenery suitable behind readable gameplay.
Same soft painterly fantasy game material treatment as reference 1, not photographic, not map engraving, not cartoon outlines or low-poly. No nearby objects, no people, no text, UI, markers, border, watermark, lightning, lava, fire, snow or ocean. Deliver only one complete panorama.

```

## Act3_DarkCitadel_Panorama_v2.prompt

```text
Mode: built-in image_gen (imagegen skill)
Output: Assets/Art/Environment/Act3_DarkCitadel_Panorama_v2.png
Edit target: Assets/Art/Environment/Act3_DarkCitadel_Panorama_v1.png
Motif reference: Assets/UI/Prologue/Prologue_00C_BirthOfDarkness_Storybook_v1.png
Implementation reference: Assets/Scripts/UI/SpriteFx.cs, DarkInkFlow; Assets/Scripts/UI/PrologueEffects.cs
Scope: Static background image only; runtime assignment unchanged.

Use case: precise-object-edit.
Edit image 1, the approved Act III panoramic battlefield background. Image 2 is ONLY a motif reference for the long black-violet ink streams in the prologue illustration; do not copy its people, village, perspective, paper texture or illustration style.
Keep image 1's EXACT 2:1 framing, all terrain and castle geometry, landmark placement, distant scale, dark exposure, heavy storm clouds, dense grey fog, muted painterly dimensional environment rendering. User approves the overall atmosphere.
CHANGE 1: completely REMOVE the pair of tiny red eyes above the castle. Fill this area seamlessly with ordinary dark cloud. No demon king appearance of any kind: no eyes, face, body, humanoid or creature silhouette anywhere.
CHANGE 2: add visibly recognizable long flowing strands of dark PURPLE MIST through the existing landscape and lower storm clouds, matching the prologue's 'DarkInkFlow' motif. They have ink-black charcoal cores with muted smoky violet edges, irregular varying thickness, gently curling directional flow, splitting and rejoining branches, tapering ends that disperse into wisps. Translate the flat ink motif into translucent volumetric smoky ribbons with depth appropriate to this environment. Several long distinct flows snake through the distant valleys, coil around hills and ruined towers, and gather around the fortress slopes; a few thinner ascending wisps connect to low cloud banks around the castle. Existing grey fog remains, with violet strands clearly distinguishable within it. Make the desaturated purple readable enough to notice at panorama scale, but retain the gloomy low-light exposure.
These must feel like evil spreading as ink in air: organic drifting mist streams with frayed soft edges, not solid tentacles, ropes, pipes, giant worms, cracks in terrain, laser beams, lightning, luminous neon lines, aurora curtains or uniform purple color grading. Not enormous foreground ribbons obscuring the world. No new objects or characters. No bright bloom or light sources, no sunlight, no red color. Castle silhouette and geography must stay readable and unchanged.
Preserve 360 equirectangular wrap intentions and matching left/right edge tones. Keep low-detail zenith and bottom fog fade. Output ONE edited panorama only, no text, interface, borders or watermark.

```

## Act3_DarkCitadel_Panorama_v3.prompt

```text
Mode: built-in image_gen (imagegen skill)
Output: Assets/Art/Environment/Act3_DarkCitadel_Panorama_v3.png
Initial reference: Assets/Art/Environment/Act3_DarkCitadel_Panorama_v2.png
Scope: Static image only; runtime assignment unchanged.

Initial pass:
Use case: style-transfer.
Edit the supplied ACT III Lightbringer battlefield panorama. User correction: it looks too cartoon-like; make it substantially more realistic and use much LESS purple mist.
Preserve the exact 2:1 panoramic composition, horizon and distant scale, gothic citadel position and architecture, geography, rivers, ruined towers, dead vegetation, heavy darkness and abundant ordinary grey fog. No demon, eyes, face or character anywhere.
Transform the rendering into a high-end realistic cinematic 3D fantasy environment: physically convincing weathered stone masonry, natural irregular fractured rock strata, believable small skeletal trees, realistic scale-dependent detail and atmospheric perspective. Smooth physically grounded shading, diffuse overcast ambient illumination, subtle material roughness. Remove drawn black outlines, etched contour marks, repetitive stylized rock scallops, comic textures, hatching, gouache brush marks, and miniature toy-like terrain. Clouds must become naturally turbulent volumetric storm clouds with broad flowing shapes and fine wisps, NOT repetitive painted scales. Fog should have real volumetric density and soft depth-dependent occlusion, burying lowlands and parts of the castle base. Maintain dark subdued charcoal/slate tones; no sunlight, sunbeams, bright sky gaps, silver lining, or bright horizon. Realistic depth without making the image brighter. High-end fantasy game environment realism, not a flat illustration.
REMOVE approximately 85-90 percent of existing violet streams. Entire sky should have ordinary dark storm clouds, no purple lines crossing it. Most valleys should have neutral cold grey fog. Keep ONLY TWO OR THREE sparse subtle elongated wisps of muted smoky violet-black mist integrated into distant low valleys and around the base of the citadel. Barely noticeable secondary accents occupying a very small portion of the image. Each wisp should have soft feathered dissipating edges and slight natural curling variation in thickness, dark charcoal interior with a faint desaturated violet tint. No hard borders, glowing purple rims, ropes, solid tentacles, branching purple networks, neon, or violet color wash over the whole landscape.
Deliver a cohesive natural-looking dark fogbound landscape with realistic stone, atmospheric layers, and a believable threatening castle silhouette. Low contrast distant backdrop suitable behind 3D gameplay. Preserve left-right panoramic edge compatibility, low-detail top zenith and bottom fog fade. Output ONE complete 2:1 panorama, ideally 4096x2048. No text, UI, border, watermark or new foreground objects.

Final refinement applied to initial pass output:
Re-render this environment from scratch as a REALISTIC CINEMATIC 3D ENVIRONMENT, using the reference ONLY as a layout blockout. Do not retain or trace its illustration textures. The main requested change is realistic rendering, not a small filter. Current image is too hand-drawn. Remove ALL repeated scalloped cloud shapes, outlined stone scales, engraved contour lines, painted patterning and stylized tiny hills.
Keep the wide 2:1 surrounding panorama, dark foggy devastated valley and distant tall gothic castle on a plateau right of center, sparse ruins, dead forest, winding river and stone viaducts. Natural eroded sedimentary rock, irregular massive geological forms, rough weathered masonry with physically accurate scale, natural dead branches. Render with physically based materials and cinematic volumetric lighting, convincing atmospheric perspective like a realistic high-end 3D dark fantasy game environment. Clouds should be broad smooth turbulent volumes of real water vapor, with NO mosaic texture, no scales, no outlined lobes. Grey fog in deep layers obscures much of the lowlands.
Extremely dark sunless overcast, subdued slate charcoal palette, no bright gaps, rays, rimlight or illuminated horizon. Ordinary neutral grey fog dominates. ONLY two very faint desaturated violet-black mist wisps among distant low valleys near the castle; sparse, translucent, naturally dissipating, no drawn lines, no glowing outline. No purple in sky. No demon or eyes anywhere. No characters or foreground objects. No illustration, concept sketch, cartoon, ink, hatch, brushstrokes, painting, toy miniature appearance. Preserve low-detail dark bottom fog fade and seamless left-right 360 panoramic intentions. One 2:1 image only.

```

## Act3_DarkCitadel_Panorama_v4.prompt

```text
Mode: built-in image_gen (imagegen skill)
Output: Assets/Art/Environment/Act3_DarkCitadel_Panorama_v4.png
Reference: Assets/Art/Environment/Act3_DarkCitadel_Panorama_v3.png
Scope: Static background revision only; runtime assignment unchanged.

Use case: precise-object-edit.
Rework this Lightbringer Act III 2:1 panoramic battlefield background according to a major geography correction: TOO MANY HILLS, NOT ENOUGH RUINS. Replace the entire repeated mound-and-mountain landscape with a BROAD LOW-LYING DEVASTATED MEDIEVAL CITY AND ITS RUINED OUTSKIRTS. This must be a strongly visible composition change, not just adding a few towers to hills.
Remove at least 80% of all small rounded hills and most jagged mountain peaks. A low distant broken ridge may remain on the far horizon only. Across the middle and lower-middle of the panorama, an extensive relatively FLAT ruined urban plain dominates: roofless stone houses with exposed rooms, collapsed town blocks, heaps of broken masonry and roof timbers, shattered gatehouses, long breached defensive walls, broken courtyards, ruined cathedral nave with fallen arches, irregular street grids, damaged squares and an old canal with broken arched bridges. Distinct architectural destruction should read immediately over the whole landscape. No uniform rows of intact buildings, no repeating lonely watchtower on every hill. Ruins vary in silhouette, scale and damage; clustered wrecked city districts separated by open rubble plains and broad old roads. Sparse skeletal trees are secondary. Everything distant, no huge foreground object.
Preserve the recognizable tall narrow gothic FINAL CITADEL right of center, still standing above the dead city on the ONE main low rocky escarpment, stepped walls and stone approach viaduct. Citadel is a distinct distant destination, not a giant foreground building. Panoramic horizon-level view across the ruins, NOT top-down atlas, no miniature diorama.
Keep the darkest Act III mood: completely sunless charcoal storm sky, thick cold grey volumetric fog rolling through ruined streets and broken walls, ruined silhouettes emerging from multiple fog layers. Ensure many destroyed architectural forms remain visible enough to identify the city, not hidden entirely by fog. No sunlight, bright cloud gaps or rays. TWO faint localized wisps of desaturated violet-black mist among distant ruined districts near the citadel, very sparse and soft, no purple lines across sky. No demon king, no eyes, faces or creatures anywhere.
More realistic cinematic high-end 3D fantasy environment rendering: physically convincing weathered masonry and shattered construction, irregular rubble and natural volumetric cloud forms, diffuse cold ambient shading, grounded scale and atmospheric perspective. No cartoon outlines, painted stone scallops, ink contours, etching, brush patterns or repetitive stylized cloud scales.
Preserve 2:1 360 equirectangular panorama intentions and left/right wrap compatibility; broad sky upper half, distant city skyline near half-height, legible distant ruined city in middle/lower-middle, bottom 20-25% fades into low-detail dark grey fog for hidden nadir. Muted slate/charcoal low contrast background for foreground gameplay. One complete panorama only, no text, labels, UI, markers, border or watermark.

```

## Act3_DarkCitadel_Panorama_v5.prompt

```text
Mode: built-in image_gen (imagegen skill)
Output: Assets/Art/Environment/Act3_DarkCitadel_Panorama_v5.png
Reference: Assets/Art/Environment/Act3_DarkCitadel_Panorama_v4.png (architecture and mood only)
Scope: Ground-eye-level skybox image revision only; runtime assignment unchanged.
Validation: Visually checked ground-level ruined skyline and lower fog fade. Unity spherical projection, seam and pole behavior still require in-engine inspection.

Use case: stylized-concept.
Create a NEW ground-level SKYBOX PANORAMA for Lightbringer Act III. Supplied image is ONLY a reference for ruined architecture, castle identity and dark mood. Its elevated viewpoint is WRONG and MUST NOT be copied. Reconstruct the scene from ground eye level, camera 1.7 metres above a flat battlefield, looking perfectly horizontally toward DISTANT ruins. No overhead view, no bird's-eye perspective, no drone, no mountain overlook, no view down into a city.
OUTPUT: ONE 2:1 full 360x180 equirectangular spherical environment texture, ideally 4096x2048, suitable for a Unity sky shader. Horizon at EXACTLY 50% image height. Camera has zero pitch, level horizon.
CRITICAL COMPOSITION: Almost ALL distant ruined architecture occupies a thin horizontal band from 43% to 54% image height. Main gothic citadel slightly right of center, its spire can rise to 35%-38% image height but base is at 50%, behind fog, NOT on a tall hill. Visible facades and side elevations of ruined walls, shattered cathedral arches, broken gatehouses and far-off collapsed buildings seen across flat ground. NO visible roof plans, street grids, river surfaces, ground planes receding from a high viewpoint, miniature hills or city spread across lower half. You cannot see down into the ruins. Most building bases are submerged in ground fog; overlapping silhouettes create atmospheric depth. Architecture should read as a destroyed city skyline, not a range of mountains. Ground is approximately flat with only a low foundation escarpment beneath the distant final castle. Few irregular distant roofless walls and tower stumps, all appropriately small angular size for distant scenery. No foreground props.
UPPER HALF: a vast oppressive sunless ceiling of charcoal storm clouds with realistic broad smooth volumetric forms. No repetitive cloud scales, no outlined cloud lobes. Sky is completely overcast, no sun, warm light, sun shafts, bright cloud openings or luminous horizon. Clouds simplify progressively to a low-detail uniform dark zenith at top edge.
LOWER HALF: predominantly softly graded dark slate-grey ground fog. All recognizable objects fade completely by 58%-60% image height. Bottom 40% is low-detail uniform dark fog, NO ruins, ground detail or objects; it will be hidden below real 3D game terrain. Do not try to fill this region with scenery.
Overall style: realistic cinematic 3D dark fantasy environment, natural weathered stone, convincing volumetric fog, restrained desaturated charcoal and slate, deep darkness with readable ruined silhouettes. No cartoon, ink outlines, engraved texture, painted repetitive stone patterns or toy diorama aesthetic. Match the castle identity and oppressive ruined-world mood of supplied image while correcting viewpoint completely.
Only TWO faint localized violet-black smoky wisps among distant ruins near horizon, soft dispersed edges, subtle enough to be secondary to ordinary grey fog. No purple sky, bright trails or glowing neon. Absolutely no demon king, eyes, faces or characters. No text, UI, border or watermark.
Seamless left/right boundary with matching fog, cloud values and skyline elevations, no landmark at either edge. Equirectangular latitude-longitude projection, not a wide perspective poster. The central requirement is a true ground-eye-height horizon view for a skybox; do not preserve the reference's elevated camera.

```

## Act3_DarkCitadel_Panorama_v6.prompt

```text
Mode: built-in image_gen (imagegen skill)
Output: Assets/Art/Environment/Act3_DarkCitadel_Panorama_v6.png
Primary reference: Docs/ArtReference/Environment/ForestValley_Panorama_v2_candidate.png
Scope: Static background revision only; runtime assignment unchanged.
Direction: Follow user-selected candidate perspective, landscape distribution and bottom fog transition. Supersedes v5 thin-skyline composition.

Use case: stylized-concept.
Use the supplied ForestValley_Panorama_v2_candidate image as the PRIMARY STRICT COMPOSITION AND PERSPECTIVE REFERENCE. Create its ACT III ruined dark kingdom counterpart for Lightbringer. Match this exact reference's 2:1 framing, camera viewpoint, visual depth, skyline height, size of distant buildings, sky-to-land ratio, layered middle/lower landscape and bottom fog transition. The user explicitly selected THIS reference. Do not flatten scenery into a thin horizontal skyline with an empty lower half. Do not impose a new ground-eye-level viewpoint. Retain the reference's broad sweeping environmental panorama and multiple receding spatial layers.
Preserve the layout rhythm: expansive cloud sky to roughly 42% image height; far mountain silhouettes around 38-47%; diverse detailed landscape from roughly 45% through 80%; soft featureless fog only towards bottom 15-20%. Distant main castle right of center near the reference castle's exact position and comparable angular size. Match these visual proportions carefully.
Change the region into the devastated ACT III kingdom. Replace most lush wooded rolling mounds with broader low-lying plains of DESTROYED MEDIEVAL SETTLEMENTS: distinguishable roofless house clusters, collapsed stone walls, broken towers and gatehouses, ruined cathedral arches, abandoned roads, fractured old stone bridges and rubble courtyards. Rich readable ruined architecture spread across the middle and lower-middle, taking the place of the forest masses. Less repetitive hills, fewer peaks; preserve a low far mountain backdrop and one modest rocky ridge under the main citadel. Keep natural broad valley depth like the reference rather than a dense uniform toy-city grid. Sparse skeletal dead trees mixed with wrecked settlements; no living green woodland. The main destination is a tall narrow gothic citadel with clustered pointed subsidiary towers and tiered charcoal walls, standing beyond the ruined kingdom, distant and partly veiled in fog.
Atmosphere: the darkest final region. Block ALL sunshine and bright cloud openings from the reference with heavy dark cold storm cloud layers. No rays, warm glow, sun or bright horizon strip. Dark slate and charcoal with readable low-midtones. Plenty of layered cold grey volumetric fog flowing between ruined districts, partly hiding bases, softly merging lower image into fog just as the reference does. Preserve enough architectural silhouettes and spatial layers that ruins remain identifiable. Only TWO sparse faint violet-black flowing mist wisps threading distant ruins near the citadel, nonluminous desaturated purple, very small fraction of the scene, no purple network in sky. No demon king appearance, no eyes, faces or characters.
Rendering: more natural realistic high-end 3D fantasy environment surfaces and atmospheric depth, soft volumetric clouds, believable weathered masonry, irregular realistic rubble, subtle materials. Avoid cartoon black outlines, engraved map hatching, repeated scalloped rock shapes and comic textures. Reference supplies composition and scale, not a requirement to exaggerate its painterly marks. Keep restrained premium fantasy character.
One 2:1 panoramic background image, ideally 4096x2048. Maintain matching left-right edge values for skybox wrap and unobtrusive bottom fog. No text, UI, frames, watermarks, foreground characters or large foreground props. Most important: faithfully match the supplied candidate's landscape distribution and camera perspective, while making the landscape predominantly ruined, sunless, foggy Act III.

```

## BattlefieldBackdropSetup

```text
전투 원경 설정

현재 1~8스테이지:
Assets/Art/Environment/ForestValleyBackdrop.asset
이미지: Assets/Art/Environment/ForestValley_Panorama_v1.png

Unity Project 창에서 ForestValleyBackdrop을 선택한다.
Panorama: 360도 파노라마(equirectangular), 가로:세로 2:1 이미지.
Rotation: 성채와 산맥이 나타나는 방향.
Exposure: 이미지 밝기.
Horizon Offset: 수평선 높이 보정.
Cloud Motion: 산맥 위 구름에만 적용하는 느린 움직임.
Fog Color / Start / End: 3D 지형과 원경 사이의 안개.
Forest: 해당 구간에서 사용할 기존 3D 숲 설정. 비우면 원래 스테이지별 숲 설정을 사용한다.

9스테이지 이후 새 지역:
1. ForestValleyBackdrop.asset을 복제한다.
2. First Stage = 9, Last Stage = 새 지역의 마지막 스테이지로 바꾼다.
3. Panorama에 새 2:1 원경 이미지를 지정하고 안개 색을 맞춘다.
4. Assets/Art/ArtStyleLibrary.asset의 Backdrops 목록에 새 설정을 추가한다.
범위가 겹치면 목록에서 먼저 일치하는 설정이 적용된다. 일치하는 항목이 없으면 기존 그라데이션 하늘을 사용한다.
이미지 교체는 먼 풍경만 바꾼다. 설원·사막 등의 새 지역에는 별도 지면 재질/식생도 준비해야 한다.

검증 및 미리보기:
Lightbringer/Art/Capture Forest Ground Preview: 1스테이지
Lightbringer/Art/Capture Battlefield Preview: 8스테이지
Docs/ArtPass/Preview의 Hero, HorizonForward, HorizonRear, HorizonSeam 이미지를 확인한다.
원경은 카메라 이동 시 먼 거리로 유지되고, 주변 지형과 숲은 실제 3D로 움직인다.
구름만 매우 느리게 움직이며 산맥과 성채는 회전하지 않는다.
기존 전투 경로, 이동 가능한 평지, 충돌 판정은 유지한다.

```

## BattlefieldExpansionNotes

```text
Battlefield expansion and woodland density — 2026-10-13

Changes
- Stronghold separation: 60m -> 100m (allied z=-22, enemy z=78).
- Routes extended to z=34/64/78; enemy routes follow the same corridor back.
- Terrain flat area extended to z=-32..92, x=-34..34; greybox floor extended too.
- First beacon moved to (-13,28), beside the new midfield; sanctuary clearances retained.
- More grass patches, shrubs and woodland islands; irregular tree/understory belts frame the boundary.
- Hero movement stays one metre inside the play rectangle and slides along its edges.
- LateUpdate recovers enabled-controller knockback outside the field and falls below y=-3.
- Disabled controllers remain under scripted sequence control. Standalone greybox movement is unchanged.
- Longer travel changes the time until armies meet; wave cadence and troop speeds were not rebalanced.

Validation
- Unity compilation and 443 Greybox component/physics checks passed.
- Added checks: 100m separation, boundary installation, all four sides, corner, diagonal sliding,
  transformed battlefield root, external displacement, fall recovery and disabled controller.
- Stage 1: 158 trees, 9,826 grass tufts; stage 8: 136 trees, 7,451 grass tufts.
- Stage 1/8 previews: routes clear, terrain flat on waypoints, supported shaders and rooted trees passed.
- Beacon sanctuary/ignition/replay checks passed at its new location.
- Full CampaignPlay run stopped at existing SoundEffectsValidation.cs check:
  "SFX slider maximum exposes the full normalized level on every voice".
  It is not reported as a full runtime pass. Forest runtime checks before that point passed.
- Dense foliage retains patch distance culling/shared instanced materials; shipping-build FPS not measured.

Art assessment
- This pass improves scale and vegetation density; it does not reproduce Black Desert's rendering quality.
- Main remaining gaps: broad flat terrain, repeated stylized tree shapes, weak local material variation,
  and insufficient separation of sunlit surfaces/shade. Prioritize lighting and material cohesion next.
- Keep the current stylized fantasy characters; any wholesale realistic asset replacement is a separate art pass.

Files added
Assets/Scripts/Core/BattlefieldLayout.cs (+ Unity .meta)
Assets/Scripts/Player/BattlefieldBoundary.cs (+ Unity .meta)
Assets/Scripts/Editor/Validation/BattlefieldBoundaryValidation.cs (+ Unity .meta)
Docs/ArtReference/Environment/BattlefieldExpansionNotes.txt

Files edited (this pass only; other pre-existing workspace changes preserved)
Assets/Scripts/Core/PrototypeBattleBuilder.cs
Assets/Scripts/Player/PlayerMovement.cs
Assets/Scripts/Visuals/Environment/BattlefieldEnvironment.cs
Assets/Scripts/Visuals/Environment/BeaconSanctuary.cs
Assets/Scripts/Visuals/Environment/ForestEnvironmentDressing.cs
Assets/Scripts/Editor/Validation/GreyboxValidation.cs
Assets/Scripts/Editor/Validation/CampaignIntegrationValidation.cs
Assets/Scripts/Editor/ArtAnimationPreview.cs
Generated validation reports/previews under Docs/Validation and Docs/ArtPass/Preview.

Unity Editor
No manual component/prefab setup needed. Stop Play, then start a new campaign battle.
Inspect stage 1 from the hero camera and walk against all four woodland edges.
Inspect stage 8 outer lanes and confirm crowds remain readable with denser foliage.

```

## ForestLightingNotes

```text
Forest lighting and surface pass — 2026-10-13

Changes
- Forest terrain now separates shadowed direct sunlight from ambient sky illumination.
  _NaturalLight opts the two forest materials into this model; other terrain materials default to the existing ramp.
- _SoilBreakup blends irregular worn soil and darker damp patches into the grass floor.
- Normal-map strength fades at distance to reduce fine-detail shimmer.
- Sun angle 38 -> 32 degrees; shadow strength .75 -> .9; less saturated, darker ambient light.
- Reduced blue shadow/warm highlight grading, saturation 14 -> 5, bloom .45 -> .24.
- Forest fog begins at 85m instead of 65m, ends at 235m instead of 205m for the extended battlefield.
- Existing characters, routes, vegetation placement, beacon presentation and boundary logic retained.

Validation
- Unity Greybox component checks passed (443).
- Stage 1 and 8 environment/surface previews passed: shader support/compilation, clear paths, root contact.
- Beacon ignition, result timing and replay checks passed.
- Hero and midfield images visually inspected. This was a visual pass; no new full runtime or FPS claim.
- Existing vegetation shader instancing warnings still appear in the Editor log.

Files modified
Assets/Art/Shaders/LightbringerTerrain.shader
Assets/Art/Environment/Forest/StageOneGladeGround.mat
Assets/Art/Environment/Forest/ForestTerrain.mat
Assets/Art/ArtStyleLibrary.asset
Assets/Art/Environment/ForestValleyBackdrop.asset
Assets/Art/LB_BattlePostProcess_v2.asset
Assets/Scripts/Visuals/Environment/BattlefieldStyling.cs
Assets/Scripts/Editor/Environment/ForestSurfaceSetup.cs

New file
Docs/ArtReference/Environment/ForestLightingNotes.txt

Previews
Docs/ArtPass/Preview/Lighting_Before_Hero.png
Docs/ArtPass/Preview/Lighting_Before_Midfield.png
Docs/ArtPass/Preview/ForestGround_Hero.png
Docs/ArtPass/Preview/ForestGround_Midfield.png
Docs/ArtPass/Preview/Battlefield_Hero.png

Unity Editor
Stop Play and start a new campaign battle. No manual component assignment required.
Review stage 1 shade beside the woods and the illuminated road, then stage 8 outer lanes.
The current tree shapes and flat combat terrain remain the main limitations compared with realistic reference games.

```

## ForestSurfaceNotes

```text
숲 지면과 뿌리 접지 개선 (2026-10-13)

변경
- 나무 원본의 가장 낮은 뿌리 끝과 실제 줄기 밑동 높이가 다르므로, 한 점이나 고정 반경만으로 배치하지 않는다.
- 큰 나무 116개, 어린 나무 4개의 하단 지점을 prefab에 저장한다.
- 인스턴스의 회전과 크기를 적용한 각 지점에서 실제 지형 삼각형의 높이를 구하고, 뿌리가 지면 안으로 약 6cm 들어가도록 배치한다.
- 런타임 Mesh Read/Write나 프레임마다 Raycast는 필요 없다. 생성할 때 한 번 계산한다.
- 숲 진영과 성역 바닥에 암석 색상·노멀·AO 텍스처를 적용한다.
- 기존의 굵고 반복적인 사각 줄눈을 제거하고, 닳은 암석과 흙을 불규칙하게 섞는다.
- 잔디·흙 텍스처에 큰 크기의 패턴을 함께 섞어 반복감을 줄인다.
- 텍스처 지면의 빛 반응을 부드러운 확산광과 약한 거친 표면 반사로 보완한다.
- 암석 텍스처에 밉맵, trilinear, anisotropy 8을 적용한다.
- ForestTerrain과 StageOneGladeGround 양쪽에 적용한다. 병사 경로와 지형 충돌은 유지한다.
- 추가 색감 조정: 흙·풀 지면의 색 배율을 약 25~30% 낮추고 풀을 짙은 올리브색으로 조정한다. 지면 그림자도 더 깊게 한다.
- 전장 길 위의 발광선은 생성하지 않는다. 전선 선택, 미니맵 경로와 전선별 병력 표시는 유지한다.

검증
- ForestGround_SurfaceChecks.txt: 1스테이지 나무·어린 나무 118개, 접지 지점 9,656개 검사.
- Battlefield_SurfaceChecks.txt: 8스테이지 나무·어린 나무 104개, 접지 지점 8,704개 검사.
- 계산한 높이를 실제 지형 MeshCollider Raycast와 비교한다.
- 경로 여유 공간, 지형 평탄성, 셰이더 컴파일, 봉화 승리 연출도 기존 검사로 확인한다.
- 수동 플레이 및 실제 전투 프레임 성능 측정은 포함하지 않는다.

Unity에서 확인
1. Play 종료 후 컴파일을 기다리고 스테이지 1에 다시 진입한다.
2. 숲 경사면의 나무 밑동, 아군 성문 앞 지면, 봉화 성역의 바닥을 확인한다.
3. 별도 Inspector 연결은 필요 없다.
4. 재설정: Lightbringer/Art/Improve Forest Surfaces and Root Contact
5. 미리보기와 검사: Lightbringer/Art/Capture Forest Ground Preview
   출력 폴더: Docs/ArtPass/Preview
   ForestGround_Hero.png / ForestGround_RootContact.png / ForestGround_StoneCloseup.png

추가 파일 (스크립트별 Unity meta 포함)
Assets/Scripts/Visuals/Environment/ForestGroundContact.cs
Assets/Scripts/Editor/Environment/ForestSurfaceSetup.cs
Docs/ArtReference/Environment/ForestSurfaceNotes.txt

수정 파일
Assets/Scripts/Visuals/Environment/ForestEnvironmentDressing.cs
Assets/Scripts/UI/BattlefieldReadability.cs
Assets/Scripts/Core/PrototypeBattleBuilder.cs
Assets/Scripts/Editor/ArtAnimationPreview.cs
Assets/Art/Shaders/LightbringerTerrain.shader
Assets/Art/Environment/Forest/ForestTree.prefab
Assets/Art/Environment/Forest/ForestSapling.prefab
Assets/Art/Environment/Forest/ForestTerrain.mat
Assets/Art/Environment/Forest/StageOneGladeGround.mat
Assets/TriForge Assets/Fantasy Worlds - DEMO Content/Textures/Terrain/T_fwOF_RockSurf_02_BC.png.meta
Assets/TriForge Assets/Fantasy Worlds - DEMO Content/Textures/Terrain/T_fwOF_RockSurf_02_N.png.meta
Assets/TriForge Assets/Fantasy Worlds - DEMO Content/Textures/Terrain/T_fwOF_RockSurf_02_Mask.png.meta

```

## ForestValley_Panorama_v1.prompt

```text
Mode: built-in image_gen
Output: Assets/Art/Environment/ForestValley_Panorama_v1.png
Reference: Title_LastLight_Storybook_v2.png (world / palette only)

Use case: stylized-concept.
Asset: production 360-degree equirectangular panoramic environment texture for the distant background of a third-person premium stylized fantasy Unity game. Generate a 2:1 landscape image, preferably 4096x2048, seamless left-right wrap, latitude-longitude spherical projection, full 360 horizontal by 180 vertical. THIS IS A SKYBOX TEXTURE, not a framed cinematic composition, not a perspective shot and not a planet.
Reference image is only the WORLD AND PALETTE inspiration: ancient fantasy ruins, blue-grey mountain layers, atmospheric forested valleys. Adapt it to a legible cool overcast LATE AFTERNOON, with softer painterly shading and far less ink linework so it blends into a premium stylized 3D forest battlefield. Do not copy the beacon, hero, logo space or night lighting.
Entire environment is distant: a vast ancient temperate forest valley surrounded by varied natural blue-grey mountain ranges, broken ridgelines and layered misty foothills; an elegant ruined pale stone fortress with slender towers on ONE distant ridge, small and integrated, occasional remote ruin silhouettes, winding bands of silver mist among dark olive/teal woods. No nearby large trees, no foreground props, no characters, no roads approaching the camera. Painterly textures, convincing rock strata and atmospheric depth, beautifully composed around ALL directions, sophisticated muted greens and slate blue, soft warm ivory sunlight breaking through heavy layered clouds from upper left, hopeful but mysterious. Strong detailed silhouettes but low contrast in the far distance so battles in the foreground remain readable.
PROJECTION / LAYOUT: horizon exactly at half image height. The mountain skyline occupies approximately the 35%-52% band of the image height. Broad intricate overcast sky and soft cloud layers occupy top half, smoothly becoming low-detail uniform slate blue towards the zenith at the top edge, NO visible sun disc. Distant forested valley extends below horizon to 70%; bottom 30% dissolves into softly textured muted fog/olive earth haze with no distinct objects, suitable for the nadir being hidden behind game terrain. The left and right edges show matching softly misted forest mountains and matching cloud values to wrap seamlessly; avoid any landmark on either edge.
No exaggerated cone mountains, no snow caps, no photorealism, no cartoon outlines, no low-poly art, no nearby subjects, no glowing corruption streak, no beacon tower, no labels, text, UI, frame, watermark, compass or borders. This must feel like a continuous surrounding fantasy world, not a backdrop poster.

```

## Lightbringer_Storybook_Logo_v1.prompt

```text
Mode: built-in image_gen, transparent background
Asset: Assets/Art/UI/MainMenu/Lightbringer_Storybook_Logo_v1.png

Use case: logo-brand.
Create a finished transparent PNG title logo for the dark fantasy storybook PC game LIGHTBRINGER. Image 1 is ONLY the palette, illustration texture and world reference: an ink-and-gouache painted ancient gold-trim beacon in an indigo valley. Do NOT reproduce this scene or its background. Output ONLY the isolated game logo on genuine alpha transparency.
Exact text: "LIGHTBRINGER", spelled L I G H T B R I N G E R, one word on ONE horizontal line. No other words or subtitle.
Art direction: a beautifully bespoke literary dark fantasy title, the lettering of an old illuminated storybook cover. Narrow elegant monumental serif capitals, subtly calligraphic wedge serifs and long graceful terminals, carefully hand-drawn character shapes with balanced optical kerning. Distinctive ornate initial L and final R, but all letters immediately readable. Antique pale ivory faces, aged muted gold edges, fine charcoal ink contours and delicate worn paper/paint grain WITHIN the letters. Flat illustrated craft, no shiny extruded 3D chrome. A little restrained gold filigree below the word: two fine tapering balanced flourishes meeting a tiny four-point beacon star, suggesting hope. No huge emblem. The word itself is the hero.
Keep the silhouette refined, calm, premium and solemn, suited to the reference's lonely hopeful storybook world. Clearly legible against deep indigo. Moderate serif stroke weight suitable for display at about 560 pixels wide. No exaggerated letter spacing, no generic font sample, no blocky blackletter, no sci-fi, no horror drips, no lens flare, no bloom or large glows, no bevelled mobile RPG badge, no shield plaque or enclosing frame. No background scenery, background fill, shadow rectangle, paper rectangle, checkerboard drawing, mockup or watermark.
Wide tightly composed horizontal asset, roughly 4:1 canvas, artwork occupying about 92% of width, with just enough clear padding to avoid clipped serif tips and flourishes. Preserve actual transparent alpha everywhere outside the logo.

```

## RuinedShrineGateway_v1.prompt

```text
Generator: built-in image_gen
Image: RuinedShrineGateway_v1.png

Create one isolated fantasy ruined shrine gateway asset for image-to-3D conversion. The attached gameplay screenshot is a structural reference only: use its pair of upright columns connected by a straight horizontal stone lintel, not the trees or entire scene. Exactly TWO tall parallel cylindrical limestone pillars supporting ONE continuous straight rectangular stone lintel across their tops, open rectangular passage between them, each column on its own square stepped plinth, no connecting floor platform. Both columns upright and structurally intact with worn edges, a few broad cracks and chipped corners; horizontal lintel weathered and partly chipped at one end but fully spans and rests on both columns. Match a premium stylized fantasy 3D game art direction: warm ivory stone, shallow broad vertical fluting, restrained antique gold bands beneath simple squared capitals, small subtle carved sun motifs, sparse muted moss at feet and ledges. Elegant substantial architectural proportions, opening clearly readable, no excessive ornament. Single complete modular architectural object, both pillars and lintel form one coherent connected structure. Centered fully visible with generous margins, single three-quarter front view with mild perspective, both columns and opening unobscured. Plain white studio background, soft even neutral illumination and minimal contact shadow. High quality sculpted beveled forms, not photorealistic, not basic low poly. No trees, no foliage beyond sparse moss attached to stone, no bushes, no loose rubble, no scenery, no characters, no magic, no UI, no labels, no text, no watermark, no collage or multiple views.

```

## RuinedShrinePillar_v1.prompt

```text
Generator: built-in image_gen
Image: RuinedShrinePillar_v1.png

Use case: stylized-concept. Create a polished 3D game asset reference image for image-to-3D reconstruction: exactly ONE ancient ruined fantasy shrine pillar, isolated centered on plain white background. Full object visible including base, generous white margins, single three-quarter view with mild perspective, camera slightly above mid height so broken top surface is visible. Premium stylized fantasy PC game aesthetic, beautifully sculpted bevels and deliberate readable forms, not photorealistic, not simple low-poly, not cartoon mobile. A tall slender warm ivory limestone cylindrical column, subtly tapering upward, shallow wide vertical flutes, on a sturdy square two-step stone plinth. Around 3.5 times taller than total base width. Uppermost capital is partly broken with a large asymmetric missing corner, exposing convincingly fractured stone; mostly intact shaft with a few broad cracks and chipped edges. A restrained narrow aged gold decorative band below the damaged capital and a subtle carved sun/star shrine motif, ancient noble light-themed architecture. Sparse muted green moss in base crevices and one small patch on a top ledge, stone remains dominant. Warm off-white stone, muted antique gold accents. Physically coherent contiguous single mesh-friendly object, all parts connected, no floating pieces. Soft neutral studio lighting, balanced illumination showing both visible sides, minimal contact shadow. No terrain platform beyond the architectural square plinth, no scattered rubble, no other pillars, no arch, no background scene, no people, no labels, no lettering, no watermark, no glow, no particles. Strong clear silhouette and clean surface color suitable as input to a 3D asset generator.

```

## StageOneBeaconNotes

```text
스테이지 1 — 중앙 측면 봉화와 성역

배치
- 중앙 길 왼편 (전장 로컬 좌표 X=-13, Z=10)에 주 봉화 하나를 배치한다.
- 기존 봉화 모델을 1.9배로 확대한다. 모델 높이는 약 9.12m이다.
- 야영지의 기존 봉화 두 개는 낮은 철제 등불로 교체한다.
- 성역에는 낮은 받침과 숲 쪽의 부서진 기둥을 배치한다.
- 중앙 길에서 성역으로 석조 진입로를 연결하고, 성역 및 진입로의 수목·관목·풀을 비운다.
- 병사 Path, 이동 지면과 충돌 규칙은 유지한다.

점화
- 전투 시작 및 재도전 시 꺼진 상태이다.
- 적 본진 파괴로 승리하면 보상 및 진행 저장은 기존 순서로 처리한다.
- 4.5초간 카메라가 봉화를 향하고, 룬과 금빛 핵이 켜진 뒤 빛의 파동이 퍼진다.
- 지형 전용 봉화 조명은 해당 Renderer의 MaterialPropertyBlock으로 전달한다.
- 결과창 및 레벨업 선택은 점화가 끝난 후 표시한다. 패배 시에는 점화하지 않는다.
- 미니맵에는 금색 테두리의 마름모로 봉화 위치를 표시한다.

Unity 확인
1. Play 중이면 종료하고 컴파일을 기다린다.
2. CampaignPrototype에서 스테이지 1에 진입한다. 수동 Inspector 연결은 필요 없다.
3. 출발 지점에서 왼편 봉화가 보이는지, 성역 접근과 중앙 전투에 방해가 없는지 확인한다.
4. 적 본진 격파 후 봉화 점화 → 필요 시 성장 선택 → 결과창 순서를 확인한다.
5. Lightbringer/Art/Capture Forest Ground Preview로 스테이지 1 정적 미리보기와 봉화 검사를 실행한다.
6. Docs/ArtPass/Preview/Beacon_Checks.txt, Beacon_Dormant.png, Beacon_Ignition.png를 확인한다.

추가 파일
Assets/Scripts/Visuals/Environment/BeaconSanctuary.cs (+ Unity meta)
Docs/ArtReference/Environment/StageOneBeaconNotes.txt

수정 파일
Assets/Scripts/Visuals/Environment/BattlefieldPropDressing.cs
Assets/Scripts/Visuals/Environment/BattlefieldEnvironment.cs
Assets/Scripts/Visuals/Environment/ForestEnvironmentDressing.cs
Assets/Art/Shaders/LightbringerTerrain.shader
Assets/Scripts/Core/PrototypeBattleBuilder.cs
Assets/Scripts/Core/CampaignSession.cs
Assets/Scripts/UI/ResultView.cs
Assets/Scripts/UI/BattleMinimap.cs
Assets/Scripts/UI/CampaignHUD.cs
Assets/Scripts/Editor/ArtAnimationPreview.cs
Assets/Scripts/Editor/Validation/HeroSystemsValidation.cs (중복 지역 변수 이름 및 메테오 경과시간을 반영하지 않던 쿨다운 검사 정리)

검증 범위
- 에디터 렌더 및 결정적 시간 진행으로 배치, 점화, 결과창 지연, 재도전, 패배를 검사한다.
- 실제 전투 중 수동 입력과 카메라 이동의 체감은 Play 모드에서 별도로 확인한다.

```

## StageOneGladeNotes

```text
스테이지 1 숲속 진입로 아트 패스

적용
- StageOneGlade.asset: 1스테이지 전용 숲 설정. ArtStyleLibrary의 Forest Environment에 연결.
- 해당 스테이지의 개별 숲 설정을 먼저 사용하고, 나머지는 배경 구간의 Forest 설정을 사용한다.
- 2~8스테이지는 ForestValleyBackdrop의 기존 ForestEnvironment 및 ForestTerrain을 유지한다.
- 플레이 지형 높이, 경로, 충돌, 소환 및 전투 규칙은 그대로 사용한다.

표현
- 나무 크기와 간격 변화, 숲 가장자리 보강.
- 바위·작은 돌·어린 나무·관목을 무리로 배치하고 큰 바위의 크기를 낮춤.
- 길 가장자리 풀의 크기와 음영 보강. 기존 패치 단위 거리 컬링 유지.
- 흙이 드러난 잔디, 확대된 지면 텍스처, 마모된 석재와 이끼 틈 표현.
- 유적 전용 복제 재질을 사용해 색을 낮추고 쓰러진 기둥 추가.
- 원본 공급업체 재질은 수정하지 않으며, 생성한 재질은 전장 종료 시 해제한다.

Unity 확인
1. Play 중이면 종료한 뒤 스크립트 컴파일을 기다린다.
2. CampaignPrototype에서 Play → 스테이지 1 전투 진입.
3. 별도 오브젝트 배치나 Inspector 연결은 필요하지 않다.
4. Lightbringer/Art/Capture Forest Ground Preview 메뉴로 고정 카메라 비교 가능.
5. Docs/ArtPass/Preview/ForestGround_Hero.png 및 ForestGround_Checks.txt 확인.

추가 파일 (Unity 메타 파일 포함)
Assets/Art/Environment/Forest/StageOneGlade.asset
Assets/Art/Environment/Forest/StageOneGladeGround.mat

수정 파일
Assets/Art/ArtStyleLibrary.asset
Assets/Art/Shaders/LightbringerTerrain.shader
Assets/Scripts/Visuals/Style/ForestEnvironmentProfile.cs
Assets/Scripts/Visuals/Environment/ForestEnvironmentDressing.cs
Assets/Scripts/Visuals/Environment/BattlefieldEnvironment.cs
Assets/Scripts/Visuals/Environment/BattlefieldPropDressing.cs
Assets/Scripts/Editor/ArtAnimationPreview.cs
Assets/Scripts/Editor/Validation/ArtStyleValidation.cs
Assets/Scripts/Editor/Validation/EnemyRosterValidation.cs (컴파일을 막던 중복 지역 변수 이름만 정리)

검증 산출물
Docs/ArtPass/Preview/StageOne_Before_Hero.png
Docs/ArtPass/Preview/StageOne_Before_Overview.png
Docs/ArtPass/Preview/ForestGround_*.png
Docs/ArtPass/Preview/ForestGround_Checks.txt
Docs/ArtPass/Preview/Battlefield_*.png
Docs/ArtPass/Preview/Battlefield_Checks.txt

검증 결과 (2026-10-13 KST)
- Unity 재컴파일 완료. Greybox 자동 검사 380개 PASS.
- 1·8스테이지 미리보기에서 장식물 충돌체 없음, 경로 여유 공간, 평탄한 이동 지면,
  지면 셰이더 컴파일, 스테이지별 재질 분리 모두 PASS.
- 영웅 시점, 전경, 지면 근접 화면을 렌더링해 확인.
- 실제 전투 중 프레임 성능 및 수동 입력 플레이는 이번 확인에 포함하지 않음.

```

## Title_LastLight_Storybook_v1.prompt

```text
Mode: built-in image_gen
Reference: ../Prologue/Prologue_07_ForgottenBeacon_Storybook_v1.png
Output: Title_LastLight_Storybook_v1.png

Use case: illustration-story.
Create a new Lightbringer title/menu background illustration, wide cinematic exact 16:9 composition (prefer 1536x864 or 2048x1152).
Input image 1 is a STYLE AND BEACON ARCHITECTURE REFERENCE ONLY: Prologue_07_ForgottenBeacon_Storybook_v1.png. Match its hand-painted dark fantasy storybook aesthetic, visible confident ink linework, rough matte paper / dry pigment texture, textured painterly shading, subdued atmospheric depth, and aged stone tower with gold Gothic filigree, blue pointed roof and ivy. Create the NEW composition described below; do not copy its left-positioned tower or large foreground heroine.
At dusk, a single ancient stone beacon tower with gold filigree and ivy stands on a grassy hilltop at the right-center of the frame, around 68-72% across the image. Show the whole tower with breathing room above the roof. Its glass lantern blazes with warm golden-white light piercing the surrounding darkness, painted luminous ivory and gold rather than glossy digital lens effects. Soft light motes and embers drift upward from it. Warm glow illuminates moss, weathered stones and tall grass around its base.
Behind it lies a vast dark valley with a winding road, ruined village, and a few distant UNLIT beacon towers fading into fog. Heavy storm clouds fill the sky; far along the horizon is a thin, faint, ominous crimson-violet corruption glow.
A TINY silhouette of a hooded commander holding a staff, seen from behind, stands near the beacon for scale. Keep this figure very small and subordinate, about 5-7% of image height, no portrait details.
The LEFT THIRD of the image must remain calm dark sky and shadowed hillside, low contrast and quiet with generous continuous negative space for a future logo and menu. Do not render any actual logo/menu. Keep roads, village details, bright horizon accents, embers, the character and tower out of this left-third reserved area. Soft painterly texture and subtle dark cloud shapes are fine, no blank solid rectangle and no artificial division.
Color palette: deep indigo and charcoal shadows, warm gold light, ivory highlights, faint crimson on the far horizon. Mood: lonely hope, the last light in the darkness.
Visible ink contours and matte hand-painted shading, storybook illustration matching the supplied reference, not photorealistic, not 3D CGI.
No text, letters, logo, watermark, UI, frame or border.

```

## Title_LastLight_Storybook_v2.prompt

```text
Mode: built-in image_gen edit
Edit target: Title_LastLight_Storybook_v1.png
Character reference: ../Characters/CharacterSheet_FemaleHero_v1.png
Output: Title_LastLight_Storybook_v2.png

Use case: precise-object-edit.
Image 1 is the EDIT TARGET: the completed dark fantasy storybook beacon landscape. Image 2 is ONLY a character identity/costume reference: CharacterSheet_FemaleHero_v1.png, specifically the main female Mage Commander and her BACK VIEW. Do not use the sheet's rendering style, text, layout, other characters or creatures.
Make exactly one localized change in image 1: replace the tiny hooded figure standing just left of the beacon base with the project's adult FEMALE HERO SEEN FROM BEHIND. Match the existing figure's exact location, tiny scale, grounded feet, upright standing pose, and staff placement. Do not enlarge her or change framing. She looks away from the viewer toward the beacon and valley, no visible face, no looking back.
Female hero details from image 2, simplified to be readable at the existing small scale: exposed dark brown-black hair gathered in a high bun (hood DOWN, head uncovered), ivory cape with navy lining and a narrow navy back panel with simplified gold star/tree embroidery, white/navy layered mage commander outfit, small gold-trimmed shoulder armor, dark fitted boots. Staff held in her right hand, with gold circular orbital head and a small subdued blue orb, no added large glow or magic effects. Ivory is shaded naturally by the dusk, warmed by the existing beacon light. Keep painterly ink contours and rough matte storybook texture identical to the target, not polished anime or CGI.
Preserve EVERYTHING else in image 1 unchanged: the whole beacon tower and its gold-white lantern, all ivy and stone ornament, embers, sky and every cloud, crimson-violet horizon, mountains, village, winding road, distant unlit towers, trees, foreground grasses and rocks, lighting and color grading, and the quiet dark left third for future menus. Same canvas, aspect ratio, framing and composition. Edit only the small character and her staff silhouette. No text, letters, logo, watermark, UI or border.

```
