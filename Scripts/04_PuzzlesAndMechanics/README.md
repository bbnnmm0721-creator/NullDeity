#  獨立解謎模組 (Puzzles & Mechanics)

## 系統概述
收錄遊戲中各個獨立的解謎小遊戲與特殊機制。每個模組皆採高內聚設計，內部包含完整的狀態機與通關條件驗證，方便在不同場景中重複配置。

## 核心子模組
* **壁畫調查 (`Mural/`)**: 包含 `PaintingManager.cs` 與 `PaintingHotspot.cs`，處理玩家對壁畫特定區域的放大檢視與多點點擊調查邏輯。
* **密碼與轉盤 (`Password/` & `CircleRotationPuzzle.cs`)**: 實作動態生成符號 (`SymbolPuzzleManager.cs`) 與圓盤旋轉對齊的角度運算機制。
* **夥伴跟隨 (`Sheep/`)**: `SheepCompanion.cs` 實作簡易 AI 跟隨邏輯，並透過 `SheepLuaCommands.cs` 與劇情系統連動。
* **虔敬值系統 (`Piety/`)**: 透過 `PietyManager.cs` 追蹤玩家的行為積分，並影響對應的 UI 反饋與結局分歧。