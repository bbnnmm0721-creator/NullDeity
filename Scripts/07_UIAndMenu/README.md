#  通用介面與主選單 (UI & Menu)

## 系統概述
處理遊戲外圍的 UI 系統，包含主選單流程、設定選項輪播以及通用的 UI 動畫表現。

## 核心腳本與架構
* `MenuManager.cs` & `GameResetBootstrap.cs`: 管理遊戲啟動流程，包含按鍵監聽 (`PressAnyKeyToStart.cs`) 與存檔初始化。
* `SettingsMenuManager.cs` & `SettingsCarousel.cs`: 實作可左右輪播的設定選項介面。
* `CanvasGroupFader.cs`: 通用的 UI 漸變工具腳本，提供各系統快速調用 UI 的顯隱動畫。
* `SoftwareCursorConfined.cs`: 客製化滑鼠游標圖案，並將游標限制在遊戲視窗範圍內。