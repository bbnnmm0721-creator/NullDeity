using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsCarousel : MonoBehaviour
{
    [Header("選項")]
    public List<RectTransform> items;
    public float spacing = 420f;
    public float centerScale = 1.0f;
    public float sideScale = 0.75f;
    public float sideAlpha = 0.35f;

    [Header("滑動/慣性")]
    public float smoothTime = 0.18f;
    public int startIndex = 0;

    [Header("動畫回調")]
    public SettingsMenuManager menuManager;
    public float animationLockTime = 0.6f;
    public bool triggerPageChange = true;

    private int count;
    private float pos;
    private float vel;
    private int targetIndex;
    private bool isAnimating = false;
    private bool isEnabled = false;

    void Awake()
    {
        if (items == null || items.Count == 0)
        {
            items = new List<RectTransform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var rt = transform.GetChild(i) as RectTransform;
                if (rt) items.Add(rt);
            }
        }

        foreach (var rt in items)
        {
            if (!rt.GetComponent<CanvasGroup>())
                rt.gameObject.AddComponent<CanvasGroup>();
        }

        count = items.Count;
        targetIndex = Mathf.Clamp(startIndex, 0, count - 1);
        pos = targetIndex;

        Debug.Log($"[SettingsCarousel] 初始化完成，選項數量: {count}");

        LayoutImmediate();
    }

    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        Debug.Log($"[SettingsCarousel] {gameObject.name} 輸入: {(enabled ? "啟用" : "禁用")}");

        if (enabled)
        {
            isAnimating = false;
            StopAllCoroutines();
        }
    }

    public void ResetToIndex(int index)
    {
        targetIndex = Mathf.Clamp(index, 0, count - 1);
        pos = targetIndex;
        vel = 0f;
        isAnimating = false;
        StopAllCoroutines();
        LayoutImmediate();
        Debug.Log($"[SettingsCarousel] {gameObject.name} 重置到索引: {index}");
    }

    void Update()
    {
        if (!isEnabled) return;

        pos = Mathf.SmoothDamp(pos, WrapIndex(targetIndex), ref vel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        Layout();
    }

    public void Move(int dir)
    {
        if (count == 0 || isAnimating || !isEnabled) return;

        int oldIndex = targetIndex;
        targetIndex = (targetIndex + (dir % count) + count) % count;

        if (oldIndex != targetIndex)
        {
            Debug.Log($"[SettingsCarousel] {gameObject.name} 移動: {oldIndex} → {targetIndex}");

            if (triggerPageChange && menuManager)
            {
                isAnimating = true;
                menuManager.OnPageChanged(targetIndex);
                StartCoroutine(UnlockAnimationAfterDelay());
            }
        }
    }

    public void SelectCurrent()
    {
        Debug.Log($"[SettingsCarousel] {gameObject.name} 選擇項目: {targetIndex}");
    }

    private IEnumerator UnlockAnimationAfterDelay()
    {
        yield return new WaitForSecondsRealtime(animationLockTime);
        isAnimating = false;
        Debug.Log($"[SettingsCarousel] {gameObject.name} 動畫解鎖");
    }

    int WrapIndex(int i) => (i % count + count) % count;

    void Layout()
    {
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            float raw = i - pos;
            raw = Mathf.Repeat(raw + count * 0.5f, count) - count * 0.5f;

            var rt = items[i];
            rt.anchoredPosition = new Vector2(raw * spacing, 0f);

            float t = Mathf.Clamp01(1f - Mathf.Abs(raw));
            float scale = Mathf.Lerp(sideScale, centerScale, t);
            rt.localScale = Vector3.one * scale;

            var cg = rt.GetComponent<CanvasGroup>();
            cg.alpha = Mathf.Lerp(sideAlpha, 1f, t);
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        int center = WrapIndex(Mathf.RoundToInt(pos));
        var centerCg = items[center].GetComponent<CanvasGroup>();
        centerCg.alpha = 1f;
        centerCg.interactable = true;
        centerCg.blocksRaycasts = true;
    }

    void LayoutImmediate()
    {
        float tmpVel = 0f;
        pos = Mathf.SmoothDamp(pos, WrapIndex(targetIndex), ref tmpVel, 0f);
        Layout();
    }

    public int GetCurrentIndex() => targetIndex;
}
