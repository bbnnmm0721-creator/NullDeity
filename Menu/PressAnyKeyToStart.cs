using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class PressAnyKeyToStart : MonoBehaviour
{
    [Header("References")]
    public Animator cameraAnimator;
    public GameObject pressKeyText;
    public MenuManager menuManager;

    [Header("額外隱藏物件（同場景）")]
    [Tooltip("按下任意鍵時，與 Press Any Button 一同隱藏的物件（例如標題）")]
    public GameObject[] additionalHideOnStart;

    [Header("封面時隱藏的 Persistent HUD（跨場景）")]
    [Tooltip("DontDestroyOnLoad 物件路徑，用 GameObject.Find 查找，例如：BookRoot、Dialogue Manager/Canvas/Slider")]
    public string[] crossSceneHidePaths;

    [Tooltip("Quest Tracker HUD 父物件路徑，例如 \"Dialogue Manager/Canvas\"")]
    public string questTrackerContainerPath = "Dialogue Manager/Canvas";

    [Tooltip("需要隱藏的 Quest Tracker HUD 名稱前綴")]
    public string questTrackerPrefix = "Basic Standard Quest Tracker HUD";

    [Header("Settings")]
    [Tooltip("要等待完成的动画状态名（可选，留空则等待整个动画序列）")]
    public string targetStateName = "camera";

    [Tooltip("是否在动画结束后禁用 Animator")]
    public bool disableAnimatorAfterPlay = true;

    [Tooltip("Animator 所在的 Layer 索引")]
    public int animatorLayer = 0;

    [Header("Skip Settings")]
    [Tooltip("是否允许从游戏返回时跳过开场动画")]
    public bool allowSkipOnReturn = true;

    [Tooltip("跳过动画时相机的目标位置")]
    public Vector3 finalCameraPosition = new Vector3(0f, -2.9f, 1.6f);

    [Tooltip("跳过动画时相机的目标旋转")]
    public Vector3 finalCameraRotation = new Vector3(0f, 0f, 0f);

    private bool hasStarted = false;

    // 跨場景 lazy cache
    private List<Transform> _crossSceneTargets;
    private Transform _questTrackerContainer;

    void Start()
    {
        // 進入封面，立刻隱藏 Persistent HUD
        SetPersistentHudVisible(false);

        if (allowSkipOnReturn && BackToMenuManager.SkipIntroAnimation)
        {
            Debug.Log("[PressAnyKeyToStart] 🚀 检测到返回标志，跳过开场动画");
            SkipAnimation();
            BackToMenuManager.SkipIntroAnimation = false;
            return;
        }

        if (cameraAnimator != null)
            cameraAnimator.enabled = false;

        if (menuManager == null)
            menuManager = FindObjectOfType<MenuManager>();

        if (menuManager != null)
        {
            menuManager.SetNavigationEnabled(false);
            if (menuManager.showDebugLogs)
                Debug.Log("[PressAnyKeyToStart] MenuManager 导航已禁用，等待开场动画完成");
        }
    }

    void OnDestroy()
    {
        // Menu 場景卸載（玩家開始遊戲）時恢復 Persistent HUD
        SetPersistentHudVisible(true);
    }

    void Update()
    {
        if (!hasStarted && Input.anyKeyDown)
            StartAnimation();
    }

    // ─── 跨場景 HUD 控制 ───────────────────────────────────────────────────────

    /// <summary>
    /// 解析 crossSceneHidePaths 並快取 Transform。
    /// DontDestroyOnLoad 物件可被 GameObject.Find 找到。
    /// </summary>
    private List<Transform> GetCrossSceneTargets()
    {
        if (_crossSceneTargets != null)
            return _crossSceneTargets;

        _crossSceneTargets = new List<Transform>();

        if (crossSceneHidePaths == null) return _crossSceneTargets;

        foreach (var path in crossSceneHidePaths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            var go = GameObject.Find(path);
            if (go != null)
            {
                _crossSceneTargets.Add(go.transform);
                Debug.Log($"[PressAnyKeyToStart] 跨場景目標快取: {path}");
            }
            else
            {
                Debug.LogWarning($"[PressAnyKeyToStart] 找不到跨場景目標: {path}");
            }
        }

        return _crossSceneTargets;
    }

    /// <summary>查找並快取 Quest Tracker 的父容器。</summary>
    private Transform GetQuestTrackerContainer()
    {
        if (_questTrackerContainer != null)
            return _questTrackerContainer;

        if (string.IsNullOrEmpty(questTrackerContainerPath))
            return null;

        var go = GameObject.Find(questTrackerContainerPath);
        if (go != null)
        {
            _questTrackerContainer = go.transform;
            Debug.Log($"[PressAnyKeyToStart] Quest Tracker 容器快取: {questTrackerContainerPath}");
        }
        else
        {
            Debug.LogWarning($"[PressAnyKeyToStart] 找不到 Quest Tracker 容器: {questTrackerContainerPath}");
        }

        return _questTrackerContainer;
    }

    /// <summary>設定所有 Persistent HUD 的顯示狀態。</summary>
    private void SetPersistentHudVisible(bool visible)
    {
        foreach (var target in GetCrossSceneTargets())
        {
            if (target == null) continue;
            ApplyVisibility(target, visible);
        }

        if (!string.IsNullOrEmpty(questTrackerPrefix))
        {
            var container = GetQuestTrackerContainer();
            if (container != null)
            {
                foreach (Transform child in container)
                {
                    if (child.name.StartsWith(questTrackerPrefix))
                        ApplyVisibility(child, visible);
                }
            }
        }
    }

    /// <summary>優先使用 CanvasGroup 控制能見度，否則退回 SetActive。</summary>
    private void ApplyVisibility(Transform target, bool visible)
    {
        var cg = target.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }
        else
        {
            target.gameObject.SetActive(visible);
        }
    }

    // ─── 同場景物件隱藏 ───────────────────────────────────────────────────────

    /// <summary>隱藏 Press Any Button 及 additionalHideOnStart 中的同場景物件。</summary>
    private void HideStartObjects()
    {
        if (pressKeyText != null)
            pressKeyText.SetActive(false);

        if (additionalHideOnStart != null)
        {
            foreach (var go in additionalHideOnStart)
            {
                if (go != null) go.SetActive(false);
            }
        }
    }

    // ─── 動畫流程 ─────────────────────────────────────────────────────────────

    void SkipAnimation()
    {
        hasStarted = true;

        if (cameraAnimator != null)
            cameraAnimator.enabled = false;

        // WaitForEndOfFrame で全スクリプトの LateUpdate 後に強制設定
        StartCoroutine(ForceApplyCameraPosition());

        HideStartObjects();
        EnableMenuNavigation();
        enabled = false;
    }

    IEnumerator ForceApplyCameraPosition()
    {
        // 2 フレーム待って他スクリプトの初期化後に上書き
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        Transform camTransform = cameraAnimator != null
            ? cameraAnimator.transform
            : Camera.main?.transform;

        if (camTransform != null)
        {
            camTransform.localPosition = finalCameraPosition;
            camTransform.localRotation = Quaternion.Euler(finalCameraRotation);
            Debug.Log($"[PressAnyKey] 強制設定相機位置: {camTransform.localPosition}");
        }
    }



    void StartAnimation()
    {
        hasStarted = true;

        if (cameraAnimator != null)
        {
            cameraAnimator.enabled = true;
            Debug.Log("[PressAnyKeyToStart] Animator 已启用，开始播放动画序列");
        }

        HideStartObjects();
        StartCoroutine(WaitForAnimationComplete());
        enabled = false;
    }

    IEnumerator WaitForAnimationComplete()
    {
        if (cameraAnimator == null)
        {
            EnableMenuNavigation();
            yield break;
        }

        yield return null;

        bool targetStateReached = false;
        float statePlayTime = 0f;
        float stateDuration = 0f;

        if (string.IsNullOrEmpty(targetStateName))
        {
            AnimatorStateInfo currentState = cameraAnimator.GetCurrentAnimatorStateInfo(animatorLayer);
            stateDuration = currentState.length;
            Debug.Log($"[PressAnyKeyToStart] 等待当前动画完成，时长: {stateDuration} 秒");
            yield return new WaitForSeconds(stateDuration);
        }
        else
        {
            Debug.Log($"[PressAnyKeyToStart] 等待动画播放到状态: {targetStateName}");

            while (!targetStateReached)
            {
                yield return null;

                AnimatorStateInfo currentState = cameraAnimator.GetCurrentAnimatorStateInfo(animatorLayer);

                if (currentState.IsName(targetStateName))
                {
                    if (!targetStateReached)
                    {
                        targetStateReached = true;
                        stateDuration = currentState.length;
                        Debug.Log($"[PressAnyKeyToStart] ✅ 到达目标状态: {targetStateName}，时长: {stateDuration} 秒");
                    }

                    statePlayTime += Time.deltaTime;

                    if (statePlayTime >= stateDuration)
                    {
                        Debug.Log($"[PressAnyKeyToStart] 目标状态播放完成");
                        break;
                    }
                }
            }
        }

        Debug.Log("[PressAnyKeyToStart] 动画序列播放完成");

        if (disableAnimatorAfterPlay)
        {
            cameraAnimator.enabled = false;
            Debug.Log("[PressAnyKeyToStart] Animator 已禁用，相机位置已保持");
        }

        EnableMenuNavigation();
    }

    void EnableMenuNavigation()
    {
        if (menuManager != null)
        {
            menuManager.SetNavigationEnabled(true);
            Debug.Log("[PressAnyKeyToStart] ✅ MenuManager 导航已启用");
        }
    }
}
