# Chapter One Main Quest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Make the office scene playable from the rooftop conversation through collecting the car key and reaching the SUV, without entering the vehicle.

**Architecture:** ScriptableObject assets own fixed quest and dialogue content; a serializable progress model owns mutable facts and an office-scene director coordinates actors and transitions. Existing interaction, NPC navigation, localization, and uGUI components receive narrow story hooks; one editor setup command wires the authored scene and checks its references.

**Tech Stack:** Unity 6000.3.24f1, C#, uGUI/TextMeshPro, Input System, AI Navigation 2.0.11, NUnit Edit Mode tests, JSON via `JsonUtility`.

**Spec:** `docs/superpowers/specs/2026-09-26-chapter-one-main-quest-design.md`

## Global Constraints

- Keep all 25 existing NPCs. Bima `NPC-021` and Yuni `NPC-022` remain rooftop workers; Raka `NPC-003` begins at the lobby; Sinta `NPC-004` begins on floor 2.
- Arya `NPC-006` is the seated boss and Nadia `NPC-002` gives the key. Do not add player level or XP.
- All chapter quests are `Main`, use a yellow dot, and replace the previous main quest at each stage. During `FindColleagues`, display A then B and strike through each only after that NPC's dialogue completes.
- First colleague walks to the boss at `4 m/s`; last colleague leads at `2.2 m/s`, waits when the player exceeds `5 m`, and finishes dialogue before entering the boss room.
- Fictional clue: **Provinsi Arunika, ujung selatan Kecamatan Tanjung Sagara**. Car key ID: `car-key`. Story stops when the keyed player reaches the SUV.
- Preserve the existing generic three-line NPC dialogue, yellow interaction sphere, ordinary doors, player camera, SUV physics, and unrelated settings UI.
- The scene and `SettingsHeaderLayoutSetup.cs` are already dirty/untracked from earlier work; inspect diffs and stage only task-owned files in each commit. Do not regenerate all 25 NPCs or overwrite these unrelated edits.

## Review Focus

- E pressed while a story line is visible must advance only that line, never also open a door or start generic NPC dialogue (Task 2 test).
- Duplicate NPC interaction, call, or key event after save/load must not repeat a reward or skip a stage (Tasks 1 and 6 tests).
- Reaching the boss door with zero or one colleague must keep the player out and name the missing colleague correctly (Task 4 test).
- First colleague still en route when the last reaches the door must delay the final briefing (Tasks 1 and 3 tests).
- Missing or partial NavMesh path must leave the NPC waiting and the quest unchanged, with a diagnostic (Task 3 test).

---

### Task 1: Quest data and deterministic chapter state

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Story/QuestDefinition.cs`, `DialogueSequence.cs`, `ChapterOneProgress.cs`
- Modify: `Assets/_Project/Scripts/Runtime/Npc/NpcProfile.cs`
- Test: `Assets/_Project/Tests/Editor/ChapterOneProgressTests.cs`

**Interfaces:** `QuestDefinition` exposes `Id`, `Category`, `TitleKey`, and ordered objective keys; `DialogueSequence` exposes `Id` and ordered `(speakerId, text)` lines. `NpcProfile` exposes story quest/dialogue references without changing `Configure(...)`. `ChapterOneProgress` exposes `Stage`, `RakaMet`, `SintaMet`, `FirstColleagueId`, arrival flags, `HasCarKey`, and `TryApply(ChapterOneEvent kind, string actorId = "") : bool`.

- [x] **Step 1: Write failing NUnit tests.** `NewGameStartsOnRooftop`, `RakaThenSintaAndReverseOrderAreBothValid`, `DuplicateAndOutOfOrderEventsDoNothing`, and `FinalBriefingWaitsForBothArrivalAndWalkDialogue`; assert all eight stage names, actor IDs, and one-time transitions from the spec.
- [x] **Step 2: Run the new Edit Mode tests.** Use Unity Test Runner or the batch command in Verification; expect failures because the types do not exist.
- [x] **Step 3: Implement the data types and progress transitions.** `ChapterOneStage` values: `MeetRooftopWorkers`, `AnswerBossCall`, `MeetBoss`, `FindColleagues`, `EscortLastColleague`, `FinalBossBriefing`, `CollectCarKey`, `ReachVehicle`, `Complete`. Events: rooftop/call/first briefing/colleague dialogue started and finished/arrival/walk dialogue finished/final briefing/key/vehicle. Starting the last colleague's walking dialogue begins `EscortLastColleague`, but their A/B objective remains incomplete until its final line; the tracker retains both rows during the walk and strikes the last row before changing to the boss briefing. Reject wrong actor IDs and repeated events; `VehicleReached` requires `HasCarKey`.
- [x] **Step 4: Run the tests; expect pass.** Add a data-validation test for missing IDs/duplicate objective keys.
- [x] **Step 5: Commit only the new state/data code and tests.**

### Task 2: Story dialogue and exclusive E input

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Story/StoryDialogueController.cs`
- Modify: `Assets/_Project/Scripts/Runtime/Interaction/PlayerInteractor.cs`, `Assets/_Project/Scripts/Runtime/Player/PlayerController.cs`
- Test: `Assets/_Project/Tests/Editor/StoryDialogueControllerTests.cs`, `NpcInteractableTests.cs`

**Interfaces:** `StoryDialogueController.Play(DialogueSequence sequence, Action onComplete, bool autoAdvance = false) : bool`, `TryConsumeInteract() : bool`, `SetAutoAdvancePaused(bool paused) : void`, `IsPlaying`, and `LineChanged`. `PlayerController.SetStoryMovementLocked(bool locked) : void`. The NPC-to-director hook is added with the director in Task 5.

- [x] **Step 1: Write failing tests.** Assert the first E press during story dialogue advances exactly one line and leaves a nearby ordinary door closed; completion unlocks player movement; generic NPC dialogue still cycles as before; repeated E after completion is available to normal interaction.
- [x] **Step 2: Run the targeted tests; expect failures.**
- [x] **Step 3: Implement dialogue playback and input priority.** `PlayerInteractor` asks `TryConsumeInteract()` before invoking `current.Interact`; preserve generic NPC interactions. Lock translation/jump/crouch during standing/phone/boss dialogue while retaining camera look. Auto-advance walking lines on a readable timer; expose pause/resume for Task 3.
- [x] **Step 4: Run targeted tests and existing door/NPC interaction tests; expect pass.**
- [x] **Step 5: Commit only dialogue/input changes and tests.**

### Task 3: NavMesh story orders and conversation facing

**Files:**
- Modify: `Assets/_Project/Scripts/Runtime/Npc/OfficeNpcAgent.cs`, `NpcActivityPoint.cs`, `OfficeNpcDirector.cs`
- Test: `Assets/_Project/Tests/Editor/OfficeNpcStoryOrderTests.cs`

**Interfaces:** `OfficeNpcAgent.HoldForStory(Transform faceTarget = null) : void`, `ReleaseStoryHold() : void`, `TrySetStoryDestination(Vector3 target, float speed) : bool`, `StoryAtDestination : bool`, `SetStoryFacing(Transform target) : void`, `SetStoryFollowDistance(Transform player, float maximumDistance) : void`. Story orders take precedence over patrol/social chat and restore the profile speed afterward.

- [x] **Step 1: Write failing tests.** Holding Bima/Yuni/Raka/Sinta before `Start` prevents an initial patrol step; facing updates toward the active speaker; speed is `4` for the first colleague and restored afterward; a leader waits beyond `5 m`; an incomplete path returns false and does not mark arrival.
- [x] **Step 2: Run targeted tests; expect failures.**
- [x] **Step 3: Add explicit hold, path-checked move, follow-distance pause, arrival, and release states to `OfficeNpcAgent`.** Validate a complete `NavMeshPath` before accepting a destination, use the existing door-ahead helper, retry failed paths without teleporting or firing arrival, and log a bounded diagnostic. Suppress ambient conversations for story-held actors and background actors barred from the boss room.
- [x] **Step 4: Run targeted tests and existing NPC verification; expect pass.**
- [x] **Step 5: Commit only NPC control changes and tests.**

### Task 4: Boss office gate and room occupancy

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Story/BossOfficeGate.cs`
- Modify: `Assets/_Project/Scripts/Runtime/Interaction/DoorInteractable.cs`, `Assets/_Project/Scripts/Runtime/Npc/OfficeNpcDirector.cs`
- Test: `Assets/_Project/Tests/Editor/BossOfficeGateTests.cs`, `DoorInteractableTests.cs`

**Interfaces:** `BossOfficeGate.Configure(ChapterOneProgress progress, DoorInteractable door) : void`, `CanOpenFor(Transform actor) : bool`, `BlockedPromptKey : string`, and `Blocked` event; `DoorInteractable.ForceOpen() : void` and `ForceClose() : void` preserve its normal swing. The director subscribes to `Blocked` in Task 5. The gate has a scene zone/corridor exit anchor for evacuating background NPCs through NavMesh.

- [x] **Step 1: Write failing tests.** Ordinary doors retain their old behavior; the first boss visit is allowed; after leaving, zero/one colleague cannot reopen the door; the blocked prompt names Raka or Sinta; invited NPC passage does not grant player access; a player inside is never locked in.
- [x] **Step 2: Run targeted tests; expect failures.**
- [x] **Step 3: Implement gate checks in the existing door, not a second competing interactable.** Keep `CanInteract` true while story-locked so the sphere/prompt still detects it, but deny opening on E and raise `Blocked` at most once per approach; Task 5 supplies Arya's brief reprimand. Clear the room via NavMesh exit targets and exclude `executive_talk` from background routes for this chapter.
- [x] **Step 4: Run targeted tests; expect pass.**
- [x] **Step 5: Commit only gate/door changes and tests.**

### Task 5: Chapter director and branching dialogue

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Story/ChapterOneDirector.cs`, `Assets/_Project/Scripts/Runtime/Story/VehicleArrivalZone.cs`
- Create: `Assets/_Project/Story/Quests/*.asset`, `Assets/_Project/Story/Dialogues/*.asset`
- Modify: six story `NpcProfile` assets for Bima, Yuni, Raka, Sinta, Arya, Nadia; `Assets/_Project/Scripts/Runtime/Npc/NpcInteractable.cs`, `Assets/_Project/Scripts/Runtime/Interaction/PlayerInteractor.cs`
- Test: `Assets/_Project/Tests/Editor/ChapterOneDirectorTests.cs`

**Interfaces:** `ChapterOneDirector.TryBeginNpcDialogue(NpcInteractable npc, Transform actor) : bool`, `TryAnswerBossCall() : bool`, `OnStoryActorArrived(string npcId) : void`, `TryReachVehicle(Transform actor) : bool`, `Progress : ChapterOneProgress`, and `ProgressChanged` event. `NpcInteractable.SetStoryDirector(ChapterOneDirector director) : void` delegates story-owned interactions but retains generic dialogue otherwise. `VehicleArrivalZone` reports the player entering the SUV's `2 m` interaction radius; no vehicle entry call is made.

- [x] **Step 1: Write failing tests.** Bima or Yuni can trigger the same rooftop scene once; the call starts only afterward; Arya first/last briefing occur at the correct stages; Raka/Sinta can be found in either order; the first walks alone at `4 m/s`; the second gets the correct personal walk dialogue; Nadia gives one key only; vehicle arrival completes only with key.
- [x] **Step 2: Run targeted tests; expect failures.**
- [x] **Step 3: Implement director orchestration against Tasks 1–4 interfaces.** Hold the four opening actors in `Awake`; map ID-based story offers from their `NpcProfile`; make `PlayerInteractor` route E to `TryAnswerBossCall()` after active dialogue and before world interaction; direct the speaker-facing trio; keep first colleague at the boss room; for the last, walk at `2.2 m/s`, pause beyond `5 m`, wait at the door until dialogue and first colleague's arrival, then open and seat them. Show both A/B objective rows during the last walk and mark the last only when its dialogue finishes. Do not move other NPCs to replace the two rooftop workers.
- [x] **Step 4: Author ScriptableObject content.** Use stable quest IDs for the eight stages and dialogue IDs for rooftop (16–20 lines), phone (4–6), first briefing (14–18), first Raka/Sinta (6–8 each), last Raka/Sinta (12–16 each), final briefing (18–22), and Nadia (5–7). All text is Indonesian and the final briefing carries the exact fictional place names from Global Constraints.
- [x] **Step 5: Run targeted tests plus an asset integrity test for speaker IDs, line counts, references, and stage-to-sequence mapping; expect pass.**
- [x] **Step 6: Commit only director/content changes and tests.**

### Task 6: Quest HUD, call notice, and localized prompts

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Story/QuestTrackerView.cs`, `StoryDialogueView.cs`, `StoryNotificationView.cs`
- Modify: `Assets/_Project/Scripts/Runtime/Interaction/InteractionPromptView.cs`, `Assets/_Project/Localization/LocalizationTable.asset`
- Test: `Assets/_Project/Tests/Editor/ChapterOneUiTests.cs`

**Interfaces:** `QuestTrackerView.Render(QuestDefinition quest, ChapterOneProgress progress) : void`, `StoryDialogueView.Render(DialogueSequence sequence, int lineIndex) : void`, `StoryNotificationView.Show(string textKey, float seconds) : void`. Views subscribe to director/dialogue events; story dialogue does not reuse or disable the generic NPC panel permanently.

- [x] **Step 1: Write failing tests.** Main quest dot is yellow; during `FindColleagues`, Raka then Sinta remain ordered and only completed rows contain TMP strike-through; changing stage replaces old quest; story dialogue hides the generic prompt, then restores it; blocked door prompt contains the missing name.
- [x] **Step 2: Run targeted tests; expect failures.**
- [x] **Step 3: Implement views and localized UI keys.** Use the existing `Save Area/Notificatuion` for temporary quest/call messages, a separate persistent tracker, and a separate TMP story dialogue panel. Add Indonesian and English localization entries for fixed UI chrome/prompts; ScriptableObject dialogue text remains the Indonesian authored story.
- [x] **Step 4: Run targeted tests; expect pass.**
- [x] **Step 5: Commit only view/localization changes and tests.**

### Task 7: Save/load and one-time car key

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Story/ChapterOneSaveStore.cs`, `CarKeyInventory.cs`
- Modify: `Assets/_Project/Scripts/Runtime/Story/ChapterOneDirector.cs`
- Test: `Assets/_Project/Tests/Editor/ChapterOneSaveTests.cs`

**Interfaces:** `ChapterOneSaveStore.Save(ChapterOneProgress progress, string path) : void`, `Load(string path) : ChapterOneProgress`; save version is `1`. `CarKeyInventory.Has(string itemId) : bool`, `TryAdd(string itemId) : bool` accepts only `car-key` for this chapter.

- [x] **Step 1: Write failing tests.** JSON round trip preserves either colleague order, stage, arrivals, and key; repeated Nadia interaction does not duplicate the key; malformed/unknown-version JSON starts a fresh chapter with an error log; a loaded call or finished dialogue is not replayed.
- [x] **Step 2: Run targeted tests; expect failures.**
- [x] **Step 3: Persist progress to `Application.persistentDataPath` on actual transitions and restore NPC orders, gate, and HUD from loaded state.** Use a temporary file plus replacement for each save; never mutate ScriptableObject assets or start quests from an item pickup.
- [x] **Step 4: Run targeted tests; expect pass.**
- [x] **Step 5: Commit only persistence changes and tests.**

### Task 8: Office scene assembly and Play Mode verification

**Files:**
- Create: `Assets/_Project/Scripts/Editor/ChapterOneSceneSetup.cs`, `Assets/_Project/Tests/Editor/ChapterOneSceneValidationTests.cs`
- Modify: `Assets/_Project/Scenes/Regional Archive Office.unity`
- Optional only if the visual audit finds no sofa: add a simple sofa prefab under `Assets/_Project/Art/Generated/Story/`

**Interfaces:** `ChapterOneSceneSetup.Ensure() : string` is idempotent and touches only story-owned objects/references. It wires existing actors, boss door, visible SUV, three seat anchors, corridor exit, `Notificatuion`, tracker, phone, and dialogue panels. The validation test inspects the saved office scene, not a synthetic scene.

- [x] **Step 1: Write failing scene validation.** Assert exactly 25 NPC profiles and existing navigation surface/links remain, six story IDs resolve uniquely, all scene references are assigned, boss gate is on the correct door, seat anchors and SUV radius are plausible, and no second interaction input handler was added.
- [x] **Step 2: Run validation; expect missing chapter objects/references.**
- [x] **Step 3: Implement and run `Ensure()` once in the office scene.** Inspect the current scene diff first and preserve unrelated user/header changes. Visually place Arya and both colleague seat anchors on the actual executive furniture; add a simple sofa only if needed. Keep NPC routes and vehicle physics untouched except for the story gate/arrival marker.
- [x] **Step 4: Run Edit Mode suite and scene validation; expect pass.** Run Play Mode from a new game through both colleague orders, deliberate one-colleague door attempt, `5 m` follow pause, boss room clearance, save/load midquest, Nadia key, and SUV arrival. Capture Console and visual evidence; a static parse or Edit Mode pass alone is insufficient.
- [x] **Step 5: Inspect diff, stage only chapter-owned scene hunks (not the pre-existing settings edit), confirm the unrelated header script remains unstaged, and commit the chapter scene/editor/test changes.**

## Verification command

Use `/home/ikbal/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -quit -projectPath /home/ikbal/Unity/Projek/Saksi-Terakhir -runTests -testPlatform EditMode -testResults /tmp/saksi-chapter-one-editmode.xml -logFile /tmp/saksi-chapter-one-editmode.log`. A zero exit status plus zero failures in the XML are required. Play Mode inspection must use the actual `Regional Archive Office` scene and record the visible outcome of each story branch.
