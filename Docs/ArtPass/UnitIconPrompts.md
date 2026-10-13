# 아군 유닛 아이콘 프롬프트 (GPT imggen)

병력 준비 화면 왼쪽 목록에 쓸 유닛 상징 아이콘 8종.
포맷은 기존 `Assets/UI/Art/Skill_*.png`의 금테 메달리온을 그대로 따른다.

## 작업 규칙

- 레퍼런스로 `Assets/UI/Art/Skill_Heal.png`, `Assets/UI/Art/Skill_Ice.png`를 먼저 첨부한다.
- 첫 장(검병)으로 톤을 확정한 뒤, 그 결과 이미지를 레퍼런스에 추가해서 나머지를 생성한다.
- 글로우 색은 이미지에 넣지 않는다. 코드(USS)에서 아이콘 뒤에 깔아 입힌다.
- 배경은 **완전 투명 PNG**로 받는다. 바깥 배경과 메달리온 안쪽 원 배경 모두 투명이어야
  뒤에 깐 글로우가 비쳐 보인다.
- 저장 위치와 이름: `Assets/UI/Art/UnitIcon_<Unit>.png`
  (`Swordsman`, `Archer`, `Shieldbearer`, `Spearman`, `Priest`, `Mage`, `Knight`, `Dragon`)

## 공통 프롬프트

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

## 유닛별 문구 (공통 프롬프트 뒤에 이어서 붙인다)

### Swordsman (검병)
```
The central emblem: an upright longsword with a simple steel crossguard and leather-wrapped
grip, polished blade with a faint highlight along the edge.
```

### Archer (궁수)
```
The central emblem: a recurve wooden bow with a single arrow nocked and drawn slightly,
small green leaf accents carved on the limbs.
```

### Shieldbearer (방패병)
```
The central emblem: a sturdy kite shield with a steel rim, a subtle sun-rune engraving on
the face, slightly dented and worn.
```

### Spearman (창병)
```
The central emblem: a long spear pointing upward with a leaf-shaped steel head and a small
red pennant fluttering below the blade.
```

### Priest (사제)
```
The central emblem: a holy sigil of a radiant sun-cross, golden metal with a soft inner
light, short ornamental rays around it.
```

### Mage (마법사)
```
The central emblem: a crystal-tipped magic staff topped with a violet orb held in a small
golden claw, faint engraved runes along the shaft.
```

### Knight (기사)
```
The central emblem: a crowned knight's helmet in three-quarter profile with a feathered
plume, polished plate steel with gold trim.
```

### Dragon (드래곤)
```
The central emblem: a dragon head in profile with swept-back horns, scaled hide and
glowing amber eyes, mouth slightly open. Physical sculpted emblem, no flames.
```

## 문제 생길 때 보정 문구

| 증상 | 추가할 문구 |
|---|---|
| 프레임이 장마다 달라짐 | `Keep the frame identical to the first icon. Change only the central emblem.` |
| 글자가 생김 | `Absolutely no text, no letters, no symbols that resemble writing.` |
| 안쪽 배경이 채워져 나옴 | `The inner circle must be fully transparent, remove any background fill or glow inside the ring.` |
| 상징이 너무 이펙트 같음 | `Physical object only, no magic effects, no energy, no light beams.` |
| 투명 PNG를 못 줌 | 흰색(#FFFFFF) 단색 배경으로 받는다. 안쪽 원도 흰색이면 제가 키잉으로 제거한다. |

## 게임 쪽 처리 계획

- 목록 타일(`.lb-upg-art`)은 `UnitIcon_*`로 교체하고 `scale-to-fit`으로 표시한다.
- 아이콘 뒤에 `Prep_Glow.png`를 깔고 `-unity-background-image-tint-color`로 역할별 색을 입힌다.
  (근접 / 원거리 / 지원 정도로 클래스 3개)
- 선택된 타일은 글로우 불투명도를 올려 강조, 잠긴 타일은 기존처럼 tint로 어둡게 처리한다.
- 오른쪽 큰 일러스트는 유지하고, 크기는 고정 캔버스로 정리한다(별도 작업).
