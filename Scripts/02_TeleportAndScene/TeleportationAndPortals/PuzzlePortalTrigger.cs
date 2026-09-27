using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;
using PixelCrushers.DialogueSystem;

public class PuzzlePortalTrigger : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("关联的传送门脚本")]
    public InteractiveScenePortal portal;

    [Tooltip("解谜系统")]
    public CircleRotationPuzzle puzzleSystem;

    [Header("相机设置")]
    [Tooltip("主相机")]
    public Camera mainCamera;

    [Tooltip("解谜摄像机（Lock Camera）")]
    public Camera lockCamera;

    [Header("淡入淡出设置")]
    [Tooltip("淡入淡出用的 Canvas Group")]
    public CanvasGroup fadeCanvasGroup;

    [Tooltip("转场持续时间")]
    public float transitionDuration = 1f;

    [Header("触发设置")]
    [Tooltip("是否需要解谜才能通过")]
    public bool requirePuzzle = true;

    [Tooltip("使用哪个传送门目的地触发解谜（索引，-1 表示所有）")]
    public int triggerDestinationIndex = -1;

    [Header("对话设置")]
    [Tooltip("A 对话（解谜前）- 留空则由 Dialogue System Trigger 处理")]
    public string dialogueA = "";

    [Tooltip("B 对话 Trigger GameObject（解谜后自动激活）")]
    public DialogueSystemTrigger dialogueBTrigger;

    [Tooltip("对话的 NPC（可选，不填则为 Player 自言自语）")]
    public Transform conversant;

    [Header("场景设置")]
    [Tooltip("解谜完成后要前往的场景名")]
    public string targetSceneName;

    [Tooltip("解谜完成后的生成点")]
    public string targetSpawnPoint = "Default";

    [Header("事件")]
    public UnityEvent onPuzzleStart;
    public UnityEvent onPuzzleCompleted;
    public UnityEvent onPuzzleExit;

    [Header("调试")]
    public bool enableDebugLog = true;

    private bool isPuzzleActive = false;
    private bool puzzleCompleted = false;
    private bool isTransitioning = false;
    private PlayerInputActions inputActions;

    void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    void OnEnable()
    {
        inputActions?.Player.Enable();
    }

    void OnDisable()
    {
        inputActions?.Player.Disable();
    }

    void OnDestroy()
    {
        inputActions?.Dispose();

        if (puzzleSystem != null)
        {
            puzzleSystem.onPuzzleSolved.RemoveListener(OnPuzzleSolved);
        }
        Lua.UnregisterFunction("StartBalconyPuzzle");
    }

    void Start()
    {
        if (!requirePuzzle)
        {
            Log("⭐ 此 Trigger 不需要解谜，PuzzlePortalTrigger 已禁用");
            this.enabled = false;
            return;
        }

        if (puzzleSystem != null && IsAlreadySolved())
        {
            Log("✅ 解谜已完成，PuzzlePortalTrigger 不再拦截");

            if (portal == null)
            {
                portal = GetComponent<InteractiveScenePortal>();
            }

            if (portal != null)
            {
                portal.enabled = true;
                Log("✅ 已重新启用原传送门");
            }

            this.enabled = false;
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Log($"✅ 自动找到主相机: {mainCamera.name}");
            }
        }

        if (portal == null)
        {
            portal = GetComponent<InteractiveScenePortal>();
        }

        if (portal != null)
        {
            portal.enabled = false;
            Log("✅ 已禁用原传送门，由 PuzzlePortalTrigger 接管");
        }

        if (puzzleSystem != null)
        {
            puzzleSystem.DeactivatePuzzle();
            puzzleSystem.onPuzzleSolved.AddListener(OnPuzzleSolved);
            Log($"✅ 已订阅解谜完成事件");
        }
        else
        {
            Log("⚠️ 警告：未设置 Puzzle System 引用！");
        }

        if (lockCamera != null)
        {
            lockCamera.gameObject.SetActive(false);
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }

        Lua.RegisterFunction("StartBalconyPuzzle", this, SymbolExtensions.GetMethodInfo(() => StartPuzzleFromLua()));

        StartCoroutine(MonitorPortalInput());

        Log("✅ 解谜传送门初始化完成");
    }

    void Update()
    {
        if (isPuzzleActive && !isTransitioning && inputActions.Player.OpenBook.WasPressedThisFrame())
        {
            Log($"🚪 检测到退出按键 (Q/Y键)");
            ExitPuzzle();
        }
    }

    IEnumerator MonitorPortalInput()
    {
        while (true)
        {
            yield return null;

            if (portal == null || isPuzzleActive || puzzleCompleted || isTransitioning) continue;

            for (int i = 0; i < portal.destinations.Length; i++)
            {
                if (triggerDestinationIndex >= 0 && i != triggerDestinationIndex)
                    continue;

                var dest = portal.destinations[i];

                if (Input.GetKeyDown(dest.activationKey))
                {
                    Log($"🎮 检测到按键 {dest.activationKey}");

                    yield return new WaitForSeconds(0.5f);

                    if (DialogueManager.isConversationActive)
                    {
                        yield return new WaitUntil(() => !DialogueManager.isConversationActive);
                    }

                    StartPuzzle();
                    yield break;
                }
            }
        }
    }

    void StartPuzzle()
    {
        if (isPuzzleActive || puzzleCompleted || isTransitioning) return;
        Log("🎮 启动解谜流程");
        StartCoroutine(EnterPuzzleMode());
    }

    public void StartPuzzleFromLua()
    {
        Log("💬 从 Lua 调用启动解谜");
        StartPuzzle();
    }

    IEnumerator EnterPuzzleMode()
    {
        isTransitioning = true;
        Log("🔄 开始进入解谜模式");

        if (CameraStateManager.Instance != null)
        {
            CameraStateManager.Instance.SetMainCameraActive(false);
            Log("📷 CameraStateManager: 设置为非主相机状态");
        }

        if (PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.isMovementLocked = true;
            Log("🔒 玩家移动已锁定");
        }

        yield return StartCoroutine(FadeOut());

        if (mainCamera != null)
        {
            mainCamera.gameObject.SetActive(false);
            Log("📷 主相机已关闭");
        }

        if (lockCamera != null)
        {
            lockCamera.gameObject.SetActive(true);
            Log("📷 Lock Camera 已开启");
        }

        yield return StartCoroutine(FadeIn());

        if (puzzleSystem != null)
        {
            puzzleSystem.ActivatePuzzle();
            Log("🎮 解谜系统已激活");
        }

        isPuzzleActive = true;
        isTransitioning = false;
        onPuzzleStart?.Invoke();

        Log("✅ 进入解谜模式完成");
    }

    void ExitPuzzle()
    {
        if (!isPuzzleActive || isTransitioning || puzzleCompleted) return;
        Log("🚪 开始退出解谜");
        StartCoroutine(ExitPuzzleMode());
    }

    IEnumerator ExitPuzzleMode()
    {
        isTransitioning = true;
        isPuzzleActive = false;

        if (puzzleSystem != null)
        {
            puzzleSystem.DeactivatePuzzle();
            Log("🎮 解谜系统已停用");
        }

        yield return StartCoroutine(FadeOut());

        if (lockCamera != null)
        {
            lockCamera.gameObject.SetActive(false);
            Log("📷 Lock Camera 已关闭");
        }

        if (mainCamera != null)
        {
            mainCamera.gameObject.SetActive(true);
            Log("📷 主相机已开启");
        }

        if (CameraStateManager.Instance != null)
        {
            CameraStateManager.Instance.SetMainCameraActive(true);
            Log("📷 CameraStateManager: 设置为主相机状态");
        }

        if (PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.isMovementLocked = false;
            Log("🔓 玩家移动已解锁");
        }

        yield return StartCoroutine(FadeIn());

        isTransitioning = false;
        onPuzzleExit?.Invoke();

        Log("✅ 退出解谜模式完成");

        StartCoroutine(MonitorPortalInput());
    }

    void OnPuzzleSolved()
    {
        if (!isPuzzleActive || puzzleCompleted)
        {
            Log("⚠️ 解谜状态异常，忽略完成事件");
            return;
        }

        puzzleCompleted = true;
        Log("🎉 解谜完成，准备播放对话B并切换场景");

        StartCoroutine(CompletePuzzle());
    }

    IEnumerator CompletePuzzle()
    {
        isTransitioning = true;

        if (puzzleSystem != null)
            puzzleSystem.DeactivatePuzzle();

        onPuzzleCompleted?.Invoke();

        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(FadeOut());

        if (lockCamera != null) lockCamera.gameObject.SetActive(false);
        if (mainCamera != null) mainCamera.gameObject.SetActive(true);
        if (CameraStateManager.Instance != null) CameraStateManager.Instance.SetMainCameraActive(true);
        if (PlayerMovement.Instance != null) PlayerMovement.Instance.isMovementLocked = false;

        yield return StartCoroutine(FadeIn());

        DialogueLua.SetVariable("Lock", true);
        Log("✅ 已设置 Variable['Lock'] = true");

        if (dialogueBTrigger != null)
        {
            Log("💬 启动 B 对话 Trigger");
            dialogueBTrigger.enabled = true;
            dialogueBTrigger.OnUse(); // 明確呼叫，不依賴 OnEnable 時機

            // 等對話真正「開始」，避免直接通過
            const float startTimeout = 3f;
            float waited = 0f;
            while (!DialogueManager.isConversationActive && waited < startTimeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (DialogueManager.isConversationActive)
            {
                yield return new WaitUntil(() => !DialogueManager.isConversationActive);
                Log("💬 对话B已结束");
            }
            else
            {
                Log("⚠️ 对话B启动超时，跳过等待");
            }
        }

        yield return StartCoroutine(FadeOut());
        Log($"🚀 切换到场景: {targetSceneName}, 生成点: {targetSpawnPoint}");
        TravelData.SetNextSpawn(targetSpawnPoint);
        GlobalSceneTransition.Go(targetSceneName, targetSpawnPoint);
    }


    IEnumerator FadeOut()
    {
        if (fadeCanvasGroup == null)
        {
            Log("⚠️ 未设置 Fade Canvas Group，跳过淡出");
            yield break;
        }

        Log($"🌑 开始淡出（{transitionDuration * 0.4f}秒）");

        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        float duration = transitionDuration * 0.4f;
        float startAlpha = fadeCanvasGroup.alpha;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
    }

    IEnumerator FadeIn()
    {
        if (fadeCanvasGroup == null)
        {
            Log("⚠️ 未设置 Fade Canvas Group，跳过淡入");
            yield break;
        }

        Log($"🌕 开始淡入（{transitionDuration * 0.6f}秒）");

        float elapsed = 0f;
        float duration = transitionDuration * 0.6f;
        float startAlpha = fadeCanvasGroup.alpha;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.gameObject.SetActive(false);
    }

    bool IsAlreadySolved()
    {
        if (puzzleSystem == null) return false;
        string puzzleID = puzzleSystem.puzzleID;
        return PlayerPrefs.GetInt(puzzleID + "_Solved", 0) == 1;
    }

    void Log(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"<color=purple>[PuzzlePortalTrigger]</color> {message}");
        }
    }
}
