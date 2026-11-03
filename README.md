# Passthrough Hands-Free Controller

A Mixed Reality (Passthrough) interface for robotic wheelchair navigation control, developed as an undergraduate thesis project for the Advanced Robotics Laboratory (AdRoLab) at the School of Electrical and Computer Engineering (FEEC), University of Campinas (UNICAMP).

- **Full Thesis Document:** [Interface de Realidade Mista para Controle de Navegação de Cadeira de Rodas Robótica](https://github.com/MateusHSAraujo/PassthroughHandsFreeController/blob/main/Assets/Scenes/PassthroughHandsFreeController/TCC%20-%20Interface%20de%20Realidade%20Mista%20para%20Controle%20de%20Navega%C3%A7%C3%A3o%20de%20Cadeira%20de%20Rodas%20Rob%C3%B3tica.pdf)
- **Video Demonstration:** [Trabalho de Conclusão de Curso - Interface de Realidade Mista para Controle de Navegação de Cadeira de Rodas Robótica](https://eduplay.rnp.br/app/video/184940-tcc-passthroughhandsfreecontroller)

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
  - Implements procedural dwell progress interpolation, scale animation, and spatial audio feedback on `Toggle` and `Button` components without requiring legacy `AnimatorController` assets.
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
  - Because the controllers remain stationary on the armrests, `ApplicationController` sends a subtle `0.001f` vibration pulse to `RTouch` every 2 seconds, preventing Horizon OS from placing the controllers into sleep mode.

---

## 6. Closed-Loop Motion Control & RestThru Client

Navigation commands are calculated on the headset and issued asynchronously to the wheelchair's REST API (`RestThruServer.cs`):

### Point-to-Point Navigation (`RestThruAPI.GotoPoint2D`)
1. **Initial Alignment**: Rotates in place at `±Vrot` until the heading error is within `HeadingTolerance` (`5°`).
2. **Path Tracking (PI Lateral Distance + P Heading)**:
   $$\Delta = K_{dist} \cdot e_{dist} + K_{int} \cdot \int e_{dist} \, dt + K_{head} \cdot e_{head}$$
   $$V_r = V_{lin} - \Delta, \quad V_l = V_{lin} + \Delta$$
   - Near the destination ($\le 5 \times \text{TravelTolerance}$), linear speed and gains are halved and the control loop frequency is quadrupled ($0.5\text{s} \to 0.125\text{s}$).
3. **Arrival**: Sends `PUT /motion/stop` when the remaining distance is $\le \text{TravelTolerance}$ (`0.1m`).

### Gaze Heading Alignment (`RestThruAPI.AlignHeading2D`)
- **Coarse Rotation**: Uses Bang-Bang control ($\pm \text{maxVrot}$) for heading errors $> 30^\circ$.
- **Fine Adjustment**: Uses a PID controller ($K_p=0.4, K_i=0.001, K_d=0.04$) when error $\le 30^\circ$.
- **Completion Guard**: Confirms alignment once the error stays within $\pm 10^\circ$ for `TimeoutToCompletion` (`3.0s`).

---

## 7. Development History

1. **Computer Vision & QR Tracking Experiments** (`0b3c434` / `fc726a8`):
   - Initially tested wheelchair localization via Passthrough camera video frames (ZXing OpenCV decoder) and MRUK QR markers.
   - Replaced due to Link/Passthrough limitations and tracking stability in dynamic environments.
2. **Controller-Anchor Pose Tracking** (`ab838e0`):
   - Adopted mounted Touch controllers as robust 6-DoF spatial anchors (`AnchorManager.cs`).
3. **Rotation Interaction & Telemetry** (`7955294` / `a798d3a`):
   - Added `RotatingState`, heading-alignment PID laws, and JSON telemetry logging for post-run analysis (`logs_analyzer.ipynb`).
4. **Unity 6 Engine & Modern Horizon UI Upgrade** (current):
   - Upgraded to Unity `6000.6.7f1`, Universal Render Pipeline (URP), procedural dwell interactions, server reachability status checks (`GET /motion/limits`), and in-headset server configuration.

---

## 8. Project Structure

```text
Assets/Scenes/PassthroughHandsFreeController/
├── Materials/               # Scene materials (EffectMesh, floor indicator, buttons)
├── Prefabs/
│   ├── Canvas/              # Spatial UI prefabs (MainMenu, ServerConfigUI, SelectTargetUI, RotateConfirmationUI)
│   ├── EffectMesh/          # MRUK semantic floor reconstruction prefabs
│   └── WheelchairManager.prefab # Wheelchair anchor & control orchestrator
├── Scripts/
│   ├── AplicationController/ # State machine (ApplicationController, StateContext, states)
│   ├── UI/                  # Gaze-dwell interactors, canvas controllers, line-of-sight positioning
│   ├── AnchorManager.cs     # 6-DoF wheelchair tracking from Touch controllers
│   ├── CameraGazeCursor.cs  # Floor raycasting & dwell target selection
│   ├── MovementSequenceController.cs # High-level navigation sequence dispatcher
│   ├── RestThruAPI.cs       # Closed-loop PI/PID control routines (GotoPoint2D, AlignHeading2D)
│   └── RestThruServer.cs    # UnityWebRequest HTTP client (vel2, stop, limits)
└── PassthroughHandsFreeController.unity # Main application scene
```

---

## 9. Requirements & Setup

### Environment
- **Unity**: `6000.6.7f1` (64-bit) with Android Build Support (OpenXR).
- **Packages**:
  - `com.meta.xr.sdk.all`: `207.0.1`
  - `com.unity.xr.meta-openxr`: `2.6.1`
  - `com.unity.render-pipelines.universal`: `17.6.0`
- **Hardware**: Meta Quest headset (Quest 2, Quest Pro, Quest 3, or Quest 3S) with room *Space Setup / Scene Mesh* configured, and a robotic wheelchair running a RestThru HTTP server on the same network.

### Running the Project
1. Open the project in **Unity `6000.6.7f1`** on Windows and load `Assets/Scenes/PassthroughHandsFreeController/PassthroughHandsFreeController.unity`.
2. Configure the target RestThru server IP and port via the in-headset **RestThru Server** menu or in [`RestThruServer.cs`](Assets/Scenes/PassthroughHandsFreeController/Scripts/RestThruServer.cs).
3. Secure the left and right Touch controllers to the wheelchair's armrest mounts and run the application via **Meta Quest Link** (Play Mode) or build and deploy the Android APK to the headset.
