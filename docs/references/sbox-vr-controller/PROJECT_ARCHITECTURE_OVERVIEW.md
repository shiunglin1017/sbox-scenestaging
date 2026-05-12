# SBox-VR-Controller 程式架構總覽

> 最後更新：2026-05-12
>  
> 本文件整理專案目前可辨識的程式架構、資料流、核心模組與技術風險，作為開發與重構參考。

## 專案定位

`SBox-VR-Controller` 是一個基於 s&box 的 VR 全身控制專案。  
架構上可視為「抽象 -> 邏輯 -> 服務實作 -> 玩家/武器消費端」的分層設計。

---

## 一、整體分層架構

### 1) 抽象層（Abstractions）

- 位置：`Code/TFT.VR.Abstractions/`
- 內容：介面、列舉、契約定義
- 職責：隔離引擎細節，定義可被邏輯層/服務層實作的標準 API

### 2) 邏輯層（Logic）

- 位置：`Code/TFT.VR.Logic/`
- 內容：可測試的純邏輯（較少直接耦合引擎元件）
- 職責：封裝狀態計算與規則，提升測試性與可維護性

### 3) 服務實作層（Services）

- 位置：`Code/TFT.VR.Services/`
- 內容：與 `Sandbox.Component`、渲染、輸入、網路同步等引擎能力整合
- 職責：把抽象契約落地成 s&box 內可執行元件

### 4) 遊戲功能層（玩家/武器/場景）

- 位置：`Code/Player/`、`Code/Weapon/`、`Assets/prefabs/`、`Assets/scenes/`
- 內容：玩家互動、動畫、移動、射擊、受傷死亡等玩法
- 職責：整合上層功能，形成實際遊戲體驗

---

## 二、目錄職責對照

- `Code/`：主程式碼（玩家、VR、移動、武器、生命值、服務）
- `Editor/`：編輯器相關程式
- `Assets/prefabs/`：預製體配置（例如玩家與互動物件）
- `Assets/scenes/`：場景入口（例如 `Testing.scene`）
- `docs/`：架構、整合與遷移文檔
- `Code/unittest/`：單元測試（xUnit）

---

## 三、核心模組說明

## 玩家互動與動畫

- `Code/Player/VrhandInteraction.cs`
  - 手部互動狀態核心：抓取、持有、放下、切換
  - 常見會與輸入、物件搜尋、持有狀態同步串接
- `Code/Player/VRAnimationHelper.cs`
  - VR 姿勢與動畫對接
  - 主要負責手臂 IK、權重、骨架調整路由

## 移動系統

- `Code/Player/PlayerWalkControllerSimple.cs`
  - 將玩家輸入轉為移動意圖（wish velocity / movement state）
- `Code/Movement/XMovement/PlayerMovement.cs`（及 partial）
  - `PlayerMovement.Physics.cs`
  - `PlayerMovement.SimulatedPhysics.cs`
  - `PlayerMovement.ClassicalPlatforms.cs`
  - 承擔移動、碰撞、平台與物理路徑切換

## 武器與命中流程

- `Code/Weapon/Barrel.cs`
- `Code/Weapon/PistolTrigger.cs`
- `Code/Weapon/PistolSlide.cs`
- `Code/Weapon/MagazineLoader.cs`
- `Code/Weapon/Recoiler.cs`
- `Code/Weapon/BulletProjectile.cs`
- `Code/Health/HealthComponent.cs`

主要鏈路：扳機輸入 -> 發射邏輯 -> 子彈/命中 -> 生命值變更與反饋。

---

## 四、進入點與生命週期

## 進入點

- 場景入口：`Assets/scenes/Testing.scene`
- 玩家預製體：`Assets/prefabs/Player.prefab`

## 生命週期與依賴解析

- 多數元件透過 `Component` 生命週期初始化
- 常見依賴解析方式：
  - `Components.Get<T>(FindMode.EverythingInSelfAndAncestors)`
- 每幀更新通常涵蓋：
  - VR 輸入讀取
  - 手部互動狀態更新
  - 動畫/IK 同步
  - 位移與物理計算

---

## 五、主要資料流

1. VR 控制器輸入（位置、旋轉、按鍵、觸發）
2. 互動狀態機（`VrhandInteraction`）
3. 動畫與 IK 套用（`VRAnimationHelper`）
4. 移動輸出（`PlayerWalkControllerSimple` -> `PlayerMovement`）
5. 武器射擊與命中（Weapon 系列元件）
6. 生命值與結果回饋（`HealthComponent`）
7. 視覺與網路同步（動畫狀態、同步欄位、RPC）

---

## 六、網路同步模型（可見模式）

- 使用 `[Sync]` 同步狀態欄位
- 使用 `[Rpc.Broadcast]` 廣播特定事件
- 以 `IsProxy` 區分本地擁有者與代理端更新行為

此模式代表：本地互動與輸入驅動需要與遠端狀態一致性進行權衡。

---

## 七、測試現況

- 測試專案位置：`Code/unittest/`
- 框架：xUnit
- 目前可推測覆蓋重點：
  - 偏向 `TFT.VR.Logic` 這類純邏輯模組
  - 引擎強耦合路徑（`Component` 生命週期、場景/prefab 接線、網路實測）覆蓋相對不足

---

## 八、目前架構風險與技術債

1. `VrhandInteraction` 與 `VRAnimationHelper` 職責過大，存在「上帝類別」傾向  
2. 命名風格有不一致（大小寫/拼字/語意）  
3. 部分實驗性或原型檔案混在主程式路徑  
4. prefab 與程式碼引用可能存在遺留接線（需定期對照）  
5. 工作樹中有大量編譯輸出/暫存產物，增加 review 與維護成本

---

## 九、建議的下一步

- 先做「模組邊界盤點」：拆分 `VrhandInteraction` 與 `VRAnimationHelper`
- 建立「prefab <-> component 接線檢查表」
- 補齊引擎耦合區的整合測試/冒煙測試
- 清理輸出產物追蹤策略（例如 ignore 規則與提交流程）

