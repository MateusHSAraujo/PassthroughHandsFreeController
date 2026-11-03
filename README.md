# Passthrough Hands-Free Controller

A Mixed Reality (Passthrough) application for **Meta Quest** headsets that enables **hands-free navigation of a robotic wheelchair** using head gaze and dwell-time interactions, communicating with any **RestThru-compatible HTTP server**.

---

## Table of Contents

1. [Overview & Motivation](#1-overview--motivation)
2. [System Architecture](#2-system-architecture)
3. [Application State Machine (`ApplicationController`)](#3-application-state-machine-applicationcontroller)
4. [Hands-Free Gaze Interaction (Head Gaze + Dwell Time)](#4-hands-free-gaze-interaction-head-gaze--dwell-time)
5. [Wheelchair Spatial Tracking (`AnchorManager`)](#5-wheelchair-spatial-tracking-anchormanager)
6. [Closed-Loop Motion Control & RestThru Client](#6-closed-loop-motion-control--restthru-client)
7. [Development History](#7-development-history)
8. [Project Structure](#8-project-structure)
9. [Requirements & Setup](#9-requirements--setup)

---

## 1. Overview & Motivation

Users with severe motor impairments often cannot operate a conventional joystick to drive a powered wheelchair. This project provides a **Mixed Reality (MR)** interface running on **Meta Quest** headsets where the user sees their physical surroundings via **Passthrough** overlaid with spatial UI elements, commanding the wheelchair **exclusively through head orientation (*Head/Camera Gaze*) and fixation duration (*Dwell Time*)**.

The application acts as a high-level closed-loop controller and client that sends wheel velocity and stop requests over HTTP to any robotic wheelchair running a **RestThru** server interface.

---

## 2. System Architecture

The core scripts are located in `Assets/Scenes/PassthroughHandsFreeController/Scripts/` and are organized into four main subsystems:

1. **State Machine & Orchestration (`AplicationController/`)**:
   - Manages the user interaction lifecycle across menus, floor target selection, linear displacement, and continuous heading rotation.
2. **Spatial UI & Gaze Interaction (`UI/`, `CameraGazeCursor.cs`, `CursorController.cs`, `HitpointIndicator.cs`)**:
   - Positions canvases within the user's line of sight (`LineOfSightPositioner`) and translates head orientation (`CenterEyeAnchor`) into dwell-time selection events (`ToggleGazeInteractor` and `HitpointIndicator`).
3. **Wheelchair Pose Tracking (`AnchorManager.cs`)**:
   - Computes the 3D position and orientation of the wheelchair every frame (`LateUpdate`) from the two Quest Touch controllers mounted on the wheelchair's armrests.
4. **Closed-Loop Control & RestThru HTTP Client (`MovementSequenceController.cs`, `RestThruAPI.cs`, `RestThruServer.cs`)**:
   - Executes asynchronous closed-loop control laws (PI/PID) for point-to-point navigation (`GotoPoint2D`) and heading alignment (`AlignHeading2D`), sending `PUT /motion/vel2` and `PUT /motion/stop` commands to the wheelchair's RestThru server.

---

## 3. Application State Machine (`ApplicationController`)

[`ApplicationController`](Assets/Scenes/PassthroughHandsFreeController/Scripts/AplicationController/ApplicationController.cs) implements the *State* pattern (`IApplicationState` / `ApplicationState`), sharing scene references across states via [`StateContext`](Assets/Scenes/PassthroughHandsFreeController/Scripts/AplicationController/ApplicationState.cs).

```text
                     ┌──────────────────────────────────────┐
                     ▼                                      │
             ┌───────────────┐                              │
     ┌──────►│ MainMenuState │──────┐                       │
     │       └───────────────┘      │                       │
     │         (Move Button)   (Rotate Button)              │
     │               │              │                       │
(Menu Button)        ▼              ▼                       │
     │   ┌────────────────────────────────┐        ┌───────────────┐
     └───│ SelectingMovementTargetState   │◄───────│ RotatingState │
         └────────────────────────────────┘        └───────────────┘
                │                   ▲                       ▲
        (Target Confirmed)    (Cancel Rotation)     (Confirm Rotation)
                ▼                   │                       │
         ┌─────────────┐     ┌─────────────────────────┐    │
         │ MovingState │────►│ ConfirmingRotationState │────┘
         └─────────────┘     └─────────────────────────┘
```

| State | Class | Behavior |
| :--- | :--- | :--- |
| **Main Menu** | `MainMenuState` | Displays `MainMenuUI` in front of the user with two gaze-activated options: **Move** (transitions to `SelectingMovementTargetState`) or **Rotate** (transitions to `RotatingState`). |
| **Floor Target Selection** | `SelectingMovementTargetState` | Activates `CameraGazeCursor`. When the user fixates on a point on the floor mesh (`FLOOR_EffectMesh`), opens `SelecTargetUI` with options to **Confirm** (`MovingState`), **Cancel** (resumes cursor), or return to **Main Menu** (`MainMenuState`). |
| **Linear Movement** | `MovingState` | Renders the trajectory line (`TrajectoryLine`) and schedules `GotoPoint2D` on `RestThruAPI`. Once completed or aborted, transitions to `ConfirmingRotationState`. |
| **Post-Movement Rotation Prompt** | `ConfirmingRotationState` | Displays `ConfirmRotationUI` asking whether the user wants to adjust the wheelchair's final heading (`RotatingState`) or select a new movement target (`SelectingMovementTargetState`). |
| **Gaze-Guided Rotation** | `RotatingState` | Schedules `AlignHeading2D` on `RestThruAPI`, rotating the wheelchair in place to follow the user's head direction (`CenterEyeAnchor`) until the heading remains within tolerance for `TimeoutToCompletion`, then transitions back to `SelectingMovementTargetState`. |

---

## 4. Hands-Free Gaze Interaction (Head Gaze + Dwell Time)

To enable completely hands-free operation across Meta Quest devices:

- **UI Ray Redirection (`HandedInputSelector.cs`)**:
  - Binds `OVRInputModule.rayTransform` to `OVRCameraRig.centerEyeAnchor` every frame so that `OVRRaycaster` canvases respond directly to head movement.
- **Dwell-Time UI Toggles (`ToggleGazeInteractor.cs`)**:
  - Implements `IPointerEnterHandler` and `IPointerExitHandler`. When the head-gaze ray hovers over a `Toggle`, a countdown coroutine (`timeToToggle`, default `2.0s`) starts. Maintaining gaze until completion plays spatial audio feedback (`MetaXRAudioSource`) and triggers `toggle.isOn = true`.
- **Floor Point Selection (`CameraGazeCursor.cs`, `CursorController.cs`, `HitpointIndicator.cs`)**:
  - Casts a `Physics.Raycast` from `CenterEyeAnchor` against the room mesh generated by the *MR Utility Kit* (`EffectMesh`), accepting hits only on `FLOOR_EffectMesh`.
  - While the gaze hit point remains within `distanceThreshold`, `HitpointIndicator` scales up uniformly until reaching `maxScale` over `hitpointTimeToTrigger` seconds (or immediately if button `A` is pressed for debugging). Upon completion, `SelecTargetUI` opens at the selected point and scales proportionally to the viewing distance (`Mathf.Clamp(scaleFactor * distance, minScale, maxScale)`).

---

## 5. Wheelchair Spatial Tracking (`AnchorManager`)

To close the navigation control loop in the Meta Quest coordinate frame:

- **Touch Controllers as Spatial Anchors**:
  - The left (`LeftAnchor`) and right (`RightAnchor`) Touch controllers are mounted in 3D-printed stands attached to the wheelchair's armrests.
  - In `AnchorManager.LateUpdate()`, the wheelchair's orthonormal basis (`WheelchairRight`, `WheelchairUp`, `WheelchairFoward`) and center position (`WheelchairPosition`, adjusted by `BackwardsOffset`) are computed from the poses of both controllers.
- **Controller Keep-Alive (`ApplicationController.VibrateRoutine`)**:
  - Because the controllers remain stationary in their mounts without human hand contact, the headset OS would normally put them to sleep. `ApplicationController` runs a background coroutine that sends a minimal vibration pulse (`OVRInput.SetControllerVibration(1, 0.001f, OVRInput.Controller.RTouch)`) every 2 seconds to keep 6DoF tracking active continuously.

---

## 6. Closed-Loop Motion Control & RestThru Client

[`RestThruAPI`](Assets/Scenes/PassthroughHandsFreeController/Scripts/RestThruAPI.cs) schedules asynchronous operations (`async/await` with `CancellationTokenSource`) across three states (`IDLE`, `BUSY`, `EMERGENCY_STOP`), communicating with the robotic wheelchair server via [`RestThruServer`](Assets/Scenes/PassthroughHandsFreeController/Scripts/RestThruServer.cs).

### Emergency Stop
- At any point while an operation is active (`State == APIStates.BUSY`), pressing **X and Y** simultaneously (`OVRInput.RawButton.X && OVRInput.RawButton.Y`) cancels the active `CancellationToken` and invokes `ForceStopRobot()` (up to 3 retries of `PUT /motion/stop`).

### 1. Point-to-Point Navigation (`GotoPoint2D`)
Executes in two stages on the horizontal XZ plane:
1. **Initial Heading Alignment**:
   - Computes the signed angle (`Vector2.SignedAngle`) between `RobotTransform.forward` and the vector toward `TargetPosition`.
   - If `|Angle| > HeadingTolerance` (default `5°`), commands an in-place rotation (`PUT /motion/vel2` with `(+Vrot, -Vrot)` or `(-Vrot, +Vrot)`) until aligned.
2. **Straight-Line Path Tracking (PI Lateral Distance + P Heading Controller)**:
   - Defines the ideal trajectory vector from `InitialPos` to `TargetPosition`.
   - At each control period `dt` (compensating for HTTP round-trip latency measured via `Stopwatch`), computes:
     - `errorHead`: angular error (in radians) between the robot's current forward vector and the ideal trajectory direction.
     - `errorDist`: perpendicular lateral distance to the ideal trajectory line (signed via 2D cross product and smoothed with an exponential moving average `0.4 * prev + 0.6 * current`).
     - `errorInt`: accumulated integral of `errorDist` with anti-windup clamping (`[-20, 20]`).
   - Computes differential wheel velocities:
     $$\Delta = K_{dist} \cdot e_{dist} - K_{head} \cdot e_{head} + K_{int} \cdot e_{int}$$
     $$V_R = V_{lin} - \Delta, \quad V_L = V_{lin} + \Delta$$
   - **Near-Target Deceleration**: when the remaining distance is below `5 * TravelTolerance`, halves `Vr`, `Vl`, `Kdist`, and `Khead` and quadruples the control loop frequency (`currdt = dt * 1000 / 4`).
   - Stops the wheelchair (`ForceStopRobot()`) once `CurrentDistance.magnitude < TravelTolerance` (default `0.1 m`).

### 2. Gaze-Guided Heading Alignment (`AlignHeading2D`)
- Rotates the wheelchair in place so `RobotTransform.forward` aligns with `TransformToFollow.forward` (`CenterEyeAnchor`).
- **Hybrid Bang-Bang + PID Controller**:
  - For large errors (`|headingErr| > 30°`): applies maximum rotational speed (`±maxVrot`) and resets integral/derivative terms.
  - For fine alignment (`|headingErr| <= 30°`): applies exponential smoothing and computes $\Delta = K_p \cdot e + K_i \int e + K_d \frac{de}{dt}$ clamped to `[-maxVrot, maxVrot]`, commanding `Vr = +Delta` and `Vl = -Delta`.
- **Stability Confirmation**:
  - Once the heading error enters `HeadingTolerance` (default `10°`), stops the wheelchair and starts filling the `RotationUI` (`RadialFill`) progress indicator. Holding the gaze within tolerance for `TimeoutToCompletion` (default `3s`) completes the operation; looking away resets the progress bar and resumes rotation.

### RestThru HTTP Endpoints Used (`RestThruServer.cs`)
- `PUT /motion/vel2` — JSON Body: `{"vel2": {"right": <float_mm_s>, "left": <float_mm_s>}}`
- `PUT /motion/stop` — JSON Body: `{}`

---

## 7. Development History

| Period | Milestone |
| :--- | :--- |
| **Jul 2025** (`108df54` – `126401f`) | Initial project structure, floor gaze raycasting (`FLOOR_EffectMesh`), progressive scale selection (`HitpointIndicator`), custom logging wrapper (`DebugLogger`), and UI audio/animation feedback. |
| **Aug–Sep 2025** (`0b3c434` – `fc726a8`) | Early experiments with **QR Code tracking** on the wheelchair armrests (evaluating ZXing/Passthrough Camera and MRUK QR Code tracking) and introduction of the central state controller. |
| **Oct 2025** (`ab838e0`) | Replaced QR code tracking with **Touch controllers mounted as spatial anchors** (`AnchorManager`), enabling rapid iteration over Meta Quest Link. Added `RestThruServer`, closed-loop `GotoPoint2D`, and `<control_info>` telemetry analysis (`logs_analyzer.ipynb`). |
| **Oct–Nov 2025** (`7955294` – `f195012`) | Refactored `ApplicationController` into formal state classes (`MainMenuState`, `SelectingMovementTargetState`, `MovingState`, `ConfirmingRotationState`, `RotatingState`), implemented `AlignHeading2D`, added `RotationUI`, and added architecture diagrams. |
| **Nov 2026** (`d737c4c` + `upgrade-unity`) | RestThru client fixes (`PUT /motion/stop` body and `AlignHeading2D` loop delay), upgrade to **Unity 6.6 (`6000.6.7f1`)**, and **Meta XR SDK `207.0.1`** with Unity MCP and Meta XR Operator integration. |

---

## 8. Project Structure

```text
Assets/Scenes/PassthroughHandsFreeController/
├── Diagrams/                  # Architecture and state machine diagrams (.asta, .dio)
├── Logs/                      # Experimental control logs (<control_info>) and logs_analyzer.ipynb
├── Materials/                 # Scene and cursor materials
├── Prefabs/                   # UI Canvas prefabs, cursors, and wheelchair visualization prefabs
├── Scripts/
│   ├── AplicationController/  # State machine (ApplicationController, StateContext, States/*)
│   ├── UI/
│   │   ├── Assets/            # Interaction components (ToggleGazeInteractor, RadialFill, LineOfSightPositioner, LaserPointer)
│   │   └── Canvas/            # Canvas controllers (MainMenuUI, SelectTargetUI, RotationUI, ErrorMessageUI)
│   ├── Utils/                 # Utilities (DebugLogger, OVRUtils)
│   ├── AnchorManager.cs       # Wheelchair pose computation from Touch controller anchors
│   ├── CameraGazeCursor.cs    # Head-gaze raycasting against FLOOR_EffectMesh
│   ├── CursorController.cs    # Spatial stability filtering for the floor cursor
│   ├── HandedInputSelector.cs # Binds OVRInputModule.rayTransform to CenterEyeAnchor
│   ├── HitpointIndicator.cs   # Progressive scale dwell indicator for floor selection
│   ├── MovementSequenceController.cs # Bridge between states, AnchorManager, TrajectoryLine, and RestThruAPI
│   ├── RestThruAPI.cs         # Operation scheduler and GotoPoint2D / AlignHeading2D closed-loop controllers
│   └── RestThruServer.cs      # UnityWebRequest HTTP client for the RestThru server
└── PassthroughHandsFreeController.unity # Main application scene
```

---

## 9. Requirements & Setup

### Requirements
- **OS**: Windows 10/11 (running Unity Editor and Meta Quest Link).
- **Unity Editor**: `6000.6.7f1` (with *Android Build Support* module).
- **Key Packages**:
  - `com.meta.xr.sdk.all`: `207.0.1`
  - `com.unity.xr.meta-openxr`: `2.6.1`
  - `com.unity.render-pipelines.universal`: `17.6.0`
- **Hardware**: Meta Quest headset (Quest 2, Quest Pro, Quest 3, or Quest 3S) with room *Space Setup / Scene Mesh* configured, and a robotic wheelchair running a RestThru HTTP server on the same network.

### Running the Project
1. Open the project in **Unity `6000.6.7f1`** on Windows and load `Assets/Scenes/PassthroughHandsFreeController/PassthroughHandsFreeController.unity`.
2. Configure the target RestThru server IP and port in [`RestThruServer.cs`](Assets/Scenes/PassthroughHandsFreeController/Scripts/RestThruServer.cs) (`Velocity2Endpoint` and `StopEndpoint`).
3. Secure the left and right Touch controllers to the wheelchair's armrest mounts and run the application via **Meta Quest Link** (Play Mode) or build and deploy the Android APK to the headset.
