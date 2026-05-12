# SBox-VR-Controller 技術文件複本（參考用）

本目錄為上游專案 **SBox-VR-Controller** 之 `docs/` 複本，供 sbox-scenestaging 離線對照與 SDD 引用。

| 同步日期 | 2026-05-12 |
|----------|------------|
| 上游路徑 | `SBox-VR-Controller/docs/`（本機工作區同層目錄） |

## 檔案索引

| 檔案 | 摘要 |
|------|------|
| [VR_DI_ARCHITECTURE.md](./VR_DI_ARCHITECTURE.md) | 介面式 DI：`IVRInputProvider`、`Components.Get` 解析慣例；**非** MS DI 容器。 |
| [VR_OFFICIAL_API_INTEGRATION.md](./VR_OFFICIAL_API_INTEGRATION.md) | 與官方 VR API 對齊之整合說明。 |
| [VR_OFFICIAL_IK_MIGRATION.md](./VR_OFFICIAL_IK_MIGRATION.md) | 官方 IK／遷移相關筆記。 |
| [MODEL_AUTHORING_GUIDE.md](./MODEL_AUTHORING_GUIDE.md) | ModelDoc／資產製作指引。 |
| [VR_TROUBLESHOOTING.md](./VR_TROUBLESHOOTING.md) | VR 除錯與常見問題。 |
| [VR_JOINT_WEIGHT_IMPLEMENTATION.md](./VR_JOINT_WEIGHT_IMPLEMENTATION.md) | 關節重量感實作細節。 |
| [VR_JOINT_WEIGHT_IMPLEMENTATION_BRIEF.md](./VR_JOINT_WEIGHT_IMPLEMENTATION_BRIEF.md) | 關節重量感簡版。 |
| [PROJECT_ARCHITECTURE_OVERVIEW.md](./PROJECT_ARCHITECTURE_OVERVIEW.md) | 上游專案架構總覽（與本倉庫根目錄 `PROJECT_ARCHITECTURE_OVERVIEW.md` 為不同文件）。 |
| [vr-migration-validation.md](./vr-migration-validation.md) | 遷移／驗證檢查清單。 |

## 程式對照（本倉庫）

本專案已將 **XMovement** 與 **DI 核心服務** 置於函式庫：

- `Libraries/tft.vr.movement/Code/XMovement/`
- `Libraries/tft.vr.movement/Code/Player/Abstractions/`
- `Libraries/tft.vr.movement/Code/Player/Services/`（`SandboxVRInputProvider`、`VRControllerAdapter`、`NullController`、`VRMovementInputSource`、`KeyboardMovementInputSource`、`CompositeMovementInputSource`）

`VR_DI_ARCHITECTURE.md` 內程式連結已對齊上述路徑；其餘上游專用檔案於文中標為「上游」或「未移植」。互動堆疊差異見下節。

## 互動堆疊差異（重要）

sbox-scenestaging 之 VR 互動主軸為 **`VRGrabber`／`VRSocket`／`VRGhostHandTarget`**（見 `docs/specs/2026-05-05-vr-interaction-stack.md`），**未**整包移植 `VrhandInteraction` 與 `GrabPoint` 武器鏈。武器與 Alyx 向流程見 `docs/specs/vr-item-weapon-production-workflow.md` 與 `docs/specs/vr-weapon-taxonomy.md`。
