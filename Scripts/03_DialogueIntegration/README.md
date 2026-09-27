#  對話系統整合與擴充 (Dialogue System Integration)

## 系統概述
本模組深度整合了第三方對話套件（如 Dialogue System for Unity），不僅客製化了對話框 UI，更透過撰寫 Lua 擴充函數與 Sequencer 腳本，讓對話系統能夠直接與遊戲內的其他系統（音效、場景、任務）進行互動。

## 核心腳本與架構
* **第三方套件擴充 (API & Lua Integration)**
  * `DialogueSequencerCommands.cs`: 擴展對話系統的過場指令，讓企劃能在文本中直接觸發特定的角色動畫或運鏡。
  * `BGM/BGMLuaFunctions.cs` 等（已整合）: 將 C# 方法註冊至對話系統的 Lua 環境中，實作在對話節點中動態切換 BGM 或派發事件。
* **自定義介面 (Custom UI)**
  * `CustomDialogueUIController.cs`: 覆寫預設的對話介面邏輯，支援動態立繪顯示 (`UseAnimatedPortraitsMultiPanel.cs`)。
  * `BacklogUI.cs` & `DSBacklog.cs`: 實作對話歷史回顧系統，自動記錄走過的文本節點供玩家隨時查閱。
  * `ScreenSpaceBarkUI.cs`: 處理 NPC 頭頂的浮動對話框（Bark），並實作自動翻轉 (`BarkUIFlipController.cs`) 以配合攝影機視角。