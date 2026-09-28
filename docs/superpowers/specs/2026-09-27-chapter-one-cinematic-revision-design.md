# Chapter One Cinematic Revision

## Goal

Make the chapter-one story scenes play as readable, automatic cutscenes while preserving the existing quest order and the user's in-progress Settings Panel work.

## Required behavior

- Every chapter-one dialogue locks player movement and camera input. The current speaker receives the visual focus; other participants face that speaker.
- Dialogue types on per character and advances automatically. Its hold time scales with text length and is tunable in the Inspector for a later Settings connection. Pressing E during story dialogue is consumed but does not skip lines.
- The boss begins at the flag-office chair anchor. When the first colleague enters, the boss moves to a randomly selected front-sofa anchor.
- Interacting with the last colleague begins a short stationary exchange. After it completes, that colleague walks slowly to the boss room and the player follows under cutscene control until the colleague enters.
- After both colleagues arrive, Raka, Sinta, and the player occupy three meeting-seat anchors. The player only takes their seat after interacting with the boss and hearing the invitation. The four-person briefing then begins with speaker focus.
- The quest tracker stays upper-left. The existing `Notificatuion` panel becomes a compact upper-right notice and does not block input.

## Scene-placement decision

The executive room furniture, Indonesian flag, and seating are merged into `Office_ArchiveHQ_Executive`; they cannot be safely located by object name. The scene setup therefore creates named anchors below `Chapter One Story` so the correct flag-office positions are visible and editable in Unity: `Boss Chair`, `Boss Meeting Left`, `Boss Meeting Right`, `Raka Seat`, `Sinta Seat`, `Player Seat`, `Boss Door Wait`, and `Boss Room Zone`.

The current editor is closed and no live Unity Pipeline session is attached. The source setup will retain existing values only where a deliberate visual placement is needed, and it will never modify the user's Settings Panel helper files.

## Persistence and recovery

Story progress is session-only for the current prototype. Starting the game always creates fresh progress at the rooftop quest and ignores save files from older runs. Gameplay progress is not written automatically. The save-store utility remains available to isolated PlayMode test fixtures; it is not enabled during normal gameplay.

## Acceptance checks

1. Unit and scene tests prove dialogue consumes E without manual progression and reports duration from text length.
2. Integration tests prove the last colleague does not receive a destination until the stationary exchange completes.
3. Scene validation finds the new named anchors and the flag-office boss door setup.
4. UI validation verifies the tracker and notification rectangles cannot overlap at the authored Canvas size.
5. The Unity EditMode and PlayMode suites pass after scene setup and a fresh serialized-scene inspection.
