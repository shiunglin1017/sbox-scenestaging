# VR Code SOLID + DI 重構計畫
> 版本：v1.0 | 日期：2026-05-14 | 狀態：待審閱

---

## 一、審查結論摘要

| 層次 | 可測性（平均） | 最大痛點 |
|------|--------------|---------|
| `Code/VRLogic/` (純規則) | 4.9 / 5 | 部分仍引用 `Sandbox` 型別 |
| `Code/VR/` Manipulators | 3.8 / 5 | 邊界極薄，可接受 |
| `Code/VR/` 互動核心 | 2.3 / 5 | 輸入、物理、場景 API 混入邏輯 |
| `Code/VR/` 系統整合 | 1.8 / 5 | `VRPlayerRig` 反射、VRGrabber SRP |

---

## 二、橫向共用問題（優先修）

### P0 — VR/桌面輸入分支到處重複

**現狀：** `Game.IsRunningInVR` + `Input.VR` 的 if/else 出現在 6 個元件：
- `VRGrabber.ReadGripAndThrowVelocity`
- `VRDistanceGrabber.IsGrabPressed`
- `VRPlayerController.OnUpdate/OnFixedUpdate`
- `VRTurnAndTeleportSystem.ReadTurnAxis`
- `VRUIPointerRay.IsPressDown`
- `VRUIPokeInteractor.IsPressDown`

**解法：** 介面 `IControllerInput`（單手）與 `IHeadInput`，由兩個實作切換：

```csharp
// Code/VR/Input/IControllerInput.cs
public interface IControllerInput
{
    float Grip { get; }            // 0–1
    Vector2 Joystick { get; }
    bool PrimaryButtonDown { get; }
    bool PrimaryButtonPressed { get; }
    Vector3 WorldPosition { get; }
    Rotation WorldRotation { get; }
    Vector3 LinearVelocity { get; }
    Vector3 AngularVelocity { get; }
}

// Code/VR/Input/IHeadInput.cs
public interface IHeadInput
{
    Vector3 WorldPosition { get; }
    Rotation WorldRotation { get; }
}

// Code/VR/Input/VRControllerInput.cs  — 真實 VR，讀 Input.VR.LeftHand/RightHand
// Code/VR/Input/DesktopControllerInput.cs — 鍵盤滑鼠模擬
// Code/VR/Input/ControllerInputProvider.cs — Component，Auto-select by Game.IsRunningInVR，公開 Left/Right/Head
```

`ControllerInputProvider` 掛在 Player 上，其他元件透過 `[Property]` 引用它。

---

### P0 — `VRGrabber.TryResolveRigidbody` 靜態共享

**現狀：** `VRSocket`、`VRDistanceGrabber`、`VRTwoHandGripStabilizer` 都調靜態。

**解法：**
```csharp
// Code/VR/IGrabbableBodyResolver.cs
public interface IGrabbableBodyResolver
{
    bool TryResolve(GameObject go, out Rigidbody rb);
}
// 預設實作 GrabbableBodyResolver : Component, IGrabbableBodyResolver
// 作為 Player 上的服務元件，或 Grabbable.TryGetRigidbody() 方法
```

---

### P1 — `VRPlayerRig` 反射式 AutoWire

**現狀：** 用字串 `"PlayerWalkControllerSimple"` + Reflection 設 Property，違反 SRP/DIP/OCP。

**解法：**
```csharp
// VRPlayerRig 改成只做開關，AutoWire 改明確 [Property]：
[Property] public Component XMovementWalkController { get; set; }  // 直接拖入
[Property] public CompositeMovementInputSource InputSource { get; set; }
// 移除 FindComponentByName / GetPropertyValue / SetPropertyValue 全部反射邏輯
```

---

## 三、逐元件重構計畫

### 3.1 VRGrabber — SRP 拆分

**現狀職責（過多）：**
1. Trigger 集合管理
2. 輸入採樣（Grip axis、VR velocity）
3. 候選物評分
4. 幾何 Hover curl 預覽
5. 物理關節建立 / 銷毀
6. 投擲緩衝 + 估算

**重構方向（同一 Component，抽內部服務）：**

```
VRGrabber（Coordinator）
  ├─ IControllerInput          ← P0 共用，透過 ControllerInputProvider 注入
  ├─ IGrabCandidateEvaluator   ← 介面 + 預設 TriggerBasedCandidateEvaluator
  ├─ IGrabJointFactory         ← 介面 + 預設 FixedJointGrabFactory
  ├─ IThrowVelocityEstimator   ← 介面 + 預設 ThrowSignalEstimator（包現有靜態）
  └─ IHoverPresentation        ← 介面 + 預設 HandRendererHoverPresentation
```

每個介面允許單元測試替換。`VRGrabber` 保留生命週期呼叫（`OnUpdate`/`OnFixedUpdate`）與狀態機，邏輯細節委派服務。

**欄位改動：**
```csharp
[Property] public IControllerInput Input { get; set; }     // 注入
[Property] public bool IsLeftHand { get; set; }
// ... 其餘 tuning 屬性保留 ...
// 移除: private readonly ThrowSignalBuffer _throwSignalBuffer = new(); → 改注入
```

---

### 3.2 VRSocket — 場景掃描解耦

**現狀：** `NotifyGripReleased` 直接 `scene.GetAllComponents<VRSocket>()` 全場掃描。

**解法：** Socket 自行訂閱 `GripReleaseNotification.Publish`：

```csharp
// OnAwake
GripReleaseNotification.Subscribe(this, OnGripReleased);
// OnDestroy
GripReleaseNotification.Unsubscribe(this);
```

`GripReleaseNotification` 改為 multicast（`List<Action<...>>`）而非全場掃描。

`IsTwoHandActive` 改注入 `ITwoHandStateQuery`：
```csharp
public interface ITwoHandStateQuery
{
    bool IsObjectTwoHanded(GameObject obj);
}
```

---

### 3.3 VRPlayerController — 輸入抽象

移除內部 `Game.IsRunningInVR` 分支，改：

```csharp
[Property] public IControllerInput LeftHandInput { get; set; }
[Property] public IControllerInput RightHandInput { get; set; }
[Property] public IHeadInput HeadInput { get; set; }
```

`LocomotionWishRules` 已為純函式，保持不動。

---

### 3.4 VRDistanceGrabber — 目標選擇抽象

```csharp
public interface IDistanceGrabTargetFinder
{
    GameObject FindBest(Vector3 aimOrigin, Rotation aimRotation, float maxDist);
}
// 預設 SceneRigidbodyTargetFinder : Component, IDistanceGrabTargetFinder
[Property] public IDistanceGrabTargetFinder TargetFinder { get; set; }
```

`DistanceGrabRules` 保持純函式不動。

---

### 3.5 VRTurnAndTeleportSystem — 策略拆分

```csharp
public interface ITurnInput { float RightStickX { get; } }
public interface ITeleportInput { bool IsHeld { get; } }
// VRTurnAndTeleportSystem 改注入，UpdateTurn/UpdateTeleport/UpdateComfort 保持私有方法
```

`TeleportArcRules` 的 `SceneTraceResult` 依賴改為自訂 DTO：
```csharp
// Code/VRLogic/HitInfo.cs
public readonly struct HitInfo { public bool Hit; public Vector3 Point; public Vector3 Normal; }
```
讓 `VRLogic` 完全不依賴 Sandbox 型別。

---

### 3.6 VRUIInteractable — 事件擴充

加 `Action` 讓外部訂閱（符合 OCP，不用繼承）：

```csharp
public Action OnHoverEnter { get; set; }
public Action OnHoverExit { get; set; }
public Action OnPressed { get; set; }
public Action OnReleased { get; set; }
```

---

### 3.7 Socketable / VRSocket — 解除具體型別耦合

```csharp
// Socketable.cs
internal void NotifySocketed(ISocket socket)  // 改 interface
public interface ISocket { string SocketId { get; } bool IsOccupied { get; } }
```

---

### 3.8 VRLogic 層 — 消除 Sandbox 型別

| 檔案 | 現依賴 | 改成 |
|------|-------|------|
| `TeleportArcRules` | `SceneTraceResult` | `HitInfo` DTO |
| `LocomotionWishRules` | `Vector2/Vector3 Sandbox` | 同 `System.Numerics` 或自訂 struct（需評估影響） |

> **注意：** `Vector2/Vector3` 在 Sandbox 與 .NET 均存在且相容，若確認引用 `Sandbox.Maths` 而非 `System.Numerics` 才需更動，否則可保留。

---

## 四、不動項目（已符合 SOLID/DI）

下列已達良好設計，**本次不重構**：

- `GrabInteractionRules`、`VRInteractionRules`、`DistanceGrabRules`、`RotationClampRules` — 純靜態規則，可測性 5/5
- `ThrowSignalBuffer`、`ThrowEstimator` — 邏輯正確，僅需抽介面供注入
- `VRLinearDriveInteractable`、`VRRotaryDriveInteractable` — SRP 良好
- `GripReleaseNotification` — 可替換 Action 設計已符合 DIP
- `WristPivotOffset`、`VRRootColliderStabilizer` — 邊界小，改動 ROI 低

---

## 五、實作順序（Phase）

### Phase A — 共用介面（無破壞性，先建）
1. `IControllerInput` + `IHeadInput` 介面
2. `VRControllerInput`、`DesktopControllerInput` 實作
3. `ControllerInputProvider` Component
4. `IGrabbableBodyResolver` + `GrabbableBodyResolver`
5. `HitInfo` DTO（讓 VRLogic 去 Sandbox 依賴）

### Phase B — VRPlayerRig 去反射
6. `VRPlayerRig` 移除反射，改明確 `[Property]`
7. 場景重新連線 Inspector

### Phase C — VRGrabber 服務化
8. `IGrabJointFactory` + `FixedJointGrabFactory`
9. `IThrowVelocityEstimator` + `ThrowSignalEstimator`（包現有靜態）
10. `IHoverPresentation` + `HandRendererHoverPresentation`
11. `VRGrabber` 改接介面

### Phase D — 系統解耦
12. `GripReleaseNotification` 改 multicast 訂閱
13. `VRSocket.IsTwoHandActive` 改 `ITwoHandStateQuery`
14. `VRPlayerController` 注入 `IControllerInput`
15. `VRDistanceGrabber.IDistanceGrabTargetFinder`
16. `VRTurnAndTeleportSystem` 注入輸入介面

### Phase E — 事件與 VRLogic 純化
17. `VRUIInteractable` 加 Action 事件
18. `Socketable` 改 `ISocket`
19. `TeleportArcRules` 換 `HitInfo` DTO（若決定做）

---

## 六、單元測試涵蓋目標

| 目標 | 測試項目 |
|------|---------|
| `DesktopControllerInput` | Grip、Joystick、PrimaryButton mapping |
| `GrabbableBodyResolver` | 有／無 Rigidbody 物件回傳正確值 |
| `VRGrabber` grab/release 狀態機 | 用 mock `IControllerInput` 驅動 |
| `VRSocket` snap 條件 | 用 mock `ITwoHandStateQuery` 阻斷雙手握持 |
| `TeleportArcRules` | 委派 trace 回傳假 HitInfo |

---

## 七、不在本計畫範圍

- 網路 Authority（`GrabNetworkContracts` 仍空殼，另立計畫）
- IK / Avatar 骨架（`CitizenAnimationHelper` 非 VR 系統）
- XMovement 內部重構（第三方 library）
- VR 硬體 SDK 替換（Oculus / SteamVR 等）
