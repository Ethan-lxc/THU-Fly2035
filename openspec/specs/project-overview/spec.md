# Project Overview

## ADDED Requirements

### Requirement: Repository layout is documented for contributors

The documentation SHALL list primary folders under `Assets/` and state whether each is gameplay-critical, UI-only, or editor/tooling.

#### Scenario: Contributor reads overview before changing gameplay

- **WHEN** a contributor opens the project-overview spec
- **THEN** they SHALL find `UI/`, `Quest/`, `Drone/`, `camera/` described as gameplay-related areas and `MCPForUnity/` described as editor/MCP tooling not required for player builds

### Requirement: Main scene and bootstrap entry are identifiable

The documentation SHALL name the primary gameplay scene (`SampleScene` or successor) and any bootstrap objects that load quest/UI state (e.g. medicine quest bootstrap).

#### Scenario: AI searches for scene entry

- **WHEN** a contributor needs to find where to place scene objects for HUD or quest
- **THEN** they SHALL find reference to `Assets/Scenes/SampleScene` (or documented replacement) and quest-related bootstrap hooks under `Assets/Quest/`

### Requirement: Sorting layers vs UI canvas order are not conflated

The documentation SHALL state that 2D `Sorting Layer` / `Order in Layer` apply to `SpriteRenderer`/`Tilemap`, while screen UI stacking uses `Canvas.sortingOrder` and sibling order.

#### Scenario: Fixing z-order confusion

- **WHEN** a contributor confuses pause HUD with sprite draw order
- **THEN** they SHALL find explicit separation between world 2D sorting and UI canvas sorting in project overview or linked UI spec
