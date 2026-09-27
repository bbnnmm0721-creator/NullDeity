using UnityEngine;

/// 場景出生點：位置 + 朝向 + ID + 攝影機等待
public class SceneSpawnPoint : MonoBehaviour
{
    [Tooltip("出生點的唯一 Id（字串不區分大小寫）")]
    public string id = "Default";

    [Tooltip("出生後是否面朝右邊，false則朝向 +X（左）")]
    public bool faceRight = true;
    
    [Header("🎥 攝影機等待設定")]
    [Tooltip("是否需要等待攝影機 Zoom 完成")]
    public bool waitForCameraZoom = false;
    
    [Tooltip("最大等待時間（秒）- 避免卡死")]
    [Range(0.5f, 10f)]
    public float maxWaitTime = 3f;

    // 讓設計師在 Scene 中可視化
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // 基本出生點顯示
        Gizmos.color = waitForCameraZoom ? Color.yellow : Color.green;
        Gizmos.DrawSphere(transform.position, 0.2f);
        
        // 顯示朝向
        Vector3 direction = faceRight ? Vector3.right : Vector3.left;
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, direction * 0.5f);
        
        // 標籤
        string label = id;
        if (waitForCameraZoom) label += " 📹";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, label);
        
        // 如果需要等待攝影機，畫一個圈表示
        if (waitForCameraZoom)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
#endif
}
