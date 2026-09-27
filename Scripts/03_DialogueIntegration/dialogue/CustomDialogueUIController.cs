using UnityEngine;
using PixelCrushers.DialogueSystem;

/// <summary>
/// 自訂對話 UI 控制器
/// 處理字幕和回答選項的顯示邏輯，並整合 StandardDialogueUI 的基本對話功能
/// </summary>
public class CustomDialogueUIController : MonoBehaviour
{
    [Header("字幕設定")]
    [SerializeField] private bool keepSubtitleDuringResponse = true; // 回答選項時是否保留字幕
    [SerializeField] private float responseMenuOffsetY = 100f;        // 回答選項距離字幕的Y軸間距

    [Header("Letterbox 設定")]  // 新增區塊
    [SerializeField] private Animator letterboxAnimator;  // 拖入你的 Animator
    [SerializeField] private bool enableLetterboxEffect = true;  // 是否啟用電影效果

    [Header("調試設定")]
    [SerializeField] private bool enableDebugLogs = true; // 是否顯示調試訊息

    private StandardDialogueUI dialogueUI;
    private DialogueSystemEvents dialogueSystemEvents;
    private bool isConversationActive = false;

    // 儲存UI元件參考
    private GameObject subtitlePanel;
    private GameObject responseMenuPanel;

    void Awake()
    {
        // 取得 StandardDialogueUI 元件
        dialogueUI = GetComponent<StandardDialogueUI>();
        if (dialogueUI == null)
        {
            LogDebug("警告：找不到 StandardDialogueUI 元件");
        }

        // 如果沒有指定 Animator，嘗試自動找到
        if (letterboxAnimator == null)
        {
            letterboxAnimator = GetComponent<Animator>();
        }
    }

    void OnEnable()
    {
        TryGetDialogueSystemEvents();
        SubscribeToEvents(true);
    }

    void OnDisable()
    {
        SubscribeToEvents(false);
        isConversationActive = false;
    }

    /// <summary>
    /// 嘗試取得 DialogueSystemEvents 元件
    /// </summary>
    void TryGetDialogueSystemEvents()
    {
        if (dialogueSystemEvents != null) return;
        if (DialogueManager.instance == null) return;

        dialogueSystemEvents = DialogueManager.instance.GetComponent<DialogueSystemEvents>();
        if (dialogueSystemEvents == null)
        {
            dialogueSystemEvents = DialogueManager.instance.gameObject.AddComponent<DialogueSystemEvents>();
        }
    }

    /// <summary>
    /// 訂閱或取消訂閱對話事件
    /// </summary>
    /// <param name="subscribe">true 為訂閱，false 為取消訂閱</param>
    void SubscribeToEvents(bool subscribe)
    {
        if (dialogueSystemEvents == null) return;

        var conversationEvents = dialogueSystemEvents.conversationEvents;

        // 先移除所有監聽器（避免重複訂閱）
        conversationEvents.onConversationStart.RemoveListener(OnConversationStart);
        conversationEvents.onConversationResponseMenu.RemoveListener(OnResponseMenu);
        conversationEvents.onConversationEnd.RemoveListener(OnConversationEnd);

        // 如果需要訂閱，則加入監聽器
        if (subscribe)
        {
            conversationEvents.onConversationStart.AddListener(OnConversationStart);
            conversationEvents.onConversationResponseMenu.AddListener(OnResponseMenu);
            conversationEvents.onConversationEnd.AddListener(OnConversationEnd);
        }
    }

    /// <summary>
    /// 對話開始時觸發
    /// </summary>
    /// <param name="actor">對話者</param>
    void OnConversationStart(Transform actor)
    {
        isConversationActive = true;
        LogDebug("對話開始");

        // 觸發 Letterbox 顯示動畫
        if (enableLetterboxEffect && letterboxAnimator != null)
        {
            letterboxAnimator.SetTrigger("ShowLetterbox");
            LogDebug("觸發 Letterbox 顯示動畫");
        }

        // 初始化UI元件參考
        InitializeUIReferences();
    }

    /// <summary>
    /// 顯示對話回答選項時觸發
    /// </summary>
    /// <param name="responses">回答選項陣列</param>
    void OnResponseMenu(Response[] responses)
    {
        if (!isConversationActive) return;

        LogDebug($"顯示對話回答選項，共 {responses.Length} 個選項");

        if (keepSubtitleDuringResponse)
        {
            // 確保字幕在回答選項時保持顯示（這取決於設置，可能不需要自己處理）
            EnsureSubtitleVisibleDuringResponse();

            // 調整回答選項位置
            AdjustResponseMenuPosition();
        }
    }

    /// <summary>
    /// 對話結束時觸發
    /// 注意：如果需要在這裡隱藏UI，由 StandardDialogueUI 自己處理
    /// </summary>
    /// <param name="actor">對話者</param>
    void OnConversationEnd(Transform actor)
    {
        isConversationActive = false;
        LogDebug("對話結束 - 由 StandardDialogueUI 自動處理UI隱藏");

        // 觸發 Letterbox 隱藏動畫
        if (enableLetterboxEffect && letterboxAnimator != null)
        {
            letterboxAnimator.SetTrigger("HideLetterbox");
            LogDebug("觸發 Letterbox 隱藏動畫");
        }

        // 清除儲存的參考
        subtitlePanel = null;
        responseMenuPanel = null;

        // 注意：如果需要在這裡做其他UI隱藏和清理
        // StandardDialogueUI 會自動處理所有UI元素的生命週期
    }

    /// <summary>
    /// 初始化UI元件參考
    /// </summary>
    void InitializeUIReferences()
    {
        if (dialogueUI == null) return;

        try
        {
            // 取得字幕面板參考
            var subtitlePanels = dialogueUI.conversationUIElements.subtitlePanels;
            if (subtitlePanels != null && subtitlePanels.Length > 0)
            {
                subtitlePanel = subtitlePanels[0].panel?.gameObject;
            }

            // 取得回答選項面板參考
            var menuPanels = dialogueUI.conversationUIElements.menuPanels;
            if (menuPanels != null && menuPanels.Length > 0)
            {
                responseMenuPanel = menuPanels[0].panel?.gameObject;
            }

            LogDebug($"UI參考初始化完成 - 字幕面板: {(subtitlePanel != null ? "找到" : "未找到")}, 回答面板: {(responseMenuPanel != null ? "找到" : "未找到")}");
        }
        catch (System.Exception e)
        {
            LogDebug($"初始化UI參考時發生錯誤: {e.Message}");
        }
    }

    /// <summary>
    /// 確保字幕在回答選項時保持顯示
    /// </summary>
    void EnsureSubtitleVisibleDuringResponse()
    {
        if (subtitlePanel == null) return;

        try
        {
            // 只在面板被隱藏時才重新顯示
            // 通常 StandardDialogueUI 會正確處理這個，這是備用措施
            if (!subtitlePanel.activeInHierarchy)
            {
                LogDebug("字幕面板被隱藏，重新顯示以保持回答選項期間顯示");
                // 注意：這裡可能需要根據你的具體情況調整
                // 如果 StandardDialogueUI 有特定顯示方法，請使用那些方法
            }
        }
        catch (System.Exception e)
        {
            LogDebug($"確保字幕顯示時發生錯誤: {e.Message}");
        }
    }

    /// <summary>
    /// 調整回答選項的位置
    /// </summary>
    void AdjustResponseMenuPosition()
    {
        if (responseMenuPanel == null || subtitlePanel == null) return;

        try
        {
            // 根據字幕的位置計算
            RectTransform subtitleRect = subtitlePanel.GetComponent<RectTransform>();
            RectTransform responseRect = responseMenuPanel.GetComponent<RectTransform>();

            if (subtitleRect != null && responseRect != null)
            {
                // 計算回答選項相對字幕的位置（字幕上方）
                Vector3 subtitlePos = subtitleRect.anchoredPosition;
                Vector3 newPos = new Vector3(
                    subtitlePos.x,
                    subtitlePos.y + subtitleRect.sizeDelta.y / 2 + responseMenuOffsetY,
                    subtitlePos.z
                );

                responseRect.anchoredPosition = newPos;
                LogDebug($"調整回答選項位置到: {newPos}");
            }
        }
        catch (System.Exception e)
        {
            LogDebug($"調整回答選項位置時發生錯誤: {e.Message}");
        }
    }

    /// <summary>
    /// 調試訊息輸出
    /// </summary>
    /// <param name="message">訊息</param>
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CustomDialogueUIController] {message}");
        }
    }

    /// <summary>
    /// 手動設定字幕面板參考（供外部或調試使用，可選）
    /// </summary>
    /// <param name="subtitle">字幕面板</param>
    /// <param name="responseMenu">回答選項面板</param>
    public void SetUIReferences(GameObject subtitle, GameObject responseMenu)
    {
        subtitlePanel = subtitle;
        responseMenuPanel = responseMenu;
        LogDebug("手動設定UI參考完成");
    }

    /// <summary>
    /// 重新初始化UI參考（供外部或調試使用，可選）
    /// </summary>
    public void RefreshUIReferences()
    {
        InitializeUIReferences();
    }

    /// <summary>
    /// 手動控制 Letterbox 顯示（額外功能，可選）
    /// </summary>
    public void ShowLetterbox()
    {
        if (letterboxAnimator != null)
        {
            letterboxAnimator.SetTrigger("ShowLetterbox");
            LogDebug("手動觸發 Letterbox 顯示");
        }
    }

    /// <summary>
    /// 手動控制 Letterbox 隱藏（額外功能，可選）
    /// </summary>
    public void HideLetterbox()
    {
        if (letterboxAnimator != null)
        {
            letterboxAnimator.SetTrigger("HideLetterbox");
            LogDebug("手動觸發 Letterbox 隱藏");
        }
    }
}
