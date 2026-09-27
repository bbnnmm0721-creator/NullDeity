#  核心架構與全域管理 (Core Architecture)

## 系統概述
遊戲的地基，負責在遊戲啟動時初始化所有必須的全域管理器 (Managers)，並處理跨場景的物件存活狀態。

## 核心腳本與架構
* `GameInitializer.cs`: 確保遊戲從任何場景啟動時，都能自動實例化所有必要的系統模組，方便單一場景測試。
* `GameStateManager.cs`: 定義並廣播遊戲的核心狀態（如：遊玩中、暫停、對話中、解謎中），防止玩家在對話時移動或觸發其他事件。
* `PersistentObject.cs`: 封裝 `DontDestroyOnLoad` 邏輯，套用於需要貫穿整個遊戲生命週期的系統控制器上。