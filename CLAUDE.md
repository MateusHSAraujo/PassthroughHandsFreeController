# CLAUDE.md

Guidance for Claude Code in this repository. Longer architecture write-ups live in `README.md` (thesis-oriented) and `GEMINI.md` (agent memory); read them only when a task needs that depth.

## Project

**PassthroughHandsFreeController**: a Unity 6 Mixed Reality (Passthrough) app for **Meta Quest** that drives a **robotic wheelchair hands-free**, using head gaze + dwell time. It sends velocity/stop commands over HTTP to any wheelchair running a **RestThru** server. Undergraduate thesis project (AdRoLab, FEEC/UNICAMP).

- Unity `6000.6.7f1` on Windows, URP `17.6.0`, `com.meta.xr.sdk.all` `207.0.1`, `com.unity.xr.meta-openxr` `2.6.1`.
- Main scene: `Assets/Scenes/PassthroughHandsFreeController/PassthroughHandsFreeController.unity`
- All scripts: `Assets/Scenes/PassthroughHandsFreeController/Scripts/` (note the folder is spelled `AplicationController`; keep that spelling).
- Current branch context: `upgrade-unity` (Unity 6 + URP + redesigned Horizon-style UI). `main` is the PR target.

## Architecture (one-paragraph map)

1. **State machine** (`AplicationController/`): `ApplicationController` + `IApplicationState`/`ApplicationState`, with scene references shared through `StateContext`. States: `MainMenu -> SelectingMovementTarget -> Moving -> ConfirmingRotation -> Rotating`, plus `MainMenu -> Settings -> ServerConfig -> Settings`. The main menu's Move/Rotate actions ask the active `IControlMode` (`Control/`) for their entry state instead of hardcoding it.
   - **Settings** (`Settings/`): `AppSettings` (data + ranges + presets) persisted by the static `SettingsStore` as one JSON PlayerPrefs key (`PTHFC_AppSettings`; it migrates the legacy `RestThru_ServerIP/Port` keys). Always change settings through `SettingsStore.Update(...)`, never PlayerPrefs directly. `SettingsStore.Changed` notifies listeners (e.g. `MeshVisibilityController`).
   - **Control modes** (`Control/`): `IControlMode` + `ControlModeRegistry`. Implemented: `GazeDwellControlMode` (target selection, closed-loop `GotoPoint2D`/`AlignHeading2D`) and `ControllerJoystickControlMode` (left stick Y drives, right stick X rotates, left Menu button exits; one `JoystickTeleopState` serves both Move and Rotate, so `HasDistinctRotateAction=false` greys out Rotate). `FloatingJoystickControlMode` (hands-free gaze pad, see below) is the third; `UnavailableControlMode` remains for future placeholders. Every mode must run its command loop through `RestThruAPI.ScheduleOperation` so the X+Y emergency stop covers it; the e-stop only acts while the API is BUSY. Continuous modes implement `ITeleopInput` (`Control/Teleop.cs`: normalized `TeleopCommand` linear/angular in [-1,1], angular +1 = CCW) and start through `MovementSequenceController.StartTeleop` -> `RestThruAPI.ScheduleTeleop` -> `StreamTeleopLoop`. The loop never blocks on the network: a zero command aborts any in-flight `vel2` and sends `/motion/stop` at once (re-sent every `StopRetryInterval` until the server acknowledges it), otherwise `vel2` is streamed every `dt`. Requests use a generous timeout (`RequestTimeoutSeconds`, 5 s: WiFi + small on-board computer). A failed or timed-out request never leaves the mode: it latches a link fault (`ITeleopLinkListener`), blocks motion until the stop is acknowledged and the input is released, and keeps trying to stop the robot. An exit request (Menu/Minimize) ends the loop after one stop attempt finishes (acknowledged or failed) or `ExitStopGraceSeconds` (2 s) elapses, so an unreachable robot never traps the user in the app. Only cancellation (X+Y, safety stops) ends it with `false`. An `ITeleopCommandFilter` hook (default pass-through) is where a future acceleration ramp goes.
   - **Floating Joystick** (`FloatingJoystickState`, `UI/Canvas/FloatingJoystickUI.cs`, `ArmrestButtonUI.cs`, `UI/Assets/ChairAnchoredPositioner.cs`, `Control/GazePadZoneTracker.cs`): a 4-wedge gaze pad (Forward/Back/Turn left/Turn right) anchored to the wheelchair frame, not the head. Zones come from the head ray vs. the pad plane; a wedge arms after 0.25 s, only after the gaze has visited the centre, and the chair stops the instant the gaze leaves. Lost HMD/controller tracking gives a zero command. Left armrest dwell button = Menu, right = Minimize / Open pad (one state with Driving/Docked sub-modes; every exit goes through the loop's stop path). The buttons are icon-only circles; sprites come from the shared `ArmrestIconSet` asset (to add an icon: append an `ArmrestIcon` value and add one entry to the asset). The UI never waits for the robot: Minimize/Menu react at once and the loop stops the chair in the background. Offsets/tilt are Inspector values on `ChairAnchoredPositioner`, to be tuned on the headset. `ControlModeId` is persisted as an int: append new values, never reorder.
2. **Gaze UI** (`UI/`, `CameraGazeCursor.cs`, `CursorController.cs`, `HitpointIndicator.cs`): `HandedInputSelector` points the OVR UI ray at `centerEyeAnchor`. `ToggleGazeInteractor` provides procedural dwell feedback with no AnimatorControllers. Canvas hierarchies live in prefabs placed in the scene (project convention); scripts reference children through serialized fields and only bind events at runtime, instead of `transform.Find` string paths. `GazeUIFactory` + `GazeStepper` / `GazeOptionSelector` are editor-time builders: `SettingsUI` has a "Rebuild Layout" component context menu that regenerates its prefab hierarchy (it overwrites manual tweaks, so prefer editing the prefab directly; after rebuilding, check that the scene instance has no stale RectTransform overrides and still references `SceneUIHelpers`). Rounded corners (the project's visual standard) come from the Meta Interaction SDK `RoundedBoxUI` material + `RoundedBoxUIProperties` component; pass a `GazeUITheme` with that material so `GazeUIFactory.ApplyRounded` matches the existing canvases. Canvases derive from `CameraGazeControlledCanvas` and are placed by `LineOfSightPositioner`. Floor targets come from a raycast against `FLOOR_EffectMesh` (MRUK).
3. **Wheelchair pose** (`AnchorManager.cs`): the two Touch controllers, mounted on the armrests, give the 6-DoF pose in `LateUpdate`. `ApplicationController.VibrateRoutine` pulses `RTouch` (`0.001f`, every 2 s) so Horizon OS does not sleep the controllers.
4. **Control + HTTP** (`MovementSequenceController.cs`, `RestThruAPI.cs`, `RestThruServer.cs`): async closed-loop `GotoPoint2D` (PI lateral distance + P heading) and `AlignHeading2D` (bang-bang above 30 degrees, PID below). Endpoints: `PUT /motion/vel2`, `PUT /motion/stop`, `GET /motion/limits` (reachability check only; its body is deliberately ignored). Server URL is `{http|https}://{host}:{port}` from `SettingsStore`. Nominal speeds come from settings: `Vlin = LinearSpeed`, initial-turn `Vrot = AngularSpeed`, and AlignHeading `maxVrot` is scaled by `AngularSpeed / AngularSpeedDefault` but never exceeds the Inspector value. Speed ranges, defaults, steps and Slow/Fast presets live only in the `SpeedLimits` ScriptableObject (`Assets/Scenes/PassthroughHandsFreeController/Resources/SpeedLimits.asset`, Inspector-editable, loaded via `Resources`); `AppSettings.Sanitize` clamps to it and `SettingsUI` applies it to the steppers at runtime. By the user's decision (2026-11-07) nothing is read from the server: the RestThru server does not clamp `vel2`, so these app limits are the only speed limits in the chain. `X + Y` triggers emergency stop. `RestThruAPI` also cancels the running operation (which stops the robot) on `OnApplicationPause`, focus loss, headset unmount and input-focus loss (device builds only).

## Conventions (must follow)

- **English only** for code, comments, commit messages and docs.
- **Logging**: use `DebugLogger.Log/LogWarning/LogError` (`Utils/DebugLogger.cs`). Never use `Debug.Log` directly.
- **Control telemetry**: `RestThruAPI` emits `<control_info> { ... } </control_info>` JSON blocks via `DebugLogger.LogWarning`, which `Logs/logs_analyzer.ipynb` parses. **Do not change that JSON structure** without also updating the notebook.
- **Name quirk**: the class is `SelecTargetUI` (no first "t") in `SelectTargetUI.cs`. Do not "fix" it in passing, because scene and prefab references depend on it.
- **Wheelchair safety**: this code moves a real, occupied wheelchair. Do not loosen tolerances, timeouts, speed limits or the emergency-stop path without being explicitly asked, and call out any change that affects motion behavior.
- `RestThruServer` sends `"{}"` when `payload == null` (avoids Unity's empty `UploadHandlerRaw` exception). Preserve this.
- Server scheme/host/port persist via `SettingsStore`, defaulting to `http://192.168.0.2:8080`. HTTPS accepts any server certificate (self-signed, untrusted or name-mismatched) through `RestThruServer.AcceptAnyCertificateHandler`, by the user's explicit choice (2026-11-07): traffic is encrypted but the server identity is not verified.

## Working with Unity safely

- **Prefer the Unity MCP** (`mcp__unity-mcp__Unity_*`) for scene, GameObject, console and asset operations, since it goes through the live Editor. Useful tools: `Unity_ManageScene`, `Unity_ManageGameObject`, `Unity_ReadConsole`/`Unity_GetConsoleLogs`, `Unity_RunCommand`, `Unity_FindProjectAssets`, `Unity_Camera_Capture`, `Unity_SceneView_Capture*`.
- Use `Unity_ManageScript`/`Unity_ScriptApplyEdits` (or normal file edits followed by a console check) for C#. After edits, check the console for compile errors before reporting success.
- Do **not** hand-edit `.unity`, `.prefab`, `.meta` or other YAML assets as text. Change them through the MCP so GUIDs and references stay intact.
- Never touch generated/ignored folders: `Library/`, `Temp/`, `Logs/`, `Build/`, `.utmp/`, `*.csproj`, `*.sln*`.
- Scene hierarchy and project-data calls can return very large output. Ask for a limited `Depth` or a specific object.
- `metavr` MCP (`mcp__metavr__*`) covers Quest device control, OpenXR input simulation, logcat, perf traces and Meta docs. Use it only for runtime testing or Meta API questions.
- The Meta XR Operator skills live in `.agents/skills/` (and `.gemini/skills/`), not `.claude/skills/`. They are available as reference material but are not auto-loaded as Claude skills.
- Runtime behavior (gaze, dwell, passthrough, controller tracking) cannot be fully verified in the Editor. Say so when a change is only compile-checked.

## Token-efficiency habits

- Name the exact file or object in requests; avoid broad exploration.
- Read README/GEMINI sections on demand instead of whole files.
- Use `/clear` between unrelated tasks and `/compact` in long sessions.

## Planned modifications

<!-- Add the changes you want to make here so each session starts with the goal. Suggested format:
- [ ] Short title: what, why, files likely involved, acceptance criteria
-->

- [x] Settings hub: HTTP/HTTPS, linear/rotation speed (presets + -/+), MRUK mesh visibility, control-mode selector (2026-11-03).
- [x] Controller-joystick teleoperation mode (2026-11-03).
- [x] Floating-panel (gaze pad) joystick teleoperation mode (needs headset tuning of pad offsets).
- [ ] Optional acceleration ramp for streaming modes via `ITeleopCommandFilter`.
- [x] ~~Parse `/motion/limits` for a max wheel speed~~: removed 2026-11-07 (it misread `maxrotvel` [deg/s] as a wheel cap). Speed limits come only from the `SpeedLimits` asset.
