# Saksi Terakhir

Saksi Terakhir is a first-person psychological horror investigation game set in a mysterious Indonesian village. Explore abandoned locations, uncover hidden truths, and survive an ancient force that watches every step you take.

| | |
|---|---|
| **Engine** | Unity 6000.4.0f1 |
| **Render pipeline** | URP 17.4.0 · Forward+ · Linear · HDR on · MSAA off |
| **Platform** | PC (developed on Linux, OpenGL Core 4.6) |
| **Perspective** | First person |
| **Genre** | Psychological horror investigation |
| **Estimated playtime** | 3–5 hours |
| **Endings** | Multiple |
| **Combat** | Minimal by design |
| **Status** | In development · design docs at version 0.1 |
| **Team** | Solo developer |

---

## Contents

- [Premise](#premise)
- [Design pillars](#design-pillars)
- [Core gameplay loop](#core-gameplay-loop)
- [Mechanics](#mechanics)
- [World rules](#world-rules)
- [Chapters](#chapters)
- [Cast](#cast)
- [What this game is not](#what-this-game-is-not)
- [Technical target](#technical-target)
- [Repository layout](#repository-layout)
- [Runtime systems](#runtime-systems)
- [Editor tools](#editor-tools)
- [Procedural asset pipeline](#procedural-asset-pipeline)
- [Current content](#current-content)
- [Getting started](#getting-started)
- [Design documents](#design-documents)
- [Conventions](#conventions)
- [License](#license)

---

## Premise

The player is a government archive officer assigned to organize and digitize old
documents before they are permanently destroyed. During that routine assignment they
find an unregistered archive: evidence of an Indonesian village that officially does
not exist.

On arrival the village looks entirely ordinary. Children play outside, shops are open,
the mosque continues its daily activities. Nothing looks unusual — yet everything feels
wrong. The villagers know more than they should, every conversation seems rehearsed,
every silence hides something.

As the investigation deepens, reality begins to fracture, and one truth becomes clear:
someone, or something, has been waiting for the player to arrive.

**Central theme:** *"Not every truth is meant to be uncovered."*

---

## Design pillars

1. **Atmosphere first** — silence, lighting, and environmental storytelling carry the
   fear, not jump scares.
2. **Mystery drives progression** — the player advances because they want answers.
3. **Investigation over combat** — observe, collect evidence, solve the mystery.
4. **Psychological horror** — reality becomes unreliable; certainty never arrives.
5. **The feeling of being watched** — the world reacts to the player's presence.

Every feature must strengthen at least one pillar. If it does not, it does not ship.

---

## Core gameplay loop

```
Observe → Explore → Investigate → Collect evidence → Analyze
   → Solve puzzle → Unlock area → Discover truth → Pressure rises → repeat
```

Every completed investigation reveals another mystery rather than a complete answer.
The player never grows stronger — progression is knowledge, understanding, and access
to new locations.

Fear escalates in five stages: something feels wrong → inconsistencies appear →
reality becomes unreliable → the village reacts to the player → confrontation becomes
unavoidable.

---

## Mechanics

| Mechanic | Role |
|---|---|
| Exploration | Primary activity. No objective markers; locations are found through maps, notes, conversation, and environmental clues. |
| Movement | Walk, run, crouch, jump, climb small obstacles. Ordinary human capability only. |
| Interaction | Doors, switches, pickups, pushable objects, readable documents. |
| Object inspection | Rotate, zoom, and read details to find hidden marks. |
| Evidence collection | Documents, photographs, audio recordings, keys, government files — stored automatically. |
| Document reading | A primary storytelling tool. Not every document tells the truth. |
| Inventory | Deliberately small: flashlight, HT, keys, documents, puzzle items. |
| Flashlight | Main light source. Not a weapon; light does not guarantee safety. |
| Handheld radio (HT) | Recovered from The Outsider. Warns, guides, and misleads. Never fully trustworthy. |
| Dialogue | NPCs may tell the truth, lie, deflect, or withhold. Choices change available information. |
| Investigation journal | Automatically records evidence, objectives, notes, village rules, and maps — organizes without solving. |
| Puzzles | Grounded in village history, documents, symbols, and mechanisms. Never arbitrary gates. |
| Observation | Rewarded: strange behavior, environmental change, sound shifts, animal reactions. |
| Stealth | Situational. Hide, stay quiet, avoid detection. |
| Chase sequences | Rare and memorable. The player cannot fight — escape, hide, survive. |
| Decisions | Influence information, relationships, progression, and ending. Never signposted as correct. |
| Environmental change | Paths appear, buildings feel different, objects move, sounds vanish. |
| Save system | Manual saves at designated locations, plus auto-save on major story beats. |

---

## World rules

The supernatural obeys a fixed internal logic — nothing happens "just because it is
horror." A selection of the sixteen rules in [Docs/Worl Rule.md](Docs/Worl%20Rule.md):

- The village cannot be found by someone not meant to find it. No map or GPS is
  accurate.
- The village always appears normal at first. Horror escalates slowly.
- Knowledge changes reality — the more truth uncovered, the more unstable the world.
- Silence is information. When nature stops, something is happening.
- Animals perceive danger before humans do; observant players gain warning.
- Light represents safety, not protection.
- Documents never tell the whole truth. Sources must be compared.
- The player is not special. The village has received visitors before.
- Every mystery has an explanation. Not every explanation will be found.

---

## Chapters

| # | Chapter | Theme | Gameplay focus | Ends when |
|---|---|---|---|---|
| 1 | The Assignment | Curiosity | Movement, interaction, reading | The player enters the village |
| 2 | A Village That Shouldn't Exist | Suspicion | Exploration, dialogue, evidence | The decades-long secret is felt |
| 3 | The Missing Witness | Investigation | Puzzles, document analysis, HT | The player is repeating someone else's investigation |
| 4 | The Rules | Fear | Stealth, survival, environmental change | The truth beneath the village surfaces |
| 5 | The Truth | Despair | Final investigation, hard decisions | The point of no return |
| 6 | The Last Witness | Acceptance | Final exploration, ending choice | Multiple endings resolve |

Per-chapter drafts live in [Docs/Story/](Docs/Story/), including
[Endings.md](Docs/Story/Endings.md).

---

## Cast

| Group | Characters |
|---|---|
| Main | Archive Officer — the playable investigator. No supernatural ability; knowledge is the only weapon. |
| Archive office | Senior Archivist, Archive Supervisor, Administrative Staff |
| Outside the village | Friend 01, Friend 02 (separated from the player mid-story), The Outsider (never met — only traces) |
| Village | Village Chief, Village Elder, Religious Figure, Child, Child's Mother, Village Dog (a warning system), background residents |
| Mystery | The Last Witness, Missing Child, The Presence |
| Environmental | The Village itself — it observes, remembers, and reacts |

No character explains the whole truth. Full detail in
[Docs/Characters.md](Docs/Characters.md).

---

## What this game is not

Not a shooter, action horror, survival crafting, multiplayer, loot, or RPG game, and
not a jump-scare simulator. Explicitly excluded: weapon upgrades, character levels,
skill trees, crafting, base building, hunger and thirst meters, experience points, and
fast travel. Any feature pushing the project toward these is rejected unless it clearly
serves the pillars.

---

## Technical target

The target machine dictates every rendering decision:

| | |
|---|---|
| GPU | **Intel HD Graphics 4600** (Haswell GT2, 2013, 20 EU) |
| GPU memory | Shared with system, DDR3, ~25 GB/s bandwidth |
| CPU | Core i7-4790 @ 3.6 GHz (4C/8T) — ample; the bottleneck is purely GPU |
| Graphics API | OpenGL Core 4.6 (Mesa), not Vulkan |
| **Frame target** | **1280×720 at 30 FPS** |

This is roughly a tenth of an Iris Xe. Generic "integrated GPU" guidance is far too
generous here, so the budget is calibrated for 720p30 rather than 1080p60 — a stable
30 FPS also suits horror pacing.

Consequences worth remembering:

- **GPU Resident Drawer is impossible on OpenGL** (needs `BatchBufferTarget.RawBuffer`)
  and is disabled.
- **Native Render Pass is ignored** — that is a Vulkan/Metal path.
- **Compute shaders are expensive on Haswell**, which matters for Forward+ light
  culling.

Horror mood — darkness, flashlight shadows, fog, ambient occlusion — is treated as a
core feature, not decoration. Cost is cut in this order: remove what is not visible,
then reduce visible quality, and `RenderScale` only as a last resort.

Key settings changed from the URP template (in `Assets/Settings/PC_RPAsset.asset` and
`PC_Renderer.asset`): shadow cascades 4 → 2, shadow distance 50 → 30, soft shadow
quality High → Low, additional-light shadowmap 2048 → 1024, cookie atlas 2048 → 512
grayscale, opaque texture off, screen-space lens flare off, and SSAO computed at half
resolution. Realtime flashlight shadows and SSAO are deliberately kept.

Every knob, the reasoning behind it, and the ordered list of what to cut next is
recorded in
[Assets/_Project/Documentation/RenderBudget.md](Assets/_Project/Documentation/RenderBudget.md).
Measure with **Window → Analysis → Rendering Debugger** and the Profiler GPU module,
in a real scene with the flashlight on.

### Packages

`com.unity.render-pipelines.universal` 17.4.0 · `com.unity.inputsystem` 1.19.0 ·
`com.unity.ai.navigation` 2.0.11 · `com.unity.timeline` 1.8.11 · `com.unity.ugui` 2.0.0 ·
`com.unity.test-framework` 1.6.0 · `com.unity.visualscripting` 1.9.10 ·
`com.unity.collab-proxy` 2.11.4 · Linux x86_64 SDK and toolchain 1.1.0

---

## Repository layout

```
Assets/
  _Project/
    Art/Models/          generated FBX + extracted materials
    Art/Textures/        characters, fabric, environment, sky
    Animations/          339 clips: Armed, Unarmed, Relax, Crawl, Swimming,
                         Climb-Ladder, Climb-Ledge
    Documentation/       RenderBudget.md
    Environment/         time-of-day presets
    Localization/        LocalizationTable.asset
    Prefabs/             gameplay-ready prefabs
    Scenes/              Regional Archive Office, Regional Vilage
    Scripts/Editor/      editor tooling (menu items below)
    Scripts/Runtime/     player, interaction, localization, environment
  Settings/              URP assets: PC_RPAsset, PC_Renderer, volume profiles
Docs/                    design documents (see index below)
Tools/CharacterGenerator/  Blender procedural asset pipeline
```

---

## Runtime systems

Implemented in `Assets/_Project/Scripts/Runtime/`:

| Area | Scripts |
|---|---|
| Player | `PlayerController` (CharacterController-based, crouch with standing-height probe), `PlayerCameraController`, `PlayerInputReader`, `PlayerAnimatorDriver`, `FirstPersonBody` |
| Interaction | `Interactable` (abstract base with localized prompt key), `DoorInteractable` (swing with obstacle probing), `PlayerInteractor`, `InteractionPromptView` |
| Localization | `LocalizationManager`, `LocalizationTable`, `LocalizationBootstrapper`, `LocalizedText` — English and Indonesian, keyed strings such as `interact.door.locked` |
| Environment | `TimeOfDayPreset`, `TimeOfDayApplier` |

---

## Editor tools

Under `Assets/_Project/Scripts/Editor/`, exposed on the **Saksi Terakhir** menu:

| Menu item | Purpose |
|---|---|
| Create Prefabs | Builds building and character prefabs from imported models, adding mesh colliders, door components, and static flags |
| Create Vehicle Prefabs | Configures vehicle materials, derives box colliders from mesh vertices, and saves the three SUV prefabs |
| Probe Player Route | Read-only diagnostic: raycasts the player's path to the gate and entrance, reporting ground height and capsule blockage |

`GeneratedAssetPostprocessor` applies import settings automatically for anything under
`Art/Models/` and `Art/Textures/` — file scale, imported normals, Mikkt tangents,
external material extraction, secondary (lightmap) UVs, and per-folder texture rules
including cubemap sky and normal-map fabric. Additional setup scripts:
`LocalizationTableSetup`, `TimeOfDaySetup`, `PlayerRigSetup`, `PlayerRigFixer`,
`InteractionProbe`, `GeneratedAssetReport`.

---

## Procedural asset pipeline

Art in this project is generated from Python rather than modelled by hand. Scripts live
in `Tools/CharacterGenerator/` and run headless against Blender 5.1+:

```bash
blender --background --factory-startup --python Tools/CharacterGenerator/generate_suv.py
```

Relative paths fail because the working directory is not preserved — pass absolute
paths.

| Script | Output |
|---|---|
| `generate_suv.py` | `SUV_Fleet` — a full SUV with opening doors, separate roll-down door glass, framed windows, and a modelled interior |
| `generate_office.py` | `Office_ArchiveHQ` — the archive building |
| `generate_mc.py`, `generate_b.py` | Protagonist and colleague characters |
| `generate_humanoid.py`, `character_body.py`, `humanoid_rig.py` | Humanoid body and rig construction |
| `audit_vehicle.py` | Read-only audit: watertightness, panel fit, glass framing, ray-leak checks |
| `render_vehicle.py` | 20 orthographic and perspective preview renders, including per-colour shots |
| `export_vehicle.py` | Exports the three painted SUV variants as FBX into `Art/Models/` |
| `bake_albedo.py`, `bake_building.py`, `bake_sky.py`, `texture_bake.py` | Texture and sky baking |
| `mesh_kit.py`, `vehicle_kit.py`, `building_kit.py` | Shared geometry, material, and export helpers |

Supporting scripts in the same folder cover textured character export
(`export_textured.py`), preview rendering (`render_preview.py`,
`render_building.py`), fabric textures (`make_fabric_textures.py`), and rig posing
checks (`pose_test.py`).

Two knobs worth knowing:

- **`vehicle_kit.DETAIL`** scales the whole SUV's density from one value. `1` produces
  roughly 35k triangles; the committed value `2.2` produces about 140k. Given the
  HD 4600 target, the high setting is intended as a bake source for a low-poly version.
- **`generate_suv.PAINT_FINISHES`** defines the three body colours (black, silver,
  grey). Pick one with `-- --paint silver`; all three exist in the saved `.blend` so
  they can be swapped in Blender without rebuilding.

---

## Current content

Two scenes exist: `Regional Archive Office` and `Regional Vilage`. Generated and
imported art includes the archive building, two characters, a 339-clip humanoid
animation library, sky textures with time-of-day presets, and three SUV prefabs with
derived box colliders. The localization table currently holds interaction prompts only.

---

## Getting started

1. Install **Unity 6000.4.0f1** through Unity Hub.
2. Open the project root. First import takes a while — generated FBX files carry
   lightmap UV generation.
3. Open a scene from `Assets/_Project/Scenes/`.
4. To regenerate art, install **Blender 5.1+** on `PATH` and run the scripts above,
   then let Unity reimport.

---

## Design documents

| Document | Contents |
|---|---|
| [Vision.md](Docs/Vision.md) | Pillars, player fantasy, art and audio direction, anti-vision |
| [Story.md](Docs/Story.md) | Premise, themes, narrative goals, player motivation |
| [Game Chapters.md](Docs/Game%20Chapters.md) | Chapter-by-chapter progression |
| [Story/](Docs/Story/) | Per-chapter drafts and endings |
| [Gameplay.md](Docs/Gameplay.md) | Loop, pillars, fear progression, HT gameplay, failure states |
| [Mechanics.md](Docs/Mechanics.md) | Every player-facing mechanic, and the excluded ones |
| [Worl Rule.md](Docs/Worl%20Rule.md) | The sixteen laws the supernatural obeys |
| [Lore.md](Docs/Lore.md) | Village history, the erased records, the Presence |
| [Characters.md](Docs/Characters.md) | Full cast with motivations and story functions |
| [Timeline.md](Docs/Timeline.md) | World chronology, mostly for internal consistency |
| [Tasks.md](Docs/Tasks.md) | Development backlog and open design questions |
| [Ideas.md](Docs/Ideas.md) | Unsorted and undecided ideas |
| [RenderBudget.md](Assets/_Project/Documentation/RenderBudget.md) | Every rendering decision and its reasoning |

Design documents are at version 0.1 and still moving; the story documents in
particular are drafts rather than settled specification.

---

## Conventions

Project-wide rules for contributors and AI assistants are defined in
[Claude.md](Claude.md), including code style, naming, and an absolute prohibition on
automated Git operations — the repository owner performs all Git work.

---

## License

No license has been chosen yet, so all rights are reserved by default.
