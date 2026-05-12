# tft.vr.movement

本函式庫自 **SBox-VR-Controller** 遷移：**XMovement** 玩家位移範例控制器，以及 **介面式 DI** 之抽象與核心服務（**非** `Microsoft.Extensions.DependencyInjection` 容器）。

## 內容

| 路徑 | 說明 |
|------|------|
| `Code/XMovement/` | `PlayerMovement` 與 `PlayerWalkControllerSimple`／`Complex` 等。 |
| `Code/Player/Abstractions/` | `IVRInputProvider`、`IControllerInput`、`IMovementInputSource`、`IHandTracker` 等。 |
| `Code/Player/Services/` | `SandboxVRInputProvider`、`VRControllerAdapter`、`NullController`、`VRMovementInputSource`、`KeyboardMovementInputSource`、`CompositeMovementInputSource`。 |

## 參考文件

- 本倉庫：`docs/references/sbox-vr-controller/VR_DI_ARCHITECTURE.md`
- 本倉庫範圍總覽：`docs/specs/vr-input-di-port.md`
- 位移與 `VRPlayerController` 互斥：`docs/specs/vr-locomotion-xmovement.md`

## 消費方式

主專案 `Code/testbed.csproj` 已 `ProjectReference` 本 `.csproj`。在玩家 prefab 上**擇一**使用 `XMovement` 之 `PlayerWalkController*` 或既有 `VRPlayerController`，避免雙重位移。
