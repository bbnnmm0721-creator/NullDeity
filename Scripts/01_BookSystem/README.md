書本解謎與背包系統

##系統概述
本模組為遊戲的核心解謎機制，負責處理玩家的背包資料、書本介面的互動（翻頁、查看），以及將道具拖曳至場景中進行解謎的邏輯。
系統採用資料與介面分離的設計模式，確保後續擴充的便利性。

##核心腳本與架構
=> 資料管理
  "ItemDatabase.cs" & "ItemData.cs": 透過 ScriptableObject 建立靈活的道具資料庫，企劃可直接在 Inspector 中新增或修改道具屬性與圖示。
  "InventoryManager.cs": 負責玩家持有道具的狀態管理（新增、移除、查詢），並提供 API 供解謎系統驗證條件。
=> UI與互動
  "BookPageController.cs" & "EvenBookUI.cs": 控制書本的開合、翻頁動畫及當前頁面狀態的更新。
  "AskBook/DraggableItemIcon.cs" & "TransDropTarget.cs": 實作拖曳介面（Drag & Drop）邏輯，運用射線檢測 (Raycast) 判斷道具是否正確放置於解謎目標上。
  "SparkleInteractableItem.cs": 場景中可互動與收集的物件基底，處理拾取後的狀態變更與視覺特效（如 "WordWaterEffect/" 的文字落水特效）。