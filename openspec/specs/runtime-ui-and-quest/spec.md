# Runtime UI and Medicine Quest

## ADDED Requirements

### Requirement: HUD canvas resolution follows shared UI utility

The HUD and related runtime UI SHALL obtain or create a root `Canvas` via `GameUiCanvasUtility.ResolveCanvasForUi` and ensure an `EventSystem` exists when UI interactions are required.

#### Scenario: Pause or HUD shares canvas

- **WHEN** `GameHudUI`, `InGameHudUI`, `PauseMenuUI`, or `MedicineQuestDialogueUI` builds UI at runtime
- **THEN** they SHALL use `GameUiCanvasUtility` for canvas resolution and `EnsureEventSystemExists` where clicks are needed

### Requirement: Medicine quest state is persisted and observable

The medicine quest SHALL use `MedicineQuestState` (PlayerPrefs-backed) for accepted, has-medicine, and complete flags, and expose change events for UI refresh.

#### Scenario: Quest completion hides world hints

- **WHEN** `MedicineQuestState.IsComplete` is true
- **THEN** NPC bulb hint (`NpcMedicineQuest`) SHALL not show as available interaction for quest progression

### Requirement: Pause and dialogue Esc handling is ordered

The system SHALL ensure `MedicineQuestDialogueUI` can consume Esc before `PauseMenuUI` opens pause when dialogue is active.

#### Scenario: Esc during dialogue

- **WHEN** dialogue flow is not hidden and user presses Esc
- **THEN** `MedicineQuestDialogueUI.TryHandleEscapeBeforePause` SHALL run first and pause menu SHALL NOT open until dialogue closes

### Requirement: Progress save hooks exist for exit

Exiting from pause menu SHALL call `GameProgressSave.SaveAll` (or equivalent) so quest state is persisted.

#### Scenario: Exit from pause

- **WHEN** user confirms exit from pause menu
- **THEN** `GameProgressSave.SaveAll` SHALL be invoked before application quit
