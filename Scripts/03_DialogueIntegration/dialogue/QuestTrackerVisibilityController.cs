using UnityEngine;
using PixelCrushers.DialogueSystem;

public class QuestTrackerVisibilityController : MonoBehaviour
{
    [Header("引用设置")]
    [Tooltip("Quest Tracker HUD GameObject（如果为空会自动查找）")]
    public GameObject questTrackerHUD;

    [Header("隐藏选项")]
    [Tooltip("对话时隐藏 Quest Tracker")]
    public bool hideOnConversation = true;

    [Tooltip("设置界面打开时隐藏 Quest Tracker")]
    public bool hideOnSettings = true;

    [Tooltip("使用淡入淡出效果")]
    public bool useFadeEffect = true;

    [Tooltip("淡入淡出时间")]
    public float fadeDuration = 0.3f;

    [Header("调试")]
    [Tooltip("显示调试信息")]
    public bool showDebugLog = true;

    private CanvasGroup canvasGroup;
    private SettingsMenuManager settingsManager;
    private bool isConversationActive = false;
    private bool dialogueEventsRegistered = false;
    private bool shouldBeVisible = true;
    private float currentAlpha = 1f;
    private float targetAlpha = 1f;

    void Awake()
    {
        // 如果没有指定 Quest Tracker，自动查找
        if (questTrackerHUD == null)
        {
            // 尝试在当前 GameObject 查找
            questTrackerHUD = gameObject;

            // 或者通过名称查找
            if (questTrackerHUD.name != "Basic Standard Quest Tracker HUD 1")
            {
                var tracker = GameObject.Find("Basic Standard Quest Tracker HUD 1");
                if (tracker != null)
                {
                    questTrackerHUD = tracker;
                    if (showDebugLog)
                        Debug.Log($"[QuestTrackerVisibility] 自动找到 Quest Tracker: {questTrackerHUD.name}");
                }
            }
        }

        // 添加或获取 CanvasGroup 组件（用于淡入淡出）
        if (useFadeEffect && questTrackerHUD != null)
        {
            canvasGroup = questTrackerHUD.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = questTrackerHUD.AddComponent<CanvasGroup>();
            }
        }

        // 查找 SettingsMenuManager
        settingsManager = FindObjectOfType<SettingsMenuManager>();
        if (settingsManager != null && showDebugLog)
        {
            Debug.Log("[QuestTrackerVisibility] 已找到 SettingsMenuManager");
        }
    }

    void Start()
    {
        // 在 Start 中注册对话事件（确保 DialogueManager 已初始化）
        RegisterDialogueEvents();
    }

    void OnEnable()
    {
        // 如果已经在运行且事件未注册，尝试注册
        if (Application.isPlaying && !dialogueEventsRegistered)
        {
            RegisterDialogueEvents();
        }
    }

    void OnDisable()
    {
        UnregisterDialogueEvents();
    }

    void OnDestroy()
    {
        UnregisterDialogueEvents();
    }

    void RegisterDialogueEvents()
    {
        if (!hideOnConversation) return;
        if (dialogueEventsRegistered) return;

        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted += OnConversationStart;
            DialogueManager.instance.conversationEnded += OnConversationEnd;
            dialogueEventsRegistered = true;

            if (showDebugLog)
                Debug.Log("[QuestTrackerVisibility] 已订阅对话事件");
        }
        else
        {
            if (showDebugLog)
                Debug.LogWarning("[QuestTrackerVisibility] DialogueManager.instance 为空，无法订阅对话事件");
        }
    }

    void UnregisterDialogueEvents()
    {
        if (!hideOnConversation) return;
        if (!dialogueEventsRegistered) return;

        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted -= OnConversationStart;
            DialogueManager.instance.conversationEnded -= OnConversationEnd;
        }

        dialogueEventsRegistered = false;

        if (showDebugLog)
            Debug.Log("[QuestTrackerVisibility] 已取消订阅对话事件");
    }

    void Update()
    {
        // 如果对话事件还未注册，尝试注册
        if (hideOnConversation && !dialogueEventsRegistered)
        {
            RegisterDialogueEvents();
        }

        // 更新可见性
        UpdateVisibility();

        // 处理淡入淡出动画
        if (useFadeEffect && canvasGroup != null)
        {
            if (currentAlpha != targetAlpha)
            {
                currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha,
                    Time.unscaledDeltaTime / fadeDuration);
                canvasGroup.alpha = currentAlpha;
            }
        }
    }

    private void UpdateVisibility()
    {
        if (questTrackerHUD == null) return;

        // 判断是否应该显示
        bool settingsOpen = hideOnSettings && settingsManager != null && settingsManager.IsOpen;
        bool newShouldBeVisible = !isConversationActive && !settingsOpen;

        // 只在状态改变时输出日志
        if (newShouldBeVisible != shouldBeVisible && showDebugLog)
        {
            Debug.Log($"[QuestTrackerVisibility] 可见性变更: {shouldBeVisible} → {newShouldBeVisible} (对话中:{isConversationActive}, 设置中:{settingsOpen})");
        }

        shouldBeVisible = newShouldBeVisible;

        // 根据设置选择显示/隐藏方式
        if (useFadeEffect && canvasGroup != null)
        {
            // 使用淡入淡出
            targetAlpha = shouldBeVisible ? 1f : 0f;
            canvasGroup.interactable = shouldBeVisible;
            canvasGroup.blocksRaycasts = shouldBeVisible;
        }
        else
        {
            // 直接显示/隐藏
            questTrackerHUD.SetActive(shouldBeVisible);
        }
    }

    private void OnConversationStart(Transform actor)
    {
        isConversationActive = true;

        if (showDebugLog)
            Debug.Log("[QuestTrackerVisibility] 对话开始，隐藏 Quest Tracker");
    }

    private void OnConversationEnd(Transform actor)
    {
        isConversationActive = false;

        if (showDebugLog)
            Debug.Log("[QuestTrackerVisibility] 对话结束，显示 Quest Tracker");
    }

    // 手动控制显示/隐藏的公开方法
    public void Show()
    {
        if (useFadeEffect)
        {
            targetAlpha = 1f;
        }
        else
        {
            questTrackerHUD?.SetActive(true);
        }

        if (showDebugLog)
            Debug.Log("[QuestTrackerVisibility] 手动显示 Quest Tracker");
    }

    public void Hide()
    {
        if (useFadeEffect)
        {
            targetAlpha = 0f;
        }
        else
        {
            questTrackerHUD?.SetActive(false);
        }

        if (showDebugLog)
            Debug.Log("[QuestTrackerVisibility] 手动隐藏 Quest Tracker");
    }

    // 公开属性，方便外部查询状态
    public bool IsVisible => shouldBeVisible;
    public bool IsConversationActive => isConversationActive;
}
