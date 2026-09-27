#  角色控制與攝影機 (Player & Camera)

## 系統概述
負責處理玩家輸入、物理移動、階梯判斷以及攝影機跟隨邏輯。全面採用 Unity 新版 Input System，便於未來擴充搖桿支援。

## 核心腳本與架構
* **玩家控制器**
  * `PlayerInputActions.cs` & `PlayerMovement.cs`: 讀取 Input System 訊號，結合 Rigidbody2D/3D 進行平滑移動，並處理動畫狀態的切換。
  * `StairSystem.cs` & `SecondFloorDetector.cs`: 解決 2.5D 視角常見的高低差與階梯移動問題，自動調整角色的 Z 軸層級與移動向量。
* **攝影機與視覺**
  * `SideScrollCamera.cs` & `CameraStateManager.cs`: 實作基於邊界限制 (Clamp) 的跟隨攝影機，並可根據場景區域動態切換狀態。
  * `ZoomZoneTrigger.cs`: 讓玩家進入特定區域時，攝影機平滑拉近或拉遠以強調視覺重點。