using UnityEngine;

/// 把它掛在任何物件上，場景切換時該物件不會被銷毀。
public class PersistentObject : MonoBehaviour
{
    void Awake()
    {
        // 若同類物件已存在，選擇保留第一個並刪掉後來的複本（可選）
        

        DontDestroyOnLoad(gameObject);
    }
}
