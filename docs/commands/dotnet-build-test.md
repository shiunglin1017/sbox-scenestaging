# 指令：建置與單元測試（testbed）

於倉庫根目錄 `sbox-scenestaging` 執行（路徑含 `&` 時請保留引號）。

## 建置主遊戲專案

```powershell
dotnet build "K:\S&boxProject\sbox-scenestaging\Code\testbed.csproj"
```

## 建置 VR 位移／DI 函式庫（獨立）

```powershell
dotnet build "K:\S&boxProject\sbox-scenestaging\Libraries\tft.vr.movement\Code\tft.vr.movement.csproj"
```

## 單元測試（CI 同指令）

建議直接執行（會一併建置測試專案與依賴）：

```powershell
dotnet test "K:\S&boxProject\sbox-scenestaging\UnitTests\testbed.unittest.csproj"
```

若已完整建置過測試專案，可省略測試專案建置：

```powershell
dotnet test "K:\S&boxProject\sbox-scenestaging\UnitTests\testbed.unittest.csproj" --no-build
```

**注意**：僅建置 `testbed.csproj` 時，輸出目錄可能尚無 `testbed.unittest.dll`，此時 `--no-build` 會失敗；請先對 `testbed.unittest.csproj` 執行過 `dotnet build` 或 `dotnet test`（無 `--no-build`）。

## 依賴

`testbed.csproj` 與 `testbed.unittest.csproj` 使用相對路徑參考本機 Steam 安裝之 s&box managed 組件；路徑不符時請調整 `.csproj` 或於該機建立對應目錄連結。

## 相關規格

- `docs/specs/vr-unit-test-plan.md`
- `docs/specs/vr-input-di-port.md`
- `docs/CI_UNIT_TEST.md`（若存在）
