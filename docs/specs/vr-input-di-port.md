# Spec：VR 輸入與介面式 DI（SBox-VR-Controller 移植範圍）

## 目的

收斂「**非** Microsoft DI 容器、而以 `Components.Get<T>(FindMode.EverythingInSelfAndAncestors)` 解析」之輸入／位移抽象，並標示本倉庫**已併入**與**未併入**邊界，避免 SDD 與實作漂移。

## 權威文件

| 主題 | 路徑 |
|------|------|
| 概念與解析慣例 | [`docs/references/sbox-vr-controller/VR_DI_ARCHITECTURE.md`](../references/sbox-vr-controller/VR_DI_ARCHITECTURE.md) |
| 函式庫 README | [`Libraries/tft.vr.movement/README.md`](../../Libraries/tft.vr.movement/README.md) |
| 本體位移與 `VRPlayerController` 互斥 | [`vr-locomotion-xmovement.md`](./vr-locomotion-xmovement.md) |
| 互動堆疊（`VRGrabber`、務實 DI） | [`2026-05-05-vr-interaction-stack.md`](./2026-05-05-vr-interaction-stack.md) |

## 本倉庫已併入（`Libraries/tft.vr.movement`）

- **Abstractions**：`IVRInputProvider`、`IControllerInput`、`IMovementInputSource`、`IHandTracker`、`IHandSkeletonProvider`、`HandSide`、`IRigRebinder` 等。
- **Services**：`SandboxVRInputProvider`、`VRControllerAdapter`、`NullController`、`VRMovementInputSource`、`KeyboardMovementInputSource`、`CompositeMovementInputSource`。
- **XMovement**：`PlayerMovement`、`PlayerWalkControllerSimple`／`Complex`（見該庫 `Code/XMovement/`）。

## 刻意未併入（仍屬上游或第二階段）

- `SandboxVRHandTracker`、`SandboxVRHandSkeletonProvider`、`OfficialHandToggle`、`VRHolsterSlot` 與 `VrhandInteraction` 武器鏈。
- 本倉庫互動主軸維持 `VRGrabber`／`VRSocket`；若未來武器要讀 `IControllerInput`，需另開適配 spec（見 `vr-item-weapon-production-workflow.md`）。

## 驗證

- `dotnet build Libraries/tft.vr.movement/Code/tft.vr.movement.csproj`
- 更廣：`docs/commands/dotnet-build-test.md`
