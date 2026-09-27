#  音訊管理系統 (Audio Management)

## 系統概述
集中管理遊戲內的 BGM 與 SFX，提供統一的調用介面，避免音訊資源的重複載入與衝突。

## 核心腳本與架構
* `BGMManager.cs`: 採 Singleton 模式全域存活，處理音樂的淡入淡出 (Crossfade) 與音量控制。
* `SceneMusicConfig.cs`: 方便企劃在不同場景掛載，進入場景時自動讀取並播放對應配置的 BGM。
* `FootstepAudioController.cs`: 依據角色的移動狀態或動畫事件 (Animation Events)，隨機播放腳步聲音效以增加沉浸感。