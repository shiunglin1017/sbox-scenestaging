# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Library `Libraries/tft.vr.movement`：XMovement 與介面式 DI 核心（`SandboxVRInputProvider`、`VRMovementInputSource`、`CompositeMovementInputSource` 等）；主專案 `testbed.csproj` 已引用。
- 文件複本 `docs/references/sbox-vr-controller/`（含索引 `README.md`）。
- Spec：`docs/specs/vr-item-weapon-production-workflow.md`、`vr-unit-test-plan.md`、`vr-animgraph-contract.md`、`vr-locomotion-xmovement.md`、`vr-weapon-taxonomy.md`。
- 指令說明 `docs/commands/dotnet-build-test.md`。
- `VRLogic.VRItemInteractionProfileRules` 與 `VRItemInteractionProfile`（Inspector 集中可抓物設定）；`VRItemInteractionProfileRulesTests`。
- `SvgTests.razor`：`@inherits Panel` 以修復 Razor 產生碼無法覆寫 `BuildRenderTree` 之建置錯誤。
- Spec `docs/specs/2026-05-05-vr-interaction-stack.md` (official-alignment contracts, SDD/TDD/CI/DI, Alyx tuning notes).
- `VRLogic`: `GrabInteractionRules`, `LocomotionWishRules`, `VrInteractionConstants`, `AlyxFeelTuningDefaults` for testable grab/locomotion rules and tuning defaults.
- `GripReleaseNotification` and `GrabNetworkContracts` for swappable release broadcast and multiplayer ownership notes.
- `.gitlab-ci.yml` job `unit_tests` running `dotnet test` on `UnitTests/testbed.unittest.csproj`.
- Unit tests `GrabInteractionRulesTests`, `LocomotionWishRulesTests`.
- Docs `docs/CI_UNIT_TEST.md` with the local/CI unit test command.
- `VRGhostHandTarget` component: non-physics ghost hand / joint target aligned to VR grip or optional `weapon_hold` attachment; `test.vr.scene` includes `GhostTarget_Left` and `GhostTarget_Right`.
- `VRPlayerRig.EnableGhostTargets` toggles all `VRGhostHandTarget` under the player hierarchy.
- Establish engineering workflow documents for SDD, TDD, CI/CD, DI, and command standards.
- `VRPlayerRig` root component to toggle locomotion, desktop hand fallback, and per-hand `VRGrabber` from the player root.
- `VRLogic` class library with `VRInteractionRules` (socket id and distance checks) and `VRLogic.UnitTests` MSTest project.
- Specification `docs/specs/2026-05-04-vr-player-rig.md`.
- `weaponlab` projectile path: `WeaponProjectile` component and `TestWeapon` fire mode switch (`Trace` / `Projectile`) with shared impact resolver.
- `AutoPropAdapterSystem` for cloud prop minimum grab compatibility (`Rigidbody` + `Collider` + `Grabbable`) with incremental scan budget.
- `VRTurnAndTeleportSystem` + `VRLogic.TeleportArcRules`: VR Snap/Smooth turn、Arc Teleport 與舒適化強度輸出。
- `VRDistanceGrabber` + `VRLogic.DistanceGrabRules`: 遠距離選取、吸附拉手、接軌 `VRGrabber.TryQueueExternalGrab`。
- `VR UI` 交互元件：`VRUIInteractable`、`VRUIPointerRay`、`VRUIPokeInteractor`（遠距雷射與近距戳擊）。
- 機關元件：`VRLinearDriveInteractable`、`VRRotaryDriveInteractable`、`VRPhysicalButton`。
- `VRTwoHandGripStabilizer`：雙手同持時以後手為 pivot、前手控制朝向。
- Unit tests：`TeleportArcRulesTests`、`DistanceGrabRulesTests`。

### Changed
- VR 抓取與物理權威策略：`ComputeGrabPose` 姿勢優先序改為 Attachment > GrabPivot > fallback；`VRItemInteractionProfile` 改為預設保留 ModelDoc/Prefab 物理值，僅在 `OverrideMass` / `OverrideDamping` / `OverrideSurface` 啟用時覆寫。
- `VRGrabber` geometry hover preview：新增中心射線距離映射 curl（視覺-only），並可透過 `HandRenderer` 參數輸出。
- `VRGhostHandTarget`：可選讀取 `VRGrabber` hover preview 姿勢作預抓取旋轉（僅視覺）。
- `VRItemInteractionProfile`：可選 **`SurfaceResourceName`**，於 `OnAwake` 以 `Surface.FindByName` 套用到本體與子階層啟用之 `Collider`（補齊計畫 Facade 之 surface 預設）。
- `VRGrabber`: grab/release runs in `OnFixedUpdate` (physics-step aligned); exposes `GrabInteractorState` and configurable grip thresholds; default attachment name uses `VrInteractionConstants`; release events go through `GripReleaseNotification`.
- `VRPlayerController`: movement, jump, and optional crouch run in `OnFixedUpdate` using `CharacterController` friction/acceleration (`ApplyFriction`/`Accelerate`/`Punch`); planar wish uses `LocomotionWishRules`; right-stick / snap turn remains on `OnUpdate` via `EnableRightStickTurn`.
- `VRGhostHandTarget` / `VRSocket` class docs clarify presentation vs interactor vs socket roles.
- `docs/specs/2026-05-04-vr-player-rig.md` documents `VRGhostHandTarget` and `VRController.Transform` / `AimTransform` for pose sourcing.
- `VRSocket` delegates id/radius checks to `VRInteractionRules`.
- `Assets/Scenes/Tests/test.vr.scene` wires `VRPlayerRig`, both-hand `VRGrabber`, and a `Grabbable` + `Rigidbody` on the test cube.
- `.sbproj` `ControlModes` now enables `Keyboard` and `Gamepad` alongside `VR` so desktop and VR can run from the same project configuration.
- `VRPlayerRig` 新增 `MovementAuthority`（預設 `XMovement`）與 XMovement/Legacy 互斥切換、自動串接 `CompositeMovementInputSource`（VR + 非VR）。
- `VRSocket` 新增 `BlockSnapWhileTwoHanded`，雙手持握中可避免插槽吸附競態。
- `VRGrabber` 新增外部抓取入口 `TryQueueExternalGrab` 與持握狀態對外存取，供 Distance Grab/雙手系統整合。

### Fixed
- `VRGrabber`: desktop (`Game.IsRunningInVR == false`) no longer reads `Input.VR` hand grips (which caused `NullReferenceException`); uses configurable mouse actions (`attack1` / `attack2` by default) per hand for grip analog.

### Removed
- N/A

## Changelog Policy

- Every code change must include an update in `CHANGELOG.md`.
- Add entries under `## [Unreleased]` using one of: `Added`, `Changed`, `Fixed`, `Removed`.
- Keep each bullet focused on impact and behavior, not implementation detail.
- At release time, move `Unreleased` items to a versioned section, for example `## [0.2.0] - 2026-04-30`.
