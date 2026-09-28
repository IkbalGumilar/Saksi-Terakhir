# Chapter One Cinematic Revision — Implementation Plan

> **For Codex:** Execute this plan task by task with TDD. This plan is approved by the user for direct implementation.

**Spec:** `docs/superpowers/specs/2026-09-27-chapter-one-cinematic-revision-design.md`

**Working-copy ruling:** The active feature branch has an uncommitted scene plus Settings Panel helper files authored by the user. Work in this feature checkout so the actual scene context is preserved. Stage only the generated chapter-one blocks and never stage or edit the Settings helper files.

## Task 1: Automatic dialogue and speaker focus

**Files:** `StoryDialogueController`, `StoryDialogueView`, player camera/controller, focused editor tests.

1. Add failing tests for automatic story progression, consumed E input, duration scaling, and combined dialogue/cinematic locks.
2. Implement text-length timing, a typewriter view, independent movement/look locks, and a focus target API.
3. Run focused EditMode tests.

## Task 2: Cinematic motion and final-meeting state

**Files:** new story motion/seating helpers, `ChapterOneProgress`, `ChapterOneDirector`, story tests.

1. Add failing tests for the stationary pre-walk exchange, delayed last-colleague movement, meeting assembly state, and reload-safe seat selection.
2. Implement player cutscene follow, persistable meeting flags, and director transitions.
3. Run focused Editor and PlayMode tests.

## Task 3: Scene anchors, dialogue assets, and notification layout

**Files:** `ChapterOneSceneSetup`, explicit dialogue assets, narrow UI helper, scene/UI validation tests.

1. Add failing scene/UI validation for edit-friendly anchors and disjoint quest/notice layout.
2. Create/wire anchors, boss relocation behavior, pre-walk and seating dialogue assets, and compact notice layout.
3. Invoke scene setup in batch and inspect the generated scene serialization.

## Task 4: Integration verification and review

1. Update integration helpers so they explicitly advance automatic dialogue in tests.
2. Run all EditMode and PlayMode tests, then inspect the full change as a fresh review.
3. Commit only the story implementation and synthetic staged scene blocks; leave Settings Panel changes unstaged.

## Review focus

- Story locks must never leave the player permanently disabled after completion or scene reload.
- Player auto-follow must preserve collision and NavMesh failure safety.
- Anchor-based placement must replace hard-coded executive room assumptions without overwriting user-authored UI.
- Save data must remain backward-compatible with existing chapter-one saves.
