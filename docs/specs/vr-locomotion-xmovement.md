# Spec：VR 本體位移（XMovement 與 VRPlayerController 互斥）

## 問題

同時啟用 **兩套** 玩家根位移（例如 `PlayerWalkController*` 與 `VRPlayerController` 皆在 FixedUpdate 推進 `CharacterController`）會造成雙重移動、與 VR Anchor／追蹤階層不一致。

## 規範

- **擇一**：玩家根上僅啟用下列之一作為**主要本體位移**來源：  
  - **A.** `Libraries/tft.vr.movement` 內之 `PlayerWalkControllerSimple`／`PlayerWalkControllerComplex`（搭配 `IMovementInputSource`，例如 `VRMovementInputSource` + `SandboxVRInputProvider`），或  
  - **B.** 既有 `Code/test/VR/VRPlayerController.cs`（見 [2026-05-05-vr-interaction-stack.md](./2026-05-05-vr-interaction-stack.md)）。  
- **未選擇**的那一組應在 prefab 或場景中**停用元件**（`Active = false`）或移除，而非僅關閉部分欄位。

## 與 DI 的關係

介面式 DI 說明見 `docs/references/sbox-vr-controller/VR_DI_ARCHITECTURE.md` 與 `Libraries/tft.vr.movement/README.md`；本倉庫移植範圍總覽見 [`docs/specs/vr-input-di-port.md`](../specs/vr-input-di-port.md)。`SandboxVRInputProvider` 與 Anchor／Tracked 的擁有權須與場景 prefab 對照，避免重複停用邏輯。

## 與 IK 的關係

`VRThreePointTracker` 處理 Avatar 與頭／手 IK 目標，**不**取代本 spec 之本體位移來源；但 `EnableAvatarRootControl` 等選項會動到 Avatar 根局部姿態，與「誰在推玩家根世界位移」應一併在場景驗收。

## 驗收（手動）

1. VR 與桌面各跑一輪：單一位移來源、無「滑步加倍」。  
2. 確認 **VR Anchor** 隨玩家根一致（官方 VR 文件要求）。  
3. 記錄於 MR：本次場景／prefab 選用 A 或 B。

## 現況預設

目前 `VRPlayerRig` 已提供 `MovementAuthority`：

- `XMovement`（預設）：啟用 `PlayerWalkControllerSimple/Complex`，停用 `VRPlayerController`。
- `LegacyVRPlayerController`：保留舊路線，供回滾或比對。

若走 `XMovement` 路線，建議同時掛載：

- `SandboxVRInputProvider`
- `VRMovementInputSource`
- `KeyboardMovementInputSource`
- `CompositeMovementInputSource`

以確保 VR 與非 VR 桌面模式都能使用同一套玩家根位移流程。
