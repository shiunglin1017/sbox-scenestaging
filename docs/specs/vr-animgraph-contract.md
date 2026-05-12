# Spec：AnimGraph／C++ IK 與 C# 契約邊界

## 目的

釐清 **`VRThreePointTracker`**（與 `VRHand` 圖）寫入 AnimGraph 的**參數契約**，以及 **C++ 底層** 責任邊界，避免誤以為 C# 單元測試可證明 IK 正確。

## 契約層（本 repo 可控）

權威實作說明：`docs/specs/vr-three-point-tracker-architecture.md`。

- **C# 責任**：每幀（`OnUpdate`）從 Tracker 讀取姿態，透過 `SkinnedModelRenderer.Set` 寫入約定之參數名（如 `head_target_pos`、`hand_l_*`／`hand_r_*`、`crouch` 等，以實際 vmdl／AnimGraph 為準）。  
- **單元測試**：僅適用「純推導」之常數／命名／簡單數學；**不**強制覆蓋整段 `VRThreePointTracker` 之 Scene 整合。

## 引擎層（C++）

| 情境 | 專案責任 |
|------|----------|
| 僅使用官方／既有 AnimGraph 節點（如文件或引擎內建 IK 節點） | 無需本倉庫 C++；在 spec 註明所用節點與 s&box **版本釘選**。 |
| 需要**自訂 C++ AnimGraph 節點** | 獨立引擎分支／建置管線；**不可**假設僅改 scenestaging 即可發布。 |

## 與 `VRGrabber`／`VRHand` 分工

- **`VRGrabber`**：物理關節與抓取狀態；**不**負責手指 curl 呈現。  
- **引擎 `VRHand` + AnimGraph**：手指與手部圖形；與三點追蹤之手腕／手肘 IK 目標分層。  
- 階層與追蹤對齊見 `vr-three-point-tracker-architecture.md` 之「場景與座標約定」。

## 變更流程

1. 更動 `SkinnedModelRenderer.Set` 參數名或空間約定 → 同步修訂 **本 spec** 與 **vr-three-point-tracker-architecture.md**。  
2. 更動 vmdl／AnimGraph 資產 → 在 MR 註明引擎版本與資產路徑。
