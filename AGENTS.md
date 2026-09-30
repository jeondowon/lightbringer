# Lightbringer — Codex Project Instructions

## Project Overview

Project name: Lightbringer

This is a desktop PC game built with Unity 6.3 LTS using URP and C#.

Before implementing gameplay systems, read:

- `Docs/GameDesign/3D_Paladog_Project_Handoff_v0.3.docx`
- `Docs/ArtReference/Characters/CharacterSheet_FemaleHero_v1.png`

Treat the GameDesign document as the primary source of truth for gameplay decisions.

---

## Core Game Concept

Lightbringer is a third-person 3D fantasy action-defense game.

The player directly controls a hero on the battlefield.

The player spends Food to summon allied units one at a time.

Summoned units automatically advance along their assigned Path and fight enemies.

The player does NOT directly control individual soldiers.

The hero supports the battlefield through direct combat, equipment abilities, magic, and a large aura.

---

## Important Design Rules

Do NOT turn this game into an RTS.

Do NOT add:

- individual unit selection
- drag selection
- manual formation control
- rally-all-units commands
- unit deck restrictions
- stage-reset roguelike progression

Unless explicitly requested, do not introduce gameplay systems that conflict with the design handoff.

Units should be deployed, not micromanaged.

---

## Path System

A stage may contain approximately 1–3 primary Paths.

When a Path is selected:

- newly summoned units are assigned to that Path
- existing units stay on their original Path
- units automatically advance toward the enemy objective

The player can personally move between fronts to support them.

---

## Hero

The final game supports male and female hero selection.

Initial development and the first art pass use the female hero.

The hero is a stylized human fantasy commander using:

- light
- runes
- magic
- staff-based combat
- aura support

The hero's aura is one of the game's signature mechanics and visuals.

---

## Art Direction

Target visual style:

Premium Stylized Fantasy 3D.

Avoid:

- simple low-poly as the final visual target
- mobile-game-style presentation
- photorealism
- excessive VFX that makes large battles unreadable

Reference the character sheet under:

`Docs/ArtReference/Characters/`

---

## Current Development Phase

We are currently building the Greybox Prototype.

Use simple primitives such as:

- Capsule
- Cube
- Plane

Do not spend time implementing final character models or polished VFX yet.

The prototype should validate whether this core loop is fun:

1. Third-person hero movement
2. Food generation
3. Unit summoning
4. Automatic Path advancement
5. Enemy detection and combat
6. Hero aura
7. Enemy base / victory condition

---

## Development Rules

Primary engine:
Unity 6.3 LTS

Render pipeline:
URP

Language:
C#

Prefer simple, modular systems that can later be expanded.

Keep gameplay code separated by responsibility.

Suggested structure:

Assets/
- Scripts/
  - Player/
  - Units/
  - Combat/
  - Aura/
  - Resources/
  - Pathing/
  - UI/
  - Core/
- Prefabs/
- Scenes/
- Art/
- Materials/
- VFX/
- Audio/

Do not modify generated Unity folders:

- Library/
- Temp/
- Logs/
- obj/

Only modify `Packages/` or `ProjectSettings/` when required.

---

## Codex Working Style

Before modifying existing code:

1. inspect the relevant files
2. understand the existing implementation
3. avoid unnecessary rewrites

For each task:

1. briefly state the implementation plan
2. make the changes
3. check for obvious compile errors
4. list files created or modified
5. tell me what I need to do inside the Unity Editor

When possible, automate repetitive Unity setup using Editor scripts.

Do not silently change the game's design.

If a requested implementation conflicts with the GameDesign document, point it out before changing the architecture.