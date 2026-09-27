using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;

public class PaintingManager : MonoBehaviour
{
    [Header("Cameras")]
    public Camera mainCamera;
    public Camera paintingCamera;

    [Header("Transition")]
    public CanvasGroup fadeCanvas;
    public float transitionDuration = 1f;

    [Header("Clues")]
    public List<string> discoveredClues = new List<string>();

    [Header("Components to Disable")]
    [Tooltip("角色移動組件，切換相機時會被禁用")]
    public MonoBehaviour[] playerMovementComponents;

    [Tooltip("書籍面板相關組件，切換相機時會被禁用")]
    public MonoBehaviour[] bookPanelComponents;

    [Tooltip("自動尋找 PlayerMovement 和 BookTutorialCoach_UITK 組件")]
    public bool autoFindComponents = true;

    [Header("進入壁畫模式時隱藏的 HUD")]
    [Tooltip("同場景靜態指定的 GameObject 列表")]
    public GameObject[] hideWhenInPainting;

    [Tooltip("跨場景物件路徑（用 GameObject.Find 查找，適用 DontDestroyOnLoad 物件），例如：BookRoot、Dialogue Manager/Canvas/Slider")]
    public string[] crossSceneHidePaths;

    [Tooltip("Quest Tracker HUD 父物件的完整名稱路徑，例如 \"Dialogue Manager/Canvas\"")]
    public string questTrackerContainerPath = "Dialogue Manager/Canvas";

    [Tooltip("需要隱藏的 HUD 名稱前綴")]
    public string questTrackerPrefix = "Basic Standard Quest Tracker HUD";

    // 跨場景 lazy cache
    private Transform _questTrackerContainer;
    private List<Transform> _crossSceneTargets;

    private PaintingExamineController examineController;
    private bool isInPaintingMode = false;

    private bool[] originalPlayerMovementStates;
    private bool[] originalBookPanelStates;

    public static PaintingManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[PaintingManager] Instance 已設定");
        }
        else
        {
            Debug.LogError("[PaintingManager] 發現多個 PaintingManager！");
            Destroy(gameObject);
            return;
        }

        if (autoFindComponents)
            AutoFindComponents();

        ValidateCameraSetup();

        if (!fadeCanvas)
            Debug.LogWarning("[PaintingManager] 缺少 Fade Canvas，將跳過淡入淡出效果");
    }

    void AutoFindComponents()
    {
        Debug.Log("[PaintingManager] 自動尋找組件中...");

        PlayerMovement[] playerMovements = FindObjectsOfType<PlayerMovement>();
        if (playerMovements.Length > 0)
        {
            playerMovementComponents = new MonoBehaviour[playerMovements.Length];
            for (int i = 0; i < playerMovements.Length; i++)
                playerMovementComponents[i] = playerMovements[i];
            Debug.Log($"[PaintingManager] 找到 {playerMovements.Length} 個 PlayerMovement 組件");
        }

        BookTutorialCoach_UITK[] bookCoaches = FindObjectsOfType<BookTutorialCoach_UITK>();
        if (bookCoaches.Length > 0)
        {
            bookPanelComponents = new MonoBehaviour[bookCoaches.Length];
            for (int i = 0; i < bookCoaches.Length; i++)
                bookPanelComponents[i] = bookCoaches[i];
            Debug.Log($"[PaintingManager] 找到 {bookCoaches.Length} 個 BookTutorialCoach_UITK 組件");
        }

        if (playerMovementComponents != null)
            originalPlayerMovementStates = new bool[playerMovementComponents.Length];
        if (bookPanelComponents != null)
            originalBookPanelStates = new bool[bookPanelComponents.Length];
    }

    void ValidateCameraSetup()
    {
        if (!mainCamera)
            Debug.LogError("[PaintingManager] 缺少 Main Camera 引用！");
        else
            Debug.Log($"[PaintingManager] Main Camera 已設定: {mainCamera.name}");

        if (!paintingCamera)
            Debug.LogError("[PaintingManager] 缺少 Painting Camera 引用！");
        else
        {
            Debug.Log($"[PaintingManager] Painting Camera 已設定: {paintingCamera.name}");
            examineController = paintingCamera.GetComponent<PaintingExamineController>();
            if (examineController)
                Debug.Log("[PaintingManager] PaintingExamineController 找到");
        }
    }

    void Start()
    {
        Debug.Log("[PaintingManager] Start() 執行");
        if (mainCamera)
        {
            mainCamera.gameObject.SetActive(true);
            Debug.Log("[PaintingManager] 主相機已啟用");
        }
        if (paintingCamera)
        {
            paintingCamera.gameObject.SetActive(false);
            Debug.Log("[PaintingManager] 壁畫相機已關閉");
        }
    }

    public void EnterPaintingMode()
    {
        Debug.Log("[PaintingManager] EnterPaintingMode() 被調用");

        if (isInPaintingMode)
        {
            Debug.LogWarning("[PaintingManager] 已經在壁畫模式中");
            return;
        }

        if (!mainCamera || !paintingCamera)
        {
            Debug.LogError("[PaintingManager] 相機引用遺失，無法切換");
            return;
        }

        StartCoroutine(TransitionToPainting());
    }

    public void DiscoverClue(string clueId)
    {
        if (!discoveredClues.Contains(clueId))
        {
            discoveredClues.Add(clueId);
            Debug.Log($"[PaintingManager] 發現新線索: {clueId} (總計: {discoveredClues.Count})");
        }
    }

    public void ExitPaintingMode()
    {
        Debug.Log("[PaintingManager] ExitPaintingMode() 被調用");

        if (!isInPaintingMode)
        {
            Debug.LogWarning("[PaintingManager] 不在壁畫模式中");
            return;
        }

        StartCoroutine(TransitionToMain());
    }

    public void StartConversationAfterDelay(string conversationName, float delay)
    {
        StartCoroutine(ExitAndStartConversation(conversationName, delay));
    }

    /// <summary>
    /// 解析所有跨場景路徑並快取 Transform 結果。
    /// 僅在第一次呼叫時執行 GameObject.Find（DontDestroyOnLoad 物件可被找到）。
    /// </summary>
    private List<Transform> GetCrossSceneTargets()
    {
        if (_crossSceneTargets != null)
            return _crossSceneTargets;

        _crossSceneTargets = new List<Transform>();

        if (crossSceneHidePaths == null || crossSceneHidePaths.Length == 0)
            return _crossSceneTargets;

        foreach (var path in crossSceneHidePaths)
        {
            if (string.IsNullOrEmpty(path)) continue;

            var go = GameObject.Find(path);
            if (go != null)
            {
                _crossSceneTargets.Add(go.transform);
                Debug.Log($"[PaintingManager] 跨場景目標已快取: {path}");
            }
            else
            {
                Debug.LogWarning($"[PaintingManager] 找不到跨場景目標: {path}，請確認路徑名稱正確");
            }
        }

        return _crossSceneTargets;
    }

    /// <summary>
    /// 跨場景查找 Quest Tracker 父容器，結果 lazy cache 供後續重用。
    /// </summary>
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
            Debug.Log($"[PaintingManager] 找到 Quest Tracker 容器: {questTrackerContainerPath}");
        }
        else
        {
            Debug.LogWarning($"[PaintingManager] 找不到 Quest Tracker 容器: {questTrackerContainerPath}");
        }

        return _questTrackerContainer;
    }

    /// <summary>設定所有 HUD 物件的顯示狀態。</summary>
    private void SetHudVisible(bool visible)
    {
        // 同場景靜態指定
        if (hideWhenInPainting != null)
        {
            foreach (var go in hideWhenInPainting)
            {
                if (go == null) continue;
                ApplyVisibility(go.transform, visible);
            }
        }

        // 跨場景動態查找（BookRoot、Slider 等 DontDestroyOnLoad 物件）
        foreach (var target in GetCrossSceneTargets())
        {
            if (target == null) continue;
            ApplyVisibility(target, visible);
        }

        // 動態查找所有 Quest Tracker HUD
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

    IEnumerator ExitAndStartConversation(string conversationName, float delay)
    {
        Debug.Log($"[PaintingManager] 等待 {delay} 秒后退出壁画模式并启动对话: {conversationName}");

        yield return new WaitForSeconds(delay);

        if (DialogueManager.isConversationActive)
        {
            Debug.Log("[PaintingManager] 关闭当前对话");
            DialogueManager.StopConversation();
            yield return new WaitForSeconds(0.3f);
        }

        if (isInPaintingMode)
        {
            Debug.Log("[PaintingManager] 退出壁画模式，返回主相机");
            yield return StartCoroutine(TransitionToMainForConversation());
        }

        yield return new WaitForSeconds(0.5f);

        if (!string.IsNullOrEmpty(conversationName))
        {
            Debug.Log($"[PaintingManager] 启动对话: {conversationName}");
            DialogueManager.StartConversation(conversationName);
        }
        else
        {
            Debug.LogWarning("[PaintingManager] 对话名称为空！");
        }
    }

    IEnumerator TransitionToPainting()
    {
        Debug.Log("[PaintingManager] 開始轉場到壁畫");
        isInPaintingMode = true;

        CameraStateManager.SetCameraState(false);

        if (fadeCanvas)
        {
            Debug.Log("[PaintingManager] 開始淡出");
            yield return StartCoroutine(Fade(0f, 1f, transitionDuration * 0.4f));
        }

        // 在黑畫面期間隱藏所有 HUD
        SetHudVisible(false);

        Debug.Log("[PaintingManager] 切換相機");
        mainCamera.gameObject.SetActive(false);
        paintingCamera.gameObject.SetActive(true);

        if (examineController)
            examineController.EnterExamineMode();

        if (fadeCanvas)
            yield return StartCoroutine(Fade(1f, 0f, transitionDuration * 0.6f));

        Debug.Log("[PaintingManager] 轉場完成");
    }

    IEnumerator TransitionToMain()
    {
        Debug.Log("[PaintingManager] 開始轉場回主場景");

        if (fadeCanvas)
            yield return StartCoroutine(Fade(0f, 1f, transitionDuration * 0.4f));

        if (examineController)
            examineController.ExitExamineModeWithoutCallback();

        paintingCamera.gameObject.SetActive(false);
        mainCamera.gameObject.SetActive(true);

        // 在黑畫面期間恢復所有 HUD
        SetHudVisible(true);

        CameraStateManager.SetCameraState(true);
        isInPaintingMode = false;

        if (fadeCanvas)
            yield return StartCoroutine(Fade(1f, 0f, transitionDuration * 0.6f));

        Debug.Log("[PaintingManager] 回到主場景完成");
    }

    IEnumerator TransitionToMainForConversation()
    {
        Debug.Log("[PaintingManager] 為對話轉場回主場景");

        if (fadeCanvas)
            yield return StartCoroutine(Fade(0f, 1f, transitionDuration * 0.4f));

        if (examineController)
            examineController.ExitExamineModeWithoutCallback();

        paintingCamera.gameObject.SetActive(false);
        mainCamera.gameObject.SetActive(true);

        // 在黑畫面期間恢復所有 HUD
        SetHudVisible(true);

        CameraStateManager.SetCameraState(true);
        isInPaintingMode = false;

        if (fadeCanvas)
            yield return StartCoroutine(Fade(1f, 0f, transitionDuration * 0.6f));

        Debug.Log("[PaintingManager] 已回到主場景，準備啟動對話");
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (!fadeCanvas) yield break;

        fadeCanvas.gameObject.SetActive(true);
        fadeCanvas.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvas.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        fadeCanvas.alpha = to;

        if (to <= 0f)
        {
            fadeCanvas.blocksRaycasts = false;
            fadeCanvas.gameObject.SetActive(false);
        }
    }

    public bool HasClue(string clueId)
    {
        return discoveredClues.Contains(clueId);
    }

    public int GetDiscoveredClueCount()
    {
        return discoveredClues.Count;
    }
}
