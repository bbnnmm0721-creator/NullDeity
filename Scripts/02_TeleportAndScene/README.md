# 場景傳送與生成系統 (Teleport & Scene Transition)

## 系統概述
本模組負責遊戲中所有的場景切換、重生點管理及無縫過場表現。透過非同步載入技術，確保玩家在切換地圖時不會產生畫面卡頓，並妥善保留跨場景的資料。

## 核心腳本與架構
=>**過場與載入控制**
  * `SceneLoader.cs`: 封裝 Unity 的 `SceneManager.LoadSceneAsync`，處理場景的非同步載入與進度條回報。
  * `GlobalSceneTransition.cs` & `FadeObstacleController.cs`: 全域轉場管理器，利用 CanvasGroup 實作平滑的淡入淡出（Fade In/Out）黑畫面效果。
=>**傳送邏輯與資料**
  * `InteractiveScenePortal.cs` & `QuestSceneTeleporter.cs`: 定義場景傳送門的觸發條件。
  * `GlobalSpawnManager.cs` & `TravelData.cs`: 記錄玩家跨場景時的目標重生點 (Spawn Point) 與面向方向，確保玩家載入新場景後出現在正確的位置。