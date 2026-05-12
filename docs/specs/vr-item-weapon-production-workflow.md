# Spec：VR 物件／槍械製作流程（ModelDoc → Prefab → 場景）

## 目的與範圍

從「要做一個可抓物／一把槍」到「ModelDoc → Prefab → 場景 → VR 驗證」的**單一路線圖**，對齊本專案 **幽靈手**（`VRGhostHandTarget`）、**Interactor**（`VRGrabber`）、**全身 IK 目標**（`VRThreePointTracker`）。行為細分類見 [vr-weapon-taxonomy.md](./vr-weapon-taxonomy.md)。

## 前提與主軸元件

| 角色 | 元件／文件 |
|------|------------|
| 幽靈手目標 | `Code/test/VR/VRGhostHandTarget.cs` |
| 抓取／關節 | `Code/test/VR/VRGrabber.cs` |
| 插槽 | `Code/test/VR/VRSocket.cs` |
| 可抓標記／握點 | `Code/test/VR/Grabbable.cs` |
| 集中 Inspector 設定 | `Code/test/VR/VRItemInteractionProfile.cs`（質量預設、多握點、attachment 對照、姿勢提示欄位）；規格見 [`vr-editor-item-interaction-facade.md`](./vr-editor-item-interaction-facade.md) |
| 常數 | `VrInteractionConstants.DefaultGripAttachmentName`（預設 `weapon_hold`） |
| 手感參考 | `AlyxFeelTuningDefaults` |
| 架構總覽 | 根目錄 `PROJECT_ARCHITECTURE_OVERVIEW.md` |
| 三點追蹤 IK | `docs/specs/vr-three-point-tracker-architecture.md` |

**與 SBox-VR-Controller 差異**：上游之 `GrabPoint`／`VrhandInteraction` 武器鏈**無法**直接套用到本倉庫 `VRGrabber`；對照表見下節與 [references/sbox-vr-controller/README.md](../references/sbox-vr-controller/README.md)。

## 總流程

1. **美術**：FBX／素材導出。  
2. **ModelDoc**：mesh、material、**physics shape**（凸包／簡化碰撞）、**attachments**（握點、瞄具、槍口等）。  
3. **Prefab**：`Rigidbody`、`Collider`、`Grabbable`、可選 `VRItemInteractionProfile`、武器邏輯（如 `Libraries/weaponlab/Code/TestWeapon.cs`）。  
4. **場景**：擺放、玩家 rig 與 `test.vr`／weaponlab 場驗證。  
5. **手動 VR**：見 `docs/commands/vr-three-issues-validation.md`。

## 一般可抓物（非槍）

- **ModelDoc**：優先簡化碰撞；避免過重 `ModelCollider`（參考官方 Physics／Model Editor 文件）。  
- **Prefab**：`VRItemInteractionProfile` 可提供質量預設（Light／Medium／Heavy）；僅在 override 開關啟用時才覆寫 `Rigidbody.MassOverride`／阻尼／surface，預設保留 ModelDoc/Prefab 原值。主握點子物件拖入 `GrabPoints` 並勾 `SyncPrimaryPivotToGrabbable` 可自動寫入 `Grabbable.GrabPivot`。  
- **Trigger**：手部需 Trigger Collider，`VRGrabber` 才會 Hover。

## 握持點（Grab point）

- **推薦**：在 ModelDoc 建立 attachment，名稱與 `VRGrabber.AttachmentName`／`VRGhostHandTarget.AttachmentName` **完全一致**（含大小寫），預設 `weapon_hold`。  
- **程式對齊**：`Grabbable.GrabPivot` 指向場景中對應世界姿態之子物件；或由 `VRItemInteractionProfile` 同步。

## 決策條文（抓取姿勢權威）

- `VRGrabber.ComputeGrabPose` 之姿勢來源優先序固定為：**Attachment > GrabPivot > 幾何 fallback**。  
- `Attachment` 由 `Grabbable` 上之 `GrabAttachmentName`（預設 `VrInteractionConstants.DefaultGripAttachmentName`）解析 `SkinnedModelRenderer.GetAttachment`；找不到時才退回 `GrabPivot`。  
- `GrabPivot` 不可用時，才使用既有 edge/幾何 fallback，確保沒有設定資料時仍可抓取。
- Hover 階段若走幾何 fallback，允許使用中心射線距離驅動手指 curl（視覺-only），不影響物理抓取門檻。

## 決策條文（物理預設權威）

- **ModelDoc/Prefab 是物理預設權威**：質量、阻尼、surface 以資產原始設定為準。  
- `VRItemInteractionProfile` 僅在對應 override 開關啟用時覆寫（`OverrideMass`、`OverrideDamping`、`OverrideSurface`）；未啟用時必須保留原始設定。  
- `ApplyRigidbodyDefaultsOnAwake` 僅控制「是否嘗試套用 Profile」，不改變各 override gate 的判定。

## 槍械：換彈／裝彈

- **簡化（weaponlab）**：`TestWeapon` 之彈量、`ReloadTime`、`SingularReload`；VR 按鍵需另接 `Input.VR` 或日後 `IControllerInput`（見 `Libraries/tft.vr.movement`）。  
- **擬真（上游 Magazine 鏈）**：依 `GrabPoint.Held` 等，**需適配層**才能與 `VRGrabber` 併用；列為第二階段，非即插即用。

## 瞄準與雙手

- `TestWeapon` 之 `TwoHanded`、`UseJoystick` 等與副手幽靈／追蹤、`VRThreePointTracker` 之雙手 IK 參數分層設定；ADS 與 AnimGraph 見 [vr-animgraph-contract.md](./vr-animgraph-contract.md)。

## 射擊系統選型

| 方式 | 說明 |
|------|------|
| Trace | `Scene.Trace` 類（`TestWeapon` 現況） |
| Projectile | 上游 `BulletProjectile` 等可評估抽離 `GrabPoint` 依賴後再併入 |

**注意**：上游 `PistolTrigger` 依賴 `GrabPoint` + `IControllerInput`；在未搬 `VrhandInteraction` 前**不可原樣掛載**。

## 上游元件「能否直接用」對照

| 元件／概念 | 直接複製到 scenestaging |
|------------|-------------------------|
| `Barrel`／`BulletProjectile`（無 GrabPoint 硬依賴時） | 可評估 |
| `PistolTrigger`、`GrabPoint`、`MagazineLoader` | 需適配 `VRGrabber` + 輸入抽象 |
| `TestWeapon`（weaponlab） | 本倉庫建議先擴此路線 |

## 檢核清單

- `PROJECT_ARCHITECTURE_OVERVIEW.md` 內 VRGrabber／Socket 小節。  
- 槍：扳機／換彈綁定、彈匣 Socket、Muzzle attachment 命名。  
- 位移：`docs/specs/vr-locomotion-xmovement.md`（`XMovement` 與 `VRPlayerController` 擇一）。
- Cloud prop：若使用 `AutoPropAdapterSystem`，確認掃描只補最小組件（`Rigidbody`、`Collider`、`Grabbable`），且不預設覆寫物理權威。
- 參考規格：`docs/specs/vr-cloud-prop-auto-adapter.md`（雲端資產零/低設定適配策略）。

## 外部參考

- [sbox-docs VR](https://docs.facepunch.com/s/sbox-dev/doc/vr-GPhXAmcHLM)（錨點、`VRTrackedObject`、`VRHand`）。  
- 本倉庫 `docs/references/sbox-vr-controller/`。
