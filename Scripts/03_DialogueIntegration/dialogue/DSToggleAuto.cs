using UnityEngine;
using UnityEngine.UI;
using PixelCrushers.DialogueSystem;
using TMPro;
using System.Collections;
using static PixelCrushers.DialogueSystem.DisplaySettings.SubtitleSettings;

public class DSToggleAuto : MonoBehaviour
{
    [Header("Button / Label")]
    public Button button;
    public Text uiText;
    public TMP_Text tmpText;

    [Header("Continue（處理當前這行）")]
    public Button continueButton;
    public StandardUIContinueButtonFastForward fastForward;
    public Text subtitleText;
    public TMP_Text subtitleTMP;

    [Header("當前行停留時間")]
    public float minHoldSeconds = 0.8f;
    public float secondsPerChar = 0.04f;

    [Header("Label 文案 / 快捷鍵")]
    public string manualText = "手動";
    public string autoText = "自動";

    [Header("調試與預設設定")]
    public bool enableDebugLogs = true;
    public bool startInManualMode = true;

    Coroutine _autoCo;
    private bool isInitialized = false;
    private PlayerInputActions inputActions;

    private static DSToggleAuto instance;

    void Reset()
    {
        button = GetComponent<Button>();
        if (!uiText) uiText = GetComponentInChildren<Text>(true);
        if (!tmpText) tmpText = GetComponentInChildren<TMP_Text>(true);
    }

    void Awake()
    {
        instance = this;

        if (!button) button = GetComponent<Button>();
        if (button) button.onClick.AddListener(ToggleAuto);

        InitializeToManualMode();
        RefreshLabel();
        isInitialized = true;
        LogDebug("DSToggleAuto 已初始化為手動模式");

        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;

        if (inputActions != null)
        {
            inputActions.Player.Disable();
            inputActions.Dispose();
        }
    }

    void InitializeToManualMode()
    {
        if (startInManualMode)
        {
            DialogueManager.displaySettings.subtitleSettings.continueButton = ContinueButtonMode.Always;
            LogDebug("強制設定為手動模式");
        }
    }

    void Update()
    {
        // UI 隱藏中時封鎖自動對話切換
        if (inputActions.Player.ToggleAuto.WasPressedThisFrame() &&
            !DialogueSkipControl.IsDialogueUIHidden)
        {
            ToggleAuto();
        }
    }

    void OnEnable()
    {
        var dsEvents = GetDialogueSystemEvents();
        if (dsEvents != null)
        {
            dsEvents.conversationEvents.onConversationEnd.AddListener(OnConversationEnd);
            dsEvents.conversationEvents.onConversationStart.AddListener(OnConversationStart);
        }
    }

    void OnDisable()
    {
        var dsEvents = GetDialogueSystemEvents();
        if (dsEvents != null)
        {
            dsEvents.conversationEvents.onConversationEnd.RemoveListener(OnConversationEnd);
            dsEvents.conversationEvents.onConversationStart.RemoveListener(OnConversationStart);
        }
    }

    void OnConversationEnd(Transform actor)
    {
        // 對話結束時停止當前 auto 協程，並暫時還原為 Always
        // 讓 QuestStateListener 的 Active 狀態有時間生效
        if (_autoCo != null)
        {
            StopCoroutine(_autoCo);
            _autoCo = null;
        }
        DialogueManager.displaySettings.subtitleSettings.continueButton = ContinueButtonMode.Always;
        LogDebug("對話結束，暫時還原為手動模式");
    }

    void OnConversationStart(Transform actor)
    {
        // 下一段對話開始時，若玩家仍在 auto 模式，重新設為 Optional 並啟動協程
        if (CurrentIsAuto())
        {
            DialogueManager.displaySettings.subtitleSettings.continueButton = ContinueButtonMode.Optional;
            if (_autoCo != null) StopCoroutine(_autoCo);
            _autoCo = StartCoroutine(AutoAdvanceCurrentLine());
            LogDebug("新對話開始，恢復自動模式");
        }
    }

    private DialogueSystemEvents GetDialogueSystemEvents()
    {
        return DialogueManager.instance != null
            ? DialogueManager.instance.GetComponent<DialogueSystemEvents>()
            : null;
    }

    public void ToggleAuto()
    {
        if (!isInitialized) return;

        bool toAuto = !CurrentIsAuto();
        LogDebug($"切換模式：{(toAuto ? "自動" : "手動")}");

        DialogueManager.displaySettings.subtitleSettings.continueButton =
            toAuto ? ContinueButtonMode.Optional : ContinueButtonMode.Always;

        RefreshLabel();

        if (_autoCo != null)
        {
            StopCoroutine(_autoCo);
            _autoCo = null;
            LogDebug("停止現有自動播放");
        }

        if (toAuto)
        {
            _autoCo = StartCoroutine(AutoAdvanceCurrentLine());
            LogDebug("開始自動播放當前行");
        }
    }

    public void ForceManualMode()
    {
        if (!isInitialized) return;

        if (CurrentIsAuto())
        {
            LogDebug("被強制切換至手動模式（由 Backlog 觸發）");

            DialogueManager.displaySettings.subtitleSettings.continueButton = ContinueButtonMode.Always;
            RefreshLabel();

            if (_autoCo != null)
            {
                StopCoroutine(_autoCo);
                _autoCo = null;
                LogDebug("停止現有自動播放");
            }
        }
    }

    public static void ForceManualModeStatic()
    {
        if (instance != null)
        {
            instance.ForceManualMode();
        }
    }

    IEnumerator AutoAdvanceCurrentLine()
    {
        LogDebug("自動播放當前行開始");

        if (BacklogUI.IsBacklogOpen)
        {
            LogDebug("Backlog 開啟中，停止自動播放");
            _autoCo = null;
            yield break;
        }

        if (fastForward)
        {
            fastForward.OnFastForward();
            LogDebug("觸發快轉打字機");
        }
        yield return null;

        string t = subtitleTMP ? subtitleTMP.text : (subtitleText ? subtitleText.text : "");
        float wait = Mathf.Max(minHoldSeconds, (t != null ? t.Length : 0) * secondsPerChar);
        LogDebug($"等待 {wait} 秒（文字長度：{(t?.Length ?? 0)}）");

        float elapsed = 0f;
        while (elapsed < wait)
        {
            if (BacklogUI.IsBacklogOpen)
            {
                LogDebug("等待期間 Backlog 開啟，停止自動播放");
                _autoCo = null;
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (BacklogUI.IsBacklogOpen)
        {
            LogDebug("準備Continue時 Backlog 開啟，停止自動播放");
            _autoCo = null;
            yield break;
        }

        if (continueButton)
        {
            continueButton.onClick.Invoke();
            LogDebug("觸發 Continue 按鈕");
        }
        else
        {
            var ui = DialogueManager.instance ? DialogueManager.instance.dialogueUI as AbstractDialogueUI : null;
            if (ui != null) ui.OnContinue();
            else DialogueManager.instance?.SendMessage("OnContinue", SendMessageOptions.DontRequireReceiver);
            LogDebug("觸發 UI Continue");
        }

        _autoCo = null;
        LogDebug("自動播放當前行結束");
    }

    bool CurrentIsAuto() =>
        DialogueManager.displaySettings.subtitleSettings.continueButton == ContinueButtonMode.Optional;

    void RefreshLabel()
    {
        string s = CurrentIsAuto() ? autoText : manualText;
        if (tmpText) tmpText.text = s;
        else if (uiText) uiText.text = s;
        LogDebug($"標籤更新為：{s}");
    }

    void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[DSToggleAuto] {message}");
    }
}
