using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using PixelCrushers.DialogueSystem;
using TMPro;

public class DialogueSkipControl : MonoBehaviour
{
    [Header("UI组件")]
    [Tooltip("Skip按钮")]
    public Button skipButton;

    [Tooltip("确认窗口Panel")]
    public GameObject confirmationPanel;

    [Tooltip("剧情摘要文字")]
    public TMP_Text plotSummaryText;

    [Tooltip("确认按钮(A键)")]
    public Button confirmButton;

    [Tooltip("取消按钮(B键)")]
    public Button cancelButton;

    [Header("對話框顯示/隱藏")]
    [Tooltip("對話框根節點的 CanvasGroup（短按 B 切換可見度）")]
    public CanvasGroup dialogueUICanvasGroup;

    [Header("输入设定")]
    [Tooltip("长按时长(秒)")]
    public float holdTime = 1.0f;

    private const string GAMEPAD_B_BUTTON_PATH = "<Gamepad>/buttonEast";
    private const string GAMEPAD_A_BUTTON_PATH = "<Gamepad>/buttonSouth";

    [Header("剧情摘要设定")]
    public string currentPlotSummary = "";

    [Header("跳过控制设定")]
    [Tooltip("遇到选项时自动停止跳过")]
    public bool stopSkipOnResponseMenu = true;

    [Tooltip("选项菜单显示时禁用跳过按钮")]
    public bool disableSkipDuringResponseMenu = true;

    [Tooltip("跳过时的对话延迟（秒）")]
    public float skipDelay = 0.1f;

    [Tooltip("显示调试信息")]
    public bool showDebugLog = true;

    [Header("自动跳过控制")]
    [Tooltip("自动继续的冷却时间（秒）")]
    [SerializeField] private float autoContinueCooldown = 0.05f;

    [Header("Fade 保護")]
    [Tooltip("跳過結束後等待此時間再重設黑屏（秒）")]
    public float faderResetDelay = 0.3f;

    private float buttonHoldTime = 0f;
    private bool isHoldingButton = false;
    private bool isConfirmationActive = false;
    private bool isSkipping = false;
    private bool shouldStopSkipping = false;
    private bool isResponseMenuActive = false;
    private bool skipEventsRegistered = false;
    private float lastAutoContinueTime = -999f;
    private bool isDialogueUIHidden = false;

    // 供 DSToggleAuto 和 BacklogUI 查詢
    private static DialogueSkipControl _instance;
    public static bool IsDialogueUIHidden => _instance != null && _instance.isDialogueUIHidden;

    private InputAction bButtonAction;
    private InputAction aButtonAction;
    private DialogueSystemEvents dialogueSystemEvents;
    private float originalSubtitleCharsPerSecond = 30f;
    private float originalMinSubtitleSeconds = 1f;

    private const string FaderCanvasName = "Canvas (Fader)";
    private const string SkipConsequencesVariable = "SkipConsequences";
    private const string PlotSummaryVariable = "PlotSummary";

    void Awake()
    {
        _instance = this;

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipButtonClick);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmSkip);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelSkip);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        bButtonAction = new InputAction(binding: GAMEPAD_B_BUTTON_PATH);
        aButtonAction = new InputAction(binding: GAMEPAD_A_BUTTON_PATH);
    }

    void Start()
    {
        RegisterSkipEvents();
        SaveOriginalSubtitleSettings();
    }

    void OnEnable()
    {
        bButtonAction?.Enable();
        aButtonAction?.Enable();

        if (Application.isPlaying && !skipEventsRegistered)
            RegisterSkipEvents();
    }

    void OnDisable()
    {
        bButtonAction?.Disable();
        aButtonAction?.Disable();
        UnregisterSkipEvents();

        if (isSkipping)
            StopSkipping();

        if (isConfirmationActive)
            HideConfirmationDialog();
    }

    void RegisterSkipEvents()
    {
        if (skipEventsRegistered) return;

        if (DialogueManager.instance != null)
        {
            dialogueSystemEvents = DialogueManager.instance.GetComponent<DialogueSystemEvents>();
            if (dialogueSystemEvents != null)
            {
                dialogueSystemEvents.conversationEvents.onConversationStart.AddListener(OnConversationStart);
                dialogueSystemEvents.conversationEvents.onConversationResponseMenu.AddListener(OnResponseMenuShown);
                dialogueSystemEvents.conversationEvents.onConversationLine.AddListener(OnConversationLine);
                dialogueSystemEvents.conversationEvents.onConversationEnd.AddListener(OnConversationEnd);
                skipEventsRegistered = true;

                if (showDebugLog)
                    Debug.Log("[DialogueSkipControl] 已订阅对话事件");
            }
        }
        else
        {
            if (showDebugLog)
                Debug.LogWarning("[DialogueSkipControl] DialogueManager.instance 为空，无法订阅跳过控制事件");
        }
    }

    void UnregisterSkipEvents()
    {
        if (!skipEventsRegistered) return;

        if (dialogueSystemEvents != null)
        {
            dialogueSystemEvents.conversationEvents.onConversationStart.RemoveListener(OnConversationStart);
            dialogueSystemEvents.conversationEvents.onConversationResponseMenu.RemoveListener(OnResponseMenuShown);
            dialogueSystemEvents.conversationEvents.onConversationLine.RemoveListener(OnConversationLine);
            dialogueSystemEvents.conversationEvents.onConversationEnd.RemoveListener(OnConversationEnd);
        }

        skipEventsRegistered = false;
    }

    /// <summary>每段新對話開始時清空殘留的 SkipConsequences，並確保 UI 恢復顯示。</summary>
    void OnConversationStart(Transform actor)
    {
        DialogueLua.SetVariable(SkipConsequencesVariable, "");

        // 新對話開始時若 UI 仍在隱藏狀態，強制恢復
        if (isDialogueUIHidden)
            ShowDialogueUI();

        if (showDebugLog)
            Debug.Log("[DialogueSkipControl] 🔄 新對話開始，已清空 SkipConsequences");
    }

    void OnResponseMenuShown(Response[] responses)
    {
        isResponseMenuActive = true;

        if (showDebugLog)
            Debug.Log($"[DialogueSkipControl] ⛔ 显示选项菜单（{responses.Length}个选项）");

        if (isSkipping && stopSkipOnResponseMenu)
            shouldStopSkipping = true;

        UpdateSkipButtonState();
    }

    void OnConversationLine(Subtitle subtitle)
    {
        if (showDebugLog && isSkipping)
        {
            Debug.Log($"[DialogueSkipControl] 📜 跳過中執行 Entry | " +
                      $"ConversationID: {subtitle.dialogueEntry.conversationID} | " +
                      $"EntryID: {subtitle.dialogueEntry.id} | " +
                      $"Speaker: {subtitle.speakerInfo?.nameInDatabase} | " +
                      $"Text: {subtitle.formattedText.text}");
        }

        if (isResponseMenuActive)
        {
            isResponseMenuActive = false;

            if (showDebugLog)
                Debug.Log("[DialogueSkipControl] ✅ 玩家已选择选项，重新启用跳过功能");

            UpdateSkipButtonState();
        }
    }

    void OnConversationEnd(Transform actor)
    {
        isResponseMenuActive = false;

        // 對話結束時若 UI 仍隱藏，強制恢復
        if (isDialogueUIHidden)
            ShowDialogueUI();

        if (Time.timeScale == 0 && !isConfirmationActive)
        {
            if (showDebugLog)
                Debug.LogWarning("[DialogueSkipControl] ⚠️ 对话结束时检测到游戏暂停，强制恢复");
            Time.timeScale = 1f;
        }

        if (isSkipping)
        {
            StopSkipping();

            if (showDebugLog)
                Debug.Log("[DialogueSkipControl] 对话结束，停止跳过");
        }

        if (isConfirmationActive)
        {
            if (showDebugLog)
                Debug.LogWarning("[DialogueSkipControl] ⚠️ 对话结束但确认窗口仍活动，强制关闭");
            HideConfirmationDialog();
        }

        StartCoroutine(ResetFaderAfterDelay(faderResetDelay));
        UpdateSkipButtonState();
    }

    void UpdateSkipButtonState()
    {
        if (skipButton == null) return;

        bool shouldDisable = isResponseMenuActive && disableSkipDuringResponseMenu;
        skipButton.interactable = !shouldDisable;
    }

    void SaveOriginalSubtitleSettings()
    {
        if (DialogueManager.displaySettings?.subtitleSettings != null)
        {
            originalSubtitleCharsPerSecond = DialogueManager.displaySettings.subtitleSettings.subtitleCharsPerSecond;
            originalMinSubtitleSeconds = DialogueManager.displaySettings.subtitleSettings.minSubtitleSeconds;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            Debug.LogWarning("[DialogueSkipControl] 🔧 按下 F9，强制恢复游戏！");
            ForceRecovery();
            return;
        }

        if (!DialogueManager.isConversationActive && isConfirmationActive)
        {
            if (showDebugLog)
                Debug.LogWarning("[DialogueSkipControl] ⚠️ 对话已结束但确认窗口仍活动，强制关闭");
            HideConfirmationDialog();
        }

        if (!isConfirmationActive && Time.timeScale == 0 && !isSkipping)
        {
            if (showDebugLog)
                Debug.LogWarning("[DialogueSkipControl] ⚠️ 检测到异常暂停，强制恢复游戏");
            Time.timeScale = 1f;
        }

        if (!DialogueManager.isConversationActive)
        {
            if (isSkipping)
                StopSkipping();
            return;
        }

        if (!skipEventsRegistered)
            RegisterSkipEvents();

        if (isSkipping && shouldStopSkipping)
        {
            StopSkipping();
            shouldStopSkipping = false;
        }

        if (isSkipping && !isConfirmationActive)
            AutoContinueDialogue();

        HandleGamepadInput();
    }

    void AutoContinueDialogue()
    {
        if (Time.realtimeSinceStartup - lastAutoContinueTime < autoContinueCooldown)
            return;

        if (isResponseMenuActive && stopSkipOnResponseMenu)
        {
            if (showDebugLog)
                Debug.Log("[DialogueSkipControl] 遇到选项菜单，停止自动继续");
            shouldStopSkipping = true;
            return;
        }

        if (DialogueManager.dialogueUI != null)
        {
            var dialogueUI = DialogueManager.dialogueUI as AbstractDialogueUI;
            if (dialogueUI != null)
            {
                lastAutoContinueTime = Time.realtimeSinceStartup;

                try
                {
                    dialogueUI.OnContinue();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DialogueSkipControl] OnContinue 出错: {e.Message}\n{e.StackTrace}");
                    StopSkipping();
                }
            }
        }
    }

    void HandleGamepadInput()
    {
        if (isConfirmationActive)
        {
            if (aButtonAction != null && aButtonAction.WasPressedThisFrame())
                OnConfirmSkip();
            else if (bButtonAction != null && bButtonAction.WasPressedThisFrame())
                OnCancelSkip();
            return;
        }

        if (isResponseMenuActive && disableSkipDuringResponseMenu)
        {
            if (isHoldingButton)
            {
                isHoldingButton = false;
                buttonHoldTime = 0f;
            }
            return;
        }

        if (isSkipping)
        {
            if (bButtonAction != null && bButtonAction.WasPressedThisFrame())
            {
                StopSkipping();
                if (showDebugLog)
                    Debug.Log("[DialogueSkipControl] 玩家手动停止跳过");
            }
            return;
        }

        if (bButtonAction != null && bButtonAction.IsPressed())
        {
            if (!isHoldingButton)
            {
                isHoldingButton = true;
                buttonHoldTime = 0f;
            }

            // UI 隱藏時不累積長按時間（封鎖長按跳過）
            if (!isDialogueUIHidden)
            {
                buttonHoldTime += Time.unscaledDeltaTime;

                if (buttonHoldTime >= holdTime)
                {
                    ShowConfirmationDialog();
                    isHoldingButton = false;
                    buttonHoldTime = 0f;
                }
            }
        }
        else
        {
            if (isHoldingButton)
            {
                // 在 holdTime 前放開 = 短按，切換 UI 顯示狀態
                ToggleDialogueUI();

                isHoldingButton = false;
                buttonHoldTime = 0f;
            }
        }
    }

    // ── 對話框顯示/隱藏 ───────────────────────────────────

    /// <summary>短按 B 時切換對話框可見度。</summary>
    void ToggleDialogueUI()
    {
        if (isDialogueUIHidden)
            ShowDialogueUI();
        else
            HideDialogueUI();
    }

    /// <summary>隱藏對話框，並停止自動對話。</summary>
    void HideDialogueUI()
    {
        isDialogueUIHidden = true;
        ApplyDialogueUIVisibility(false);

        // 停止自動對話
        DSToggleAuto.ForceManualModeStatic();

        if (showDebugLog)
            Debug.Log("[DialogueSkipControl] 🙈 對話框已隱藏");
    }

    /// <summary>恢復對話框顯示。</summary>
    void ShowDialogueUI()
    {
        isDialogueUIHidden = false;
        ApplyDialogueUIVisibility(true);

        if (showDebugLog)
            Debug.Log("[DialogueSkipControl] 👁️ 對話框已恢復顯示");
    }

    void ApplyDialogueUIVisibility(bool visible)
    {
        // 優先使用 Inspector 指定的 CanvasGroup
        if (dialogueUICanvasGroup != null)
        {
            dialogueUICanvasGroup.alpha = visible ? 1f : 0f;
            dialogueUICanvasGroup.interactable = visible;
            dialogueUICanvasGroup.blocksRaycasts = visible;
            return;
        }

        // 備用：自動從 DialogueManager 取得
        var ui = DialogueManager.dialogueUI as AbstractDialogueUI;
        if (ui == null) return;

        var cg = ui.GetComponent<CanvasGroup>();
        if (cg == null) cg = ui.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;

        // 快取起來避免每次都找
        dialogueUICanvasGroup = cg;
    }

    // ── 以下與原版相同 ────────────────────────────────────

    public void OnSkipButtonClick()
    {
        if (!DialogueManager.isConversationActive) return;

        if (isResponseMenuActive && disableSkipDuringResponseMenu)
        {
            if (showDebugLog)
                Debug.Log("[DialogueSkipControl] ⚠️ 选项菜单活动中，无法触发跳过");
            return;
        }

        if (isSkipping)
        {
            StopSkipping();
            return;
        }

        ShowConfirmationDialog();
    }

    void ShowConfirmationDialog()
    {
        if (confirmationPanel == null)
        {
            Debug.LogWarning("[DialogueSkipControl] 确认窗口 Panel 未设置");
            return;
        }

        UpdatePlotSummary();
        confirmationPanel.SetActive(true);
        isConfirmationActive = true;
        Time.timeScale = 0f;

        if (showDebugLog)
            Debug.Log("[DialogueSkipControl] 显示跳过确认对话框（暂停游戏）");
    }

    void UpdatePlotSummary()
    {
        if (plotSummaryText == null) return;

        string summary = DialogueLua.GetVariable(PlotSummaryVariable).asString;
        if (!string.IsNullOrEmpty(summary))
            currentPlotSummary = summary;

        plotSummaryText.text = $"\n\n{currentPlotSummary}\n\n<b>確定要跳過當前劇情嗎？</b>";
    }

    void OnConfirmSkip()
    {
        HideConfirmationDialog();

        if (DialogueManager.isConversationActive)
        {
            if (showDebugLog)
                Debug.Log("[DialogueSkipControl] ✅ 确认跳过，开始自动跳过对话");

            ExecuteSkipConsequences();
            StartSkipping();
        }
    }

    void OnCancelSkip()
    {
        if (showDebugLog)
            Debug.Log("[DialogueSkipControl] ❌ 取消跳过");

        HideConfirmationDialog();
    }

    void HideConfirmationDialog()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        isConfirmationActive = false;
        Time.timeScale = 1f;

        if (showDebugLog)
            Debug.Log("[DialogueSkipControl] 关闭确认对话框（恢复游戏）");
    }

    void StartSkipping()
    {
        if (isSkipping) return;

        isSkipping = true;
        shouldStopSkipping = false;
        lastAutoContinueTime = -999f;

        DSToggleAuto.ForceManualModeStatic();

        if (DialogueManager.displaySettings?.subtitleSettings != null)
        {
            DialogueManager.displaySettings.subtitleSettings.subtitleCharsPerSecond = 9999f;
            DialogueManager.displaySettings.subtitleSettings.minSubtitleSeconds = skipDelay;
        }

        UpdateSkipButtonText(true);

        if (showDebugLog)
            Debug.Log("[DialogueSkipControl] 🚀 开始跳过对话");
    }

    void StopSkipping()
    {
        if (!isSkipping) return;

        isSkipping = false;
        shouldStopSkipping = false;

        if (DialogueManager.displaySettings?.subtitleSettings != null)
        {
            DialogueManager.displaySettings.subtitleSettings.subtitleCharsPerSecond = originalSubtitleCharsPerSecond;
            DialogueManager.displaySettings.subtitleSettings.minSubtitleSeconds = originalMinSubtitleSeconds;
        }

        UpdateSkipButtonText(false);

        StartCoroutine(ResetFaderAfterDelay(faderResetDelay));

        if (showDebugLog)
            Debug.Log("[DialogueSkipControl] ⏸️ 停止跳过对话");
    }

    private System.Collections.IEnumerator ResetFaderAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ResetFaderIfStuck();
    }

    private void ResetFaderIfStuck()
    {
        if (DialogueManager.instance == null) return;

        var faderTransform = DialogueManager.instance.transform.Find(FaderCanvasName);
        if (faderTransform == null) return;

        var faderImage = faderTransform.GetComponentInChildren<UnityEngine.UI.Image>();
        if (faderImage == null) return;

        if (faderImage.color.a > 0.1f)
        {
            var c = faderImage.color;
            faderImage.color = new Color(c.r, c.g, c.b, 0f);
            faderTransform.gameObject.SetActive(false);

            if (showDebugLog)
                Debug.Log("[DialogueSkipControl] 🔧 強制清除殘留的 Fade 黑屏");
        }
    }

    void UpdateSkipButtonText(bool isSkippingNow)
    {
        if (skipButton == null) return;

        var buttonText = skipButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
            buttonText.text = isSkippingNow ? "停止跳过" : "跳过";
    }

    void ExecuteSkipConsequences()
    {
        string skipConsequences = DialogueLua.GetVariable(SkipConsequencesVariable).asString;

        if (string.IsNullOrEmpty(skipConsequences)) return;

        DialogueLua.SetVariable(SkipConsequencesVariable, "");

        if (showDebugLog)
            Debug.Log($"[DialogueSkipControl] 执行跳过后果: {skipConsequences}");

        var lines = skipConsequences.Split(';');
        string luaCommands = "";
        string sequencerCommands = "";

        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.StartsWith("SetActive(") ||
                trimmed.StartsWith("SetEnabled(") ||
                trimmed.StartsWith("Fade(") ||
                trimmed.StartsWith("Camera(") ||
                trimmed.StartsWith("Audio("))
                sequencerCommands += trimmed + "; ";
            else
                luaCommands += trimmed + "; ";
        }

        if (!string.IsNullOrEmpty(luaCommands))
        {
            if (showDebugLog)
                Debug.Log($"[DialogueSkipControl] → 执行 Lua: {luaCommands}");

            try { Lua.Run(luaCommands); }
            catch (System.Exception e)
            { Debug.LogError($"[DialogueSkipControl] Lua 执行出错: {e.Message}"); }
        }

        if (!string.IsNullOrEmpty(sequencerCommands))
        {
            if (showDebugLog)
                Debug.Log($"[DialogueSkipControl] → 执行 Sequencer: {sequencerCommands}");

            try { DialogueManager.PlaySequence(sequencerCommands); }
            catch (System.Exception e)
            { Debug.LogError($"[DialogueSkipControl] Sequencer 执行出错: {e.Message}"); }
        }
    }

    void ForceRecovery()
    {
        Debug.Log("[DialogueSkipControl] 🔧 执行强制恢复...");

        Time.timeScale = 1f;

        if (isConfirmationActive)
            HideConfirmationDialog();

        if (isSkipping)
            StopSkipping();

        if (isDialogueUIHidden)
            ShowDialogueUI();

        isResponseMenuActive = false;
        shouldStopSkipping = false;
        isHoldingButton = false;
        buttonHoldTime = 0f;

        ResetFaderIfStuck();

        Debug.Log("[DialogueSkipControl] ✅ 强制恢复完成");
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;

        UnregisterSkipEvents();

        if (isSkipping)
            StopSkipping();

        if (isConfirmationActive)
            Time.timeScale = 1f;

        bButtonAction?.Disable();
        bButtonAction?.Dispose();
        aButtonAction?.Disable();
        aButtonAction?.Dispose();
    }

    public void ManualStartSkipping()
    {
        if (DialogueManager.isConversationActive && !isSkipping && !isResponseMenuActive)
            StartSkipping();
    }

    public void ManualStopSkipping()
    {
        if (isSkipping)
            StopSkipping();
    }

    public bool IsSkipping => isSkipping;
    public bool IsResponseMenuActive => isResponseMenuActive;
    public bool IsConfirmationActive => isConfirmationActive;
}
