# Drone and Camera

## ADDED Requirements

### Requirement: Drone movement respects gameplay input lock

The drone controller SHALL NOT advance movement or pathing while `GameplayInputLock.BlockPlayerMove` is true.

#### Scenario: Dialogue blocks drone

- **WHEN** `GameplayInputLock.BlockPlayerMove` is true
- **THEN** `IsoDroneController` SHALL stop active waypoint movement and SHALL NOT start new pathing from input

### Requirement: Drone uses isometric-style path decomposition

The drone SHALL compute axis-aligned path segments from click target using the project’s axis decomposition (waypoint then final target) as implemented in `IsoDroneController`.

#### Scenario: Click to move

- **WHEN** user clicks with gameplay unlocked and `IsoDroneController` receives input
- **THEN** the drone SHALL move toward the computed waypoint and then toward the target position using `FixedUpdate` velocity rules

### Requirement: Camera follows player target

The main gameplay camera SHALL follow the designated player transform via `CameraFollow` (or equivalent) when configured in scene.

#### Scenario: Player moves

- **WHEN** player transform changes position
- **THEN** camera SHALL update to maintain follow offset as implemented in `CameraFollow`
