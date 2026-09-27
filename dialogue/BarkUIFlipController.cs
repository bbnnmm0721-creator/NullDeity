using UnityEngine;

public class BarkUIFlipController : MonoBehaviour
{
    [Header("防止漂移设置")]
    [Tooltip("锁定 Bark UI 的本地位置，防止漂移")]
    public bool lockPosition = true;

    private Transform parentTransform;
    private Vector3 originalScale;
    private Vector3 originalLocalPosition;

    void Start()
    {
        parentTransform = transform.parent;
        originalScale = transform.localScale;
        originalLocalPosition = transform.localPosition;

        Debug.Log($"[BarkUIFlipController] 已初始化\n原始縮放: {originalScale}\n原始位置: {originalLocalPosition}");
    }

    void LateUpdate()
    {
        if (parentTransform != null)
        {
            Vector3 parentScale = parentTransform.localScale;
            Vector3 newScale = originalScale;

            // 抵消 X 軸翻轉
            if (parentScale.x < 0)
            {
                newScale.x = -Mathf.Abs(originalScale.x);
            }
            else
            {
                newScale.x = Mathf.Abs(originalScale.x);
            }

            transform.localScale = newScale;
        }

        // 锁定位置防止漂移
        if (lockPosition)
        {
            transform.localPosition = originalLocalPosition;
        }
    }
}
