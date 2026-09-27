using UnityEngine;
using UnityEngine.UI;
using PixelCrushers.DialogueSystem;

public class BacklogUI : MonoBehaviour
{
    public GameObject panel;
    public Scrollbar scrollbar;
    public ScrollRect scrollRect;

    [Header("Scrollbar Settings")]
    public float scrollSpeed = 0.5f;
    [Range(0f, 0.5f)]
    public float deadZone = 0.1f;

    private PlayerInputActions inputActions;
    private bool wasDialogueSystemInputEnabled = true;
    private StandardUISubtitlePanel[] subtitlePanels;

    public static bool IsBacklogOpen { get; private set; }

    void Awake()
    {
        if (panel) panel.SetActive(false);
        IsBacklogOpen = false;

        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();
    }

    void OnEnable()
    {
        var dsEvents = GetDialogueSystemEvents();
        if (dsEvents != null)
            dsEvents.conversationEvents.onConversationEnd.AddListener(OnConversationEnded);
    }

    void OnDisable()
    {
        var dsEvents = GetDialogueSystemEvents();
        if (dsEvents != null)
            dsEvents.conversationEvents.onConversationEnd.RemoveListener(OnConversationEnded);
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
            inputActions.Dispose();
        }
        IsBacklogOpen = false;
    }

    /// <summary>取得掛在 DialogueManager 上的 DialogueSystemEvents 元件。</summary>
    private DialogueSystemEvents GetDialogueSystemEvents()
    {
        if (DialogueManager.instance == null) return null;
        return DialogueManager.instance.GetComponent<DialogueSystemEvents>();
    }

    /// <summary>對話結束時若 backlog 仍開著，強制關閉並恢復輸入。</summary>
    private void OnConversationEnded(Transform actor)
    {
        if (IsBacklogOpen)
            Hide();
    }

    /// <summary>顯示 backlog 面板並停用對話輸入。</summary>
    public void Show()
    {
        if (panel == null) return;
        panel.SetActive(true);
        IsBacklogOpen = true;

        DSToggleAuto.ForceManualModeStatic();
        DisableDialogueInput();
    }

    /// <summary>隱藏 backlog 面板並恢復對話輸入。</summary>
    public void Hide()
    {
        if (panel == null) return;
        panel.SetActive(false);
        IsBacklogOpen = false;
        RestoreDialogueInput();
    }

    /// <summary>切換 backlog 顯示狀態。</summary>
    public void Toggle()
    {
        if (panel == null) return;
        if (panel.activeSelf) Hide();
        else Show();
    }

    void DisableDialogueInput()
    {
        if (DialogueManager.instance == null) return;

        var inputDeviceManager = DialogueManager.instance.GetComponent<PixelCrushers.InputDeviceManager>();
        if (inputDeviceManager != null)
        {
            wasDialogueSystemInputEnabled = inputDeviceManager.enabled;
            inputDeviceManager.enabled = false;
        }

        if (subtitlePanels == null || subtitlePanels.Length == 0)
            subtitlePanels = FindObjectsOfType<StandardUISubtitlePanel>();

        foreach (var p in subtitlePanels)
        {
            if (p.continueButton != null)
                p.continueButton.interactable = false;
        }
    }

    void RestoreDialogueInput()
    {
        if (DialogueManager.instance == null) return;

        var inputDeviceManager = DialogueManager.instance.GetComponent<PixelCrushers.InputDeviceManager>();
        if (inputDeviceManager != null)
            inputDeviceManager.enabled = wasDialogueSystemInputEnabled;

        if (subtitlePanels != null)
        {
            foreach (var p in subtitlePanels)
            {
                if (p.continueButton != null)
                    p.continueButton.interactable = true;
            }
        }
    }

    void Update()
    {
        if (panel == null) return;

        // UI 隱藏中時封鎖 History 開啟
        if (inputActions.Player.ToggleHistory.WasPressedThisFrame() &&
            !DialogueSkipControl.IsDialogueUIHidden)
        {
            Toggle();
        }

        if (panel.activeSelf)
        {
            if (inputActions.Player.Cancel.WasPressedThisFrame())
                Hide();

            HandleScrolling();
        }
    }


    void HandleScrolling()
    {
        Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        if (Mathf.Abs(moveInput.y) > deadZone)
        {
            if (scrollRect != null)
            {
                float newPos = scrollRect.verticalNormalizedPosition + moveInput.y * scrollSpeed * Time.deltaTime;
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(newPos);
            }
            else if (scrollbar != null)
            {
                float delta = moveInput.y * scrollSpeed * Time.deltaTime;
                if (scrollbar.direction == Scrollbar.Direction.BottomToTop ||
                    scrollbar.direction == Scrollbar.Direction.TopToBottom)
                    scrollbar.value = Mathf.Clamp01(scrollbar.value + delta);
                else
                    scrollbar.value = Mathf.Clamp01(scrollbar.value - delta);
            }
        }
    }
}
