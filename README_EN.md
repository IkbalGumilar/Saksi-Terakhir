# The Last Witness

[Bahasa Indonesia — Saksi Terakhir](README.md) · **English**

A first-person 3D psychological horror and mystery game set in Indonesia. An archive employee and two colleagues investigate a village discovered in old records. Players follow conversations, read documents, and make decisions recorded as story conditions.

This repository contains the Unity project, gameplay code, assets, prefabs, and Blender sources. The documentation describes the current implementation without revealing the central mystery.

## Development status

The project is under development. Chapter 1 systems cover office activities, finding colleagues, departure preparations, an automatic car journey to a fuel station, fuel-station activities, and a fade-to-black ending. In the Unity Editor, the ending stops Play Mode after progress is saved successfully. In a player build, it freezes gameplay on a black screen; the transition to the next chapter is not implemented yet.

The main scene is [Regional Archive Office](Assets/_Project/Scenes/Regional%20Archive%20Office.unity), the only scene currently enabled in the [build configuration](ProjectSettings/EditorBuildSettings.asset). Village and asset-assembly scenes also exist. The presence of code and assets does not mean the entire game is complete or every path has been tested on every device.

Versions and documentation references were checked against project files on **October 11, 2026**. This README update did not run Play Mode or performance tests.

## Technology

| Component | Version / use |
| --- | --- |
| Unity | **6000.3.24f1 — Unity 6.3 LTS** |
| Universal Render Pipeline | **17.3.0** |
| Input System | **1.19.0** |
| AI Navigation | **2.0.11** |
| uGUI | **2.0.0**, with TextMeshPro for text |
| Unity Test Framework | **1.6.0** |
| Timeline | **1.8.11** |
| Programming language | C#; separate runtime and Editor assemblies |
| Localization | Custom Indonesian and English text table |
| Persistence | JSON snapshots with encryption and authentication |
| Asset creation | Blender, FBX models, materials, textures, and Unity prefabs |

The Editor version follows [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt). Dependencies are recorded in [manifest.json](Packages/manifest.json) and [packages-lock.json](Packages/packages-lock.json). An installed package does not automatically indicate a gameplay feature such as multiplayer.

## Opening the project

1. Install Unity Hub and Unity **6000.3.24f1**. Add build modules for your target platform when needed.
2. Add the repository folder in Unity Hub and open it with that version.
3. Wait for package restoration, asset import, and compilation to finish. Unity rebuilds `Library` if it is absent.
4. Open [Regional Archive Office.unity](Assets/_Project/Scenes/Regional%20Archive%20Office.unity).
5. Enter Play Mode. Choose **New Game**, or **Continue** when a valid autosave exists.

Scenes and assets are already stored in the repository; rerunning every scene-generation tool is not required to open the project. Development takes place on Ubuntu, while build compatibility and minimum hardware requirements still need validation on target platforms. No official performance benchmark or minimum specifications have been established.

## Controls

Bindings are defined in [InputSystem_Actions.inputactions](Assets/InputSystem_Actions.inputactions) and read by [PlayerInputReader](Assets/_Project/Scripts/Runtime/Player/PlayerInputReader.cs).

| Action | Keyboard / mouse |
| --- | --- |
| Move | WASD or arrow keys |
| Look | Mouse movement |
| Sprint | Left Shift |
| Crouch | C; configurable hold or toggle behavior |
| Jump | Space |
| Interact | E |
| Open / close inventory and journal | Tab |
| Close inventory / request pause | Escape |
| Select an item or quest | Left click |
| Item context menu | Right click |
| Move an item | Drag and drop |

Gamepad bindings also exist: left/right sticks for movement/look, left-stick press for sprint, B for crouch, A for jump, Y for interaction, Select for the journal, and Start for pause. These bindings still require testing on physical devices.

The inventory header provides **Inventory**, **Documents**, and **Quests** pages, selected by clicking or arrow navigation. Story dialogue advances automatically according to text duration. During telephone conversations, movement and look remain available, while inventory and pause are blocked. In the car, driving is automatic and the player can move the camera. Travel dialogue other than phone calls allows inventory and pause.

`Escape` closes the inventory first when it is open. [PlayerPauseController](Assets/_Project/Scripts/Runtime/Player/PlayerPauseController.cs) provides pause logic and UI hooks; a pause request requires an attached panel or handler. Opening the inventory locks player controls but does not stop world time.

## Gameplay systems

### Player and interaction

[PlayerController](Assets/_Project/Scripts/Runtime/Player/PlayerController.cs) handles walking, sprinting, crouching, gravity, jumping, and cinematic movement. [PlayerCameraController](Assets/_Project/Scripts/Runtime/Player/PlayerCameraController.cs) manages look and menu previews, while [PlayerInteractor](Assets/_Project/Scripts/Runtime/Interaction/PlayerInteractor.cs) finds interaction targets and displays prompts.

Automatic movement uses terrain checks and navigation to find supported ground and avoid obstacles. NPC conversation preparation seeks a position approximately **2 meters** away, subject to reachable space. Idle breathing and the menu's side-to-side look movement are handled separately.

### Inventory, documents, and held items

| Area | Behavior |
| --- | --- |
| Item grid | Defaults to **3 × 3**; configurable from **1–8 columns** and **1–8 rows**, up to **64 slots** |
| Documents | Separate list; no item-grid capacity cost and cannot be discarded like physical items |
| Rearrangement | Items can be moved or swapped between slots using drag and drop |
| Drop | The Drop menu action or dragging outside the item panel creates a 3D object with a collider and Rigidbody |
| Pickup | Dropped objects can be picked up again while retaining their instance identity |
| Full inventory | Adding an item is rejected and an inventory-full notification is shown |
| Hold | Up to two items; a third replaces the item held longest |
| Use | A matching held item is used directly on a target; a stored item is selected through the inventory |

Grid size can be increased through `TrySetGridSize`; finding bags and connecting all upgrade rules remain content work. Slots are duplicated from UI templates at **100-unit** spacing. Held-item markers and hand models follow item state.

Definitions use [ItemDefinition](Assets/_Project/Scripts/Runtime/Inventory/ItemDefinition.cs), a ScriptableObject:

| Attribute | Content |
| --- | --- |
| ID | Stable identity, such as `item.car_key` |
| Name | Localization text key |
| Description | Localization text key |
| Type | `Item` or `Document` |
| Icon | Slot sprite |
| Model Prefab | 3D model; optional for documents |
| Document Content | Readable document text |

The [item catalog](Assets/_Project/Resources/ItemDefinitionCatalog.asset) connects definitions to the system. Definitions live in [Items](Assets/_Project/Items), icons in [Items/Image](Assets/_Project/Items/Image), and pickup objects in [Prefabs/Inventory/Pickups](Assets/_Project/Prefabs/Inventory/Pickups). Available modeled items include the car key/remote, flashlight, 18650 Li-ion battery, power bank, 10-meter natural-fiber rope, 10-meter nylon rope, wrapped rice meal, first-aid box, and notebook.

Use effects depend on the attached handler or target; a model and definition do not imply a complete gameplay effect for every item. The main flow is in [InventoryPanelController](Assets/_Project/Scripts/Runtime/Inventory/InventoryPanelController.cs), with data logic in [InventoryState](Assets/_Project/Scripts/Runtime/Inventory/InventoryState.cs).

### Quests, branches, and dialogue

[ChapterOneProgress](Assets/_Project/Scripts/Runtime/Story/ChapterOneProgress.cs) manages Chapter 1 stages. [ChapterOneStoryState](Assets/_Project/Scripts/Runtime/Story/ChapterOneStoryState.cs) records choices and story facts, while [ChapterOneConditionEvaluator](Assets/_Project/Scripts/Runtime/Story/ChapterOneConditionEvaluator.cs) checks requirements involving quests, dialogue, items, and documents.

[Quest](Assets/_Project/Story/Quests) and [dialogue](Assets/_Project/Story/Dialogues) definitions are separate from scene controllers. Activities include office preparation, breakfast, finding colleagues, a shared meal, ATM use, supply purchases, and fuel-station activities. Available shop offers follow Chapter 1 activities; a general trading economy is not a complete system yet.

The journal supports **Main**, **Side**, and **Optional Quests**, including active, completed, and skipped states. The main quest follows story progress. Players can select one supporting quest to appear below it: **yellow** for main, **green** for side, and **blue** for optional. [QuestJournalCatalog](Assets/_Project/Resources/QuestJournalCatalog.asset) contains journal definitions.

Notifications are queued to avoid overlap. When a main-quest checkpoint changes, the quest notification finishes first, followed by saving and a slowly blinking autosave logo.

### NPCs and world population

[NpcPopulationManager](Assets/_Project/Scripts/Runtime/Npc/NpcPopulationManager.cs) manages spawn points, profile selection, pool reuse, and distance-based spawning/despawning. Activity points support walking, seated work, typing, cleaning, carrying documents, and guarding. Actors needed by missions, holding story orders, or in conversation receive protection according to their state.

The **15-NPC limit** applies to NPCs displayed by the scene's renderer manager, including mission NPCs. More actors may remain active. Hidden mission actors can be retained; reduced visibility does not automatically destroy mission participants.

[NpcProfile](Assets/_Project/Scripts/Runtime/Npc/NpcProfile.cs) includes identity, role, occupation, department, building context, routes, and `interactable`. Setting it to `false` disables direct player conversations while ambient conversations and greetings can still run. Conversation context varies by building and occupation; one-way greetings use trigger memory to avoid continuous repetition.

Passing NPCs use a **0–10** priority, default **1**. Lower values yield; equal priorities randomly select who yields for one encounter. The player does not use this NPC priority system. Navigation checks terrain, passages, doors, and NavMesh/links; reliable movement still depends on colliders, passage width, and scene navigation data.

### Doors and vehicles

[DoorInteractable](Assets/_Project/Scripts/Runtime/Interaction/DoorInteractable.cs) supports swinging, sliding, and roll-up doors, item locks, and passage checks. Player-opened doors schedule automatic closure after a random **4–7 seconds**; NPC or automatic-movement openings use **1 second**. An occupied passage delays closure. Each door still needs suitable components and configuration.

[VehicleKeyUse](Assets/_Project/Scripts/Runtime/VehicleKeyUse.cs) handles a car remote with a default **5-meter** range, the driver's door, and seating based on story conditions. [VehicleSignalFeedback](Assets/_Project/Scripts/Runtime/VehicleSignalFeedback.cs) provides indicator lights and assignable audio references.

[VehicleJourneyDirector](Assets/_Project/Scripts/Runtime/VehicleJourneyDirector.cs), [AutomaticVehicleDriver](Assets/_Project/Scripts/Runtime/AutomaticVehicleDriver.cs), and [VehicleRoutePath](Assets/_Project/Scripts/Runtime/VehicleRoutePath.cs) run the automatic office-to-fuel-station route and departure from the station. Players can look around while seated; travel progress and vehicle positions are included in save snapshots.

[FuelStationChapterDirector](Assets/_Project/Scripts/Runtime/Story/FuelStationChapterDirector.cs) manages station activities, dialogue/document choices, returning to the car, and the Chapter 1 ending. The village continuation is not connected as a complete chapter transition yet.

## Save, load, and main menu

[GameSessionController](Assets/_Project/Scripts/Runtime/Persistence/GameSessionController.cs) manages sessions and the menu:

| Option | Current behavior |
| --- | --- |
| New Game | Reloads the scene with a new game state |
| Continue | Resumes the snapshot already restored for the menu preview, after a fade |
| Load Game | Rereads the same autosave and reloads the active scene |

Without a valid save, **Continue** and **Load Game** are hidden. Gameplay controls are stopped while the main menu is open. The preview checks the view and nearby space; **Continue retains the adjusted position/view** shown in the preview. Multiple-slot selection and manual saving through a UI are not available yet.

Autosaves reside at `Application.persistentDataPath/autosave.json`. [GameSaveSnapshot](Assets/_Project/Scripts/Runtime/Persistence/GameSaveSnapshot.cs) and [GameWorldSnapshot](Assets/_Project/Scripts/Runtime/Persistence/GameWorldSnapshot.cs) include progress/branches, the journal, player and camera pose, items/documents, slot and held-item layout, doors, pickup/drop objects, greetings, story actors requiring persistence, and vehicle travel. Ordinary ambient population can be regenerated and is not all included in the save.

[ProtectedJsonSaveStore](Assets/_Project/Scripts/Runtime/Persistence/ProtectedJsonSaveStore.cs) writes a versioned JSON envelope using **AES-256-CBC**, a random IV, and **HMAC-SHA256**. Writes use a temporary file and atomic replacement; [GameSaveRepository](Assets/_Project/Scripts/Runtime/Persistence/GameSaveRepository.cs) can recover a valid `.bak` backup. This format deters casual editing and detects corruption. Keys are embedded in the application, so the protection is not resistant to all reverse engineering. Steam Cloud is not integrated.

## Time and localization

[TimeOfDayPreset](Assets/_Project/Scripts/Runtime/Environment/TimeOfDayPreset.cs) and [TimeOfDayApplier](Assets/_Project/Scripts/Runtime/Environment/TimeOfDayApplier.cs) control skybox, exposure/tint, sunlight or moonlight, ambient lighting, and fog. Seven presets are stored in [Environment](Assets/_Project/Environment):

| Preset | Time |
| --- | --- |
| Predawn | 05:00 |
| Morning | 07:00 |
| Late morning | 11:00 |
| Afternoon | 13:00 |
| Evening | 17:00 |
| Night | 20:00 |
| Midnight | 00:00 |

Presets are used by the menu and story; weather simulation and a free-running day/night cycle are not implemented.

[LocalizationTable.asset](Assets/_Project/Localization/LocalizationTable.asset) stores Indonesian and English text. [LocalizationManager](Assets/_Project/Scripts/Runtime/Localization/LocalizationManager.cs) manages keys, fallback, view updates, and language preferences through `PlayerPrefs`. The current asset default is Indonesian. The English README title is a translation; the project and code namespace remain `SaksiTerakhir`.

## Project structure and scenes

| Location | Contents |
| --- | --- |
| [Assets/_Project/Scripts/Runtime](Assets/_Project/Scripts/Runtime) | Player, interaction, inventory, story, NPCs, navigation, persistence, environment, and vehicles |
| [Assets/_Project/Scripts/Editor](Assets/_Project/Scripts/Editor) | Configuration, asset creation, and Editor inspection tools |
| [Assets/_Project/Tests](Assets/_Project/Tests) | EditMode and PlayMode tests |
| [Assets/_Project/Scenes](Assets/_Project/Scenes) | Gameplay, assembly, and photo-studio scenes |
| [Assets/_Project/Prefabs](Assets/_Project/Prefabs) | Player, NPCs, vehicles, pickups, and assembled environments |
| [Assets/_Project/Art](Assets/_Project/Art) | Models, materials, and textures for main content |
| [Assets/_Project/Items](Assets/_Project/Items) | Item/document definitions and icons |
| [Assets/_Project/Story](Assets/_Project/Story) | Quest and dialogue definitions |
| [Assets/_Project/Npc](Assets/_Project/Npc) and [Data/Npc](Assets/_Project/Data/Npc) | Office and population NPC profiles |
| [Assets/_Project/Resources](Assets/_Project/Resources) | Item and journal catalogs loaded at runtime |
| [Assets/_Project/Navigation](Assets/_Project/Navigation) | Environment navigation data |
| [Assets/_Project/UI](Assets/_Project/UI) | Fonts and UI assets |
| [Assets/ModularAsset](Assets/ModularAsset) | Building, road, vegetation, and equipment kits |
| [Assets/Settings](Assets/Settings) | Render pipeline and volume configuration |
| [Packages](Packages) / [ProjectSettings](ProjectSettings) | Dependencies and Unity configuration |
| [SourceArt](SourceArt) | Blender `.blend` sources |

| Scene | Purpose |
| --- | --- |
| [Regional Archive Office](Assets/_Project/Scenes/Regional%20Archive%20Office.unity) | Main office district and Chapter 1 gameplay scene |
| [Regional Vilage](Assets/_Project/Scenes/Regional%20Vilage.unity) | Village scene for further development; spelling follows the filename |
| [OfficeArchive_Assembly](Assets/_Project/Scenes/OfficeArchive_Assembly.unity) | Modular office assembly |
| [WarehouseArchive_Assembly](Assets/_Project/Scenes/WarehouseArchive_Assembly.unity) | Archive warehouse assembly |
| [RoadLoop_Assembly](Assets/_Project/Scenes/RoadLoop_Assembly.unity) | Road network and district assembly |
| [ItemShow](Assets/_Project/Scenes/ItemShow.unity) | Item-model photo studio for icons |

The environment includes a four-story archive office and rooftop, parking, six warehouses, loop roads and sidewalks, houses/shops, a canteen, office buildings, a fuel station, a park, and world-boundary vegetation. Some surrounding buildings serve as scenery with empty interiors. A door, lift, ATM, or shop model does not always imply that all its functionality is connected.

The modular kit is grouped into `Bangunan` (structures, houses, stilt houses, and fuel-station parts), `Lingkungan` (roads and vegetation), and `Perlengkapan` (equipment). Assembled environments are in [Prefabs/Environment](Assets/_Project/Prefabs/Environment). Blender sources are separated from imported assets to make Unity assets easier to locate.

## Editor tools

These menu entries come from existing code. Configuration tools can modify scenes/assets; save your work before running them and choose tools according to the task.

| `Saksi Terakhir` menu | Source / purpose |
| --- | --- |
| `Chapter One/Configure Active World` | [ChapterOneWorldSetup](Assets/_Project/Scripts/Editor/ChapterOneWorldSetup.cs): connects Chapter 1 content in the active world |
| `Inventory/Hubungkan UI Inventory` | [InventorySceneSetup](Assets/_Project/Scripts/Editor/InventorySceneSetup.cs): connects inventory UI |
| `Inventory/Buat Catalog dan Prefab Pickup` | [InventoryItemAssetSetup](Assets/_Project/Scripts/Editor/InventoryItemAssetSetup.cs): prepares the catalog and pickups |
| `Inventory/Periksa Catalog dan Prefab Pickup` | Checks references using the same tool |
| `NPC/Set Up World Population` | [WorldNpcSceneSetup](Assets/_Project/Scripts/Editor/WorldNpcSceneSetup.cs): sets up NPC population |
| `Doors/Configure Active Scene` | [WorldDoorSetup](Assets/_Project/Scripts/Editor/WorldDoorSetup.cs): configures doors |
| `Vehicles/Connect Office To SPBU Journey` | [VehicleJourneySetup](Assets/_Project/Scripts/Editor/VehicleJourneySetup.cs): connects the vehicle route |
| `Connect Fuel Station Chapter Ending` | [FuelStationChapterSetup](Assets/_Project/Scripts/Editor/FuelStationChapterSetup.cs): connects station activities and the ending |
| `Create Time Of Day Presets` / `Time Of Day/Configure Active Scene` | [TimeOfDaySetup](Assets/_Project/Scripts/Editor/TimeOfDaySetup.cs): creates and applies time presets |
| `ItemShow/Buat Ulang Studio Foto` | [ItemShowPhotoStudioSetup](Assets/_Project/Scripts/Editor/ItemShowPhotoStudioSetup.cs): rebuilds the item photo studio |

Menu names above preserve their exact strings in the Editor, including Indonesian entries.

## Testing and change guidelines

[Editor tests](Assets/_Project/Tests/Editor) cover inventory state, item use/drop, quests/conditions, saves, menus, doors, NPCs/navigation, seating, and vehicle travel. [PlayMode tests](Assets/_Project/Tests/PlayMode) cover the Chapter 1 office flow, session bootstrap, and inventory physics.

Open **Window > General > Test Runner** to run the **EditMode** or **PlayMode** suites. File-reference checks and successful compilation do not replace gameplay testing. Test large scenes using one active Editor to avoid additional memory load.

When changing code, use intent-revealing names, controlled Inspector configuration, and clear component responsibilities. Preserve item/quest IDs, localization keys, save compatibility, GUIDs, and scene/prefab references. Separate data rules from presentation where it improves maintainability; UI and layout changes should respect developer-authored scenes.

## Repository hygiene and documentation

[.gitignore](.gitignore) excludes Unity caches, build outputs, generated IDE files, private agent configuration, crash dumps, Python caches, and Blender backups. Source assets and `.meta` files, `Packages`, `ProjectSettings`, `.blend` sources, and both READMEs remain eligible for version control. Changing ignore rules does not automatically remove already-tracked files from Git.

Story documentation, code notes, and document-generation tools are stored **outside the checkout**. In the developer workspace, their location is `/home/ikbal/Dokumen/Dokumentasi Saksi Terakhir`. Temporary files go in `/tmp`. The project's `SourceArt` contains only Blender sources. The external documentation folder is not a runtime dependency for opening the game; these Indonesian and English READMEs remain at the repository root.

## License

There is no general project code/asset license file at the repository root yet. Third-party font licenses remain in [UI/Fonts/Licenses](Assets/_Project/UI/Fonts/Licenses). Check permission with the project owner before using or distributing code and assets outside this project.
