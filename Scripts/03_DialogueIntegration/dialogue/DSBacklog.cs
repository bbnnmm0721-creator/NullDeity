using UnityEngine;
using TMPro;
using PixelCrushers.DialogueSystem;
using UnityEngine.UI;
using System.Collections;

[DefaultExecutionOrder(-100)]
public class DSBacklog : MonoBehaviour
{
    [Header("UI 组件")]
    public TMP_Text logText;
    public ScrollRect scrollRect;
    public Scrollbar verticalScrollbar;

    [Header("滚动设置")]
    [Tooltip("新内容添加时自动滚动到底部")]
    public bool autoScrollToBottom = true;
    [Tooltip("每次添加内容后的滚动延迟")]
    public float scrollDelay = 0.1f;

    [Header("ContentSizeFitter 设置")]
    [Tooltip("自动添加并配置 ContentSizeFitter")]
    public bool autoConfigureContentSizeFitter = true;

    [Header("過濾設定")]
    [Tooltip("過濾空白或純空格的對話")]
    public bool filterEmptyDialogue = true;

    [Header("调试")]
    public bool showDebugLogs = false;

    DialogueSystemEvents dse;
    RectTransform contentRect;
    ContentSizeFitter contentSizeFitter;
    VerticalLayoutGroup verticalLayoutGroup;

    void Awake()
    {
        TryBindEventsHost();
        SetupReferences();
        ConfigureScrollComponents();
    }

    void OnEnable()
    {
        TryBindEventsHost();
        Subscribe(true);
    }

    void OnDisable() => Subscribe(false);

    void SetupReferences()
    {
        if (logText != null)
        {
            contentRect = logText.transform.parent as RectTransform;

            if (showDebugLogs)
                Debug.Log($"[DSBacklog] Content Rect: {contentRect?.name}");
        }

        if (scrollRect == null && logText != null)
        {
            scrollRect = logText.GetComponentInParent<ScrollRect>();

            if (showDebugLogs)
                Debug.Log($"[DSBacklog] ScrollRect 自动查找: {(scrollRect != null ? "成功" : "失败")}");
        }

        if (verticalScrollbar == null && scrollRect != null)
        {
            verticalScrollbar = scrollRect.verticalScrollbar;

            if (showDebugLogs)
                Debug.Log($"[DSBacklog] Scrollbar 自动查找: {(verticalScrollbar != null ? "成功" : "失败")}");
        }
    }

    void ConfigureScrollComponents()
    {
        if (contentRect == null)
        {
            Debug.LogWarning("[DSBacklog] contentRect 為 null，無法配置滾動組件");
            return;
        }

        if (autoConfigureContentSizeFitter)
        {
            contentSizeFitter = contentRect.GetComponent<ContentSizeFitter>();
            if (contentSizeFitter == null)
            {
                contentSizeFitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
                if (showDebugLogs)
                    Debug.Log("[DSBacklog] 已添加 ContentSizeFitter");
            }

            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        verticalLayoutGroup = contentRect.GetComponent<VerticalLayoutGroup>();
        if (verticalLayoutGroup == null)
        {
            verticalLayoutGroup = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.childControlWidth = true;
            verticalLayoutGroup.childControlHeight = true;
            verticalLayoutGroup.childForceExpandWidth = true;
            verticalLayoutGroup.childForceExpandHeight = false;
            verticalLayoutGroup.padding = new RectOffset(10, 10, 10, 10);
            verticalLayoutGroup.spacing = 5f;

            if (showDebugLogs)
                Debug.Log("[DSBacklog] 已添加 VerticalLayoutGroup");
        }

        if (scrollRect != null)
        {
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            if (verticalScrollbar != null)
            {
                scrollRect.verticalScrollbar = verticalScrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

                if (showDebugLogs)
                    Debug.Log("[DSBacklog] Scrollbar 已連接到 ScrollRect");
            }
        }

        if (logText != null)
        {
            logText.overflowMode = TextOverflowModes.Overflow;
            logText.enableWordWrapping = true;

            var logRect = logText.rectTransform;
            logRect.anchorMin = new Vector2(0, 1);
            logRect.anchorMax = new Vector2(1, 1);
            logRect.pivot = new Vector2(0.5f, 1);

            var layoutElement = logText.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = logText.gameObject.AddComponent<LayoutElement>();
            }
            layoutElement.preferredHeight = -1;
            layoutElement.flexibleHeight = 1;

            if (showDebugLogs)
                Debug.Log("[DSBacklog] LogText 配置完成");
        }

        if (showDebugLogs)
        {
            Debug.Log("[DSBacklog] ========== 滾動組件配置完成 ==========");
            Debug.Log($"ContentRect: {contentRect?.name}");
            Debug.Log($"ScrollRect: {scrollRect?.name}");
            Debug.Log($"Scrollbar: {verticalScrollbar?.name}");
            Debug.Log($"ContentSizeFitter: {(contentSizeFitter != null ? "已配置" : "未配置")}");
            Debug.Log($"VerticalLayoutGroup: {(verticalLayoutGroup != null ? "已配置" : "未配置")}");
        }
    }

    void TryBindEventsHost()
    {
        if (dse != null) return;
        if (DialogueManager.instance == null) return;

        dse = DialogueManager.instance.GetComponent<DialogueSystemEvents>();
        if (dse == null) dse = DialogueManager.instance.gameObject.AddComponent<DialogueSystemEvents>();
    }

    void Subscribe(bool add)
    {
        if (dse == null) return;
        var ev = dse.conversationEvents;

        ev.onConversationStart.RemoveListener(OnStart);
        ev.onConversationLine.RemoveListener(OnLine);
        ev.onConversationEnd.RemoveListener(OnEnd);

        if (add)
        {
            ev.onConversationStart.AddListener(OnStart);
            ev.onConversationLine.AddListener(OnLine);
            ev.onConversationEnd.AddListener(OnEnd);
        }
    }

    void OnStart(Transform actor)
    {
        if (logText) logText.text = string.Empty;
        UpdateLayout();

        if (showDebugLogs)
            Debug.Log("[DSBacklog] 對話開始，清空歷史記錄");
    }

    int _lastEntryId = -1, _lastFrame = -1;

    void OnLine(Subtitle s)
    {
        int id = s?.dialogueEntry != null ? s.dialogueEntry.id : 0;
        if (id == _lastEntryId && Time.frameCount == _lastFrame) return;
        _lastEntryId = id; _lastFrame = Time.frameCount;

        if (!logText) return;

        string dialogueText = s.formattedText.text;

        if (filterEmptyDialogue && string.IsNullOrWhiteSpace(dialogueText))
        {
            if (showDebugLogs)
                Debug.Log("[DSBacklog] 跳過空白對話");
            return;
        }

        var who = s.speakerInfo.isPlayer ? "你" : s.speakerInfo.nameInDatabase;
        var newLine = $"\n<color=#BEBEBE>{who}</color>：{dialogueText}";

        logText.text += newLine;

        if (showDebugLogs)
            Debug.Log($"[DSBacklog] 新增對話: {who}");

        StartCoroutine(UpdateLayoutDelayed());
    }

    void OnEnd(Transform actor)
    {
        if (showDebugLogs)
            Debug.Log("[DSBacklog] 對話結束");
    }

    IEnumerator UpdateLayoutDelayed()
    {
        yield return null;
        UpdateLayout();

        yield return new WaitForSeconds(scrollDelay);
        UpdateLayout();

        if (autoScrollToBottom)
        {
            ScrollToBottom();
        }
    }

    void UpdateLayout()
    {
        if (logText == null || contentRect == null) return;

        logText.ForceMeshUpdate();
        LayoutRebuilder.ForceRebuildLayoutImmediate(logText.rectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();

        if (showDebugLogs)
        {
            Debug.Log($"[DSBacklog] 布局更新完成，內容高度: {contentRect.sizeDelta.y}");
        }
    }

    void ScrollToBottom()
    {
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;

            if (showDebugLogs)
                Debug.Log("[DSBacklog] 滾動到底部");
        }
    }

    [ContextMenu("测试滚动到顶部")]
    public void ScrollToTop()
    {
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            Debug.Log("[DSBacklog] 滾動到頂部");
        }
    }

    [ContextMenu("添加测试文本")]
    public void AddTestText()
    {
        if (logText != null)
        {
            for (int i = 0; i < 20; i++)
            {
                logText.text += $"\n<color=#BEBEBE>測試角色{i}</color>：這是一段測試文字，用來檢查滾動功能是否正常。";
            }
            StartCoroutine(UpdateLayoutDelayed());
            Debug.Log("[DSBacklog] 已添加測試文字");
        }
    }

    [ContextMenu("清空文本")]
    public void ClearText()
    {
        if (logText != null)
        {
            logText.text = string.Empty;
            UpdateLayout();
            Debug.Log("[DSBacklog] 已清空文字");
        }
    }
}
