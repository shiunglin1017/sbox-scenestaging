# Spec: VR 幽靈手、抓取、Socket 與本體移動（官方對齊／業界分層／CI／DI）

## Context and problem statement

本專案已有 `VRGhostHandTarget`、`VRGrabber`、`VRSocket`、`VRPlayerRig` 與 `VRLogic.VRInteractionRules`，但缺少一份**與官方文件一致**的契約說明、與 [ENGINEERING_WORKFLOW.md](../ENGINEERING_WORKFLOW.md) 對齊的 SDD／TDD／CI／DI 條目，以及本體移動與物理步長一致化。本 spec 銜接 [2026-05-04-vr-player-rig.md](./2026-05-04-vr-player-rig.md) 並擴充至 Alyx 向手感、Walker 風格 `CharacterController` 移動與合併門檻。

## Goals

- 固定**追蹤鏈**、**Trigger 偵測**、**ModelDoc attachment 命名**與官方 s&box 文件一致之開發契約。
- 將 **Interactor／Interactable／Socket** 職責寫入規格：`VRGhostHandTarget`（呈現目標）、`VRGrabber`（選取＋關節）、`VRSocket`（槽位狀態）。
- **抓取**：關節建立／釋放與 **FixedUpdate** 對齊；Grip 閾值與 wish 方向之純邏輯收斂至 `VRLogic` 並附單元測試。
- **本體移動**：預設敘述為 `VRPlayerController` 以 **OnFixedUpdate** 驅動 `CharacterController`（`Accelerate`／`ApplyFriction`／`Punch` 跳躍、可選蹲伏），與雙手物理步長一致。若改採 **`Libraries/tft.vr.movement`** 之 `PlayerWalkController*`，則須與 `VRPlayerController` **擇一啟用**（互斥與驗收見 [vr-locomotion-xmovement.md](./vr-locomotion-xmovement.md)）。
- **CI**：管線現階段僅 `dotnet test`（[UnitTests/testbed.unittest.csproj](../../UnitTests/testbed.unittest.csproj)）；**每完成計畫 todo 須本地／MR 通過同一測試命令**。
- **DI（務實）**：純邏輯在 `VRLogic`；釋放廣播可替換為 `GripReleaseNotification.Publish`（預設轉發 `VRSocket.NotifyGripReleased`），利於日後多人或事件匯流排。

## Non-Goals

- 完整 IoC 容器、自動化 VR 頭戴 E2E、GitLab Runner 內建 s&box 編輯器編譯（CI 僅 dotnet test，依賴本機／代理已配置的 `ProjectReference` 路徑）。
- 可配置關節（非 FixedJoint）之完整實作；雙手穩定與多人權威之完整實作（僅合約與擴充點）。

## Scope

### In

- `docs/specs/2026-05-05-vr-interaction-stack.md`（本檔）。
- `Code/VRLogic/*.cs`：`GrabInteractionRules`、`LocomotionWishRules`、`VrInteractionConstants`、`AlyxFeelTuningDefaults`。
- `Code/test/VR/VRGrabber.cs`、`VRPlayerController.cs`、`VRItemInteractionProfile.cs`、`GripReleaseNotification.cs`。
- `UnitTests/*Tests.cs` 擴充。
- `.gitlab-ci.yml`（單一 test job）。

### Out

- 修改 `test.vr.scene` 二進位資產（非必要不碰）；完整 `PlayerController` Walker 元件與 VR 的雙掛載整合（可後續 spec）。

## 官方文件契約（追蹤／輸入／Trigger／Attachment）

| 主題 | 官方來源 | 專案契約 |
|------|----------|----------|
| VR 根 | [VR — docs](https://docs.facepunch.com/s/sbox-dev/doc/vr-GPhXAmcHLM)：`VRAnchor`、`VRTrackedObject`、`VRHand` | 玩家根與 playspace 對齊；頭／手追蹤以 `VRTrackedObject` 或 `Input.VR` 為準；`VRGhostHandTarget.TransformSource` 指向追蹤鏈節點。 |
| 輸入 | 同上；`Game.IsRunningInVR` | `VRGrabber`／位移分支依 `Game.IsRunningInVR`；桌面 fallback 使用 `Input.AnalogMove`／主攝影機（與現有 `VRPlayerController`／`VRFallbackSimulator` 一致）。 |
| Trigger | s&box 文件：Physics → Triggers（與本 repo 對照之 `sbox-docs/docs/physics/triggers.md`） | `VRGrabber`、`VRSocket` 使用 **IsTrigger** 的 Collider + `ITriggerListener`。 |
| Attachment | s&box 文件：Model Editor（DCC 優先） | `HandRenderer.GetAttachment( name )` 之 `AttachmentName` 須與 ModelDoc **大小寫一致**；預設常數見 `VrInteractionConstants.DefaultGripAttachmentName`。 |

## API / component contracts

### 呈現層：`VRGhostHandTarget`

- 僅負責**目標 Transform**（無剛體）；不建立關節、不決定 Grip 閾值。
- 與 `VRGrabber` 共用同一 `AttachmentName` 語意時，握點與幽靈目標一致。

### Interactor：`VRGrabber`

- **Hover**：Trigger 內可抓物（`TryResolveRigidbody`）。
- **Select / Attach**：Grip 超過 `GrabInteractionRules` 閾值；**關節建立於 `OnFixedUpdate`**，對齊物理步。
- **Release**：於 Fixed 步銷毀關節並寫入釋放速度；廣播透過 `GripReleaseNotification.Publish`。

### Socket：`VRSocket`

- ID／半徑仍委託 `VRInteractionRules`；不直接依賴 `VRGrabber` 內部欄位。
- 可選 `BlockSnapWhileTwoHanded`：當物件處於 `VRTwoHandGripStabilizer.IsTwoHandActive` 時暫停吸附，避免雙手持握與插槽互相拉扯。

### 本體：`VRPlayerController`

- **轉向**保留於 `OnUpdate`（與顯示幀一致）。
- **位移／跳躍／蹲伏**於 `OnFixedUpdate` 使用 `CharacterController` 之摩擦與加速 API，與 [ExampleComponents PlayerController](../../Code/ExampleComponents/PlayerController.cs)／Walker 風格一致。

### Locomotion 擴充：`VRTurnAndTeleportSystem`

- 右搖桿支援 `Snap Turn` / `Smooth Turn` 二擇一。
- 支援 Arc Teleport（拋物線落點檢查），放開觸發鍵後瞬移玩家根。
- `ComfortStrength01` 輸出可供 vignette/tunneling 視覺層讀取。

### 遠距抓取：`VRDistanceGrabber`

- 以手部瞄準方向選擇候選剛體，套用吸附速度拉向手部，再委託 `VRGrabber.TryQueueExternalGrab` 建立實際關節。
- 候選評分與吸附速度由 `VRLogic.DistanceGrabRules` 處理，保持可測試性。

### UI 互動：`VRUIPointerRay` / `VRUIPokeInteractor`

- `VRUIPointerRay`：遠距雷射 hover/press。
- `VRUIPokeInteractor`：近距離碰撞戳擊。
- 兩者以 `VRUIInteractable` 作為共同交互代理層。

### 機關操作：Linear / Rotary / Physical Button

- `VRLinearDriveInteractable`：限制單軸位移。
- `VRRotaryDriveInteractable`：限制單軸旋轉角。
- `VRPhysicalButton`：以壓入深度判定按鈕觸發。

### `AlyxFeelTuningDefaults`

- 僅**預設常數與註解**（質量級距、關節策略說明），供關卡與程式對齊調参，非執行期強制。

### 多人（預留）

- `GripReleaseNotification.Publish` 可替換為「僅伺服器轉發」或 RPC 匯流排；**現狀仍為單機 `VRSocket.NotifyGripReleased` 掃場景**。

## Test plan (TDD)

- 單元：`GrabInteractionRules`、`LocomotionWishRules`（閾值邊界、頭向 wish 零／單位化）。
- 迴歸：既有 `VRInteractionRulesTests` 保持綠色。
- 手動：VR／`test.vr.scene` 抓取、插槽、蹲跳（本 spec 不強制自動化整合測試）。

## CI/CD impact

- 新增 `.gitlab-ci.yml`：`dotnet test UnitTests/testbed.unittest.csproj`。
- **注意**：測試專案依賴本機 Steam sbox 路徑之 `ProjectReference`；無 sbox SDK 的 Runner 會失敗，需自建 Runner 或於後續 spec 改為可攜式參考。

## DI plan

- **純邏輯**：`VRLogic` 靜態規則，無 `Scene` 依賴。
- **廣播**：`GripReleaseNotification.Publish` 可於測試或多人模組替換。
- **薄膠水**：`Component` 內不引入完整 DI 容器。
- **介面式 DI（可選路線）**：自 SBox-VR-Controller 移植之 `IVRInputProvider`／`IMovementInputSource` 等已置於 `Libraries/tft.vr.movement`；權威說明見 `docs/references/sbox-vr-controller/VR_DI_ARCHITECTURE.md`，本倉庫移植範圍見 [`vr-input-di-port.md`](./vr-input-di-port.md)。`VRGrabber` 現況仍可直接讀 `Input.VR`；日後可選擇改經 `IControllerInput` 以利替身測試。
- **本體位移互斥**：若使用 `PlayerWalkController*`，與 `VRPlayerController` 擇一啟用，見 [`vr-locomotion-xmovement.md`](./vr-locomotion-xmovement.md)。

## 全身 IK：三點追蹤與 `VRHand` 分工

- **`VRThreePointTracker`**（`Code/VRLogic/VRThreePointTracker.cs`）：每幀將頭／雙手追蹤目標寫入 Avatar 之 `SkinnedModelRenderer`／AnimGraph 約定參數，處理身體 Yaw／XY 跟隨與蹲下比例等；**不**建立 `FixedJoint`、**不**取代 `VRGrabber` 之抓取關節。
- **引擎 `VRHand` + AnimGraph**：手指 curl／手部網格呈現由官方元件與圖驅動；與三點追蹤之手腕／手臂 IK 目標為**分層**關係。
- **規格登錄（SDD）**：行為細節與參數名以 [`vr-three-point-tracker-architecture.md`](./vr-three-point-tracker-architecture.md) 為準；AnimGraph／C++ IK 邊界與單測範圍見 [`vr-animgraph-contract.md`](./vr-animgraph-contract.md)。

## Rollout and rollback

- Rollout：合併後執行 `dotnet test`；手動驗證 `test.vr.scene`。
- Rollout：還原本 spec 相關程式與管線檔；`VRPlayerController` 若行為異常可關閉 `EnableCrouch`／還原舊分支。

## Risks

- CI 在純 dotnet 映像上可能因缺少 sbox 參考而失敗 → 文件化並以自建 Runner 緩解。
- 蹲伏與 VR 相機高度需場景調校 → 預設關閉或低影響。

## Definition of done

- [ ] 本 spec 與 `CHANGELOG.md` 一致。
- [ ] 新增／更新之 `VRLogic` 規則具單元測試且 `dotnet test` 通過。
- [ ] `VRGrabber`／`VRPlayerController` 行為符合上列契約。
- [ ] `.gitlab-ci.yml` 存在且僅跑 unit test 階段。
