using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 全域相機狀態管理器，通知所有需要的組件
/// </summary>
public class CameraStateManager : MonoBehaviour
{
    [System.Serializable]
    public class CameraStateEvent : UnityEvent<bool> { }  // bool: isMainCamera

    public static CameraStateManager Instance { get; private set; }

    [Header("Current State")]
    public bool isMainCameraActive = true;

    [Header("Events")]
    public CameraStateEvent onCameraStateChanged = new CameraStateEvent();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[CameraStateManager] 全域相機狀態管理器已建立");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 設定相機狀態並通知所有訂閱者
    /// </summary>
    public void SetMainCameraActive(bool active)
    {
        if (isMainCameraActive != active)
        {
            isMainCameraActive = active;
            Debug.Log($"[CameraStateManager] 相機狀態變更: MainCamera = {active}");
            onCameraStateChanged?.Invoke(active);
        }
    }

    /// <summary>
    /// 靜態方法，方便其他腳本調用
    /// </summary>
    public static void SetCameraState(bool isMainActive)
    {
        if (Instance != null)
        {
            Instance.SetMainCameraActive(isMainActive);
        }
    }
}
