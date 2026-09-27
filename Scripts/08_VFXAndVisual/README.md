#  視覺特效與渲染 (VFX & Visuals)

## 系統概述
包含遊戲內特殊視覺表現的腳本與 Shader 控制器，處理粒子生成、材質同步與程序化網格生成。

## 核心腳本與架構
* `DitherSpriteSync.cs`: 同步角色或物件的半透明 Dither (抖動) 效果，優化效能。
* `ProximityDuplicateOutline.cs`: 基於玩家距離動態調整物件的外框線或重影特效，提示可互動性。
* `ParticleField.cs` & `StarTrailsPureMesh.cs`: 使用純程式碼與 Mesh 即時生成星軌或環境粒子特效，減少對預設 Particle System 的依賴。