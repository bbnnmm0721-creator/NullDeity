using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PixelCrushers.DialogueSystem;

public class PanelAnim : MonoBehaviour
{
    [Header("UI 物件")]
    public GameObject panel;
    public Button kiwi;
    public Button kiwi2;
    public GameObject toolkitRoot;

    [Header("Intro 動畫（可選）")]
    public bool playIntro = true;
    public GameObject introImage;
    public Animator introAnimator;
    public string introState = "New Animation";

    [Header("開關音效")]
    public AudioClip openSound;
    public AudioClip closeSound;

    [Header("開書時隱藏的物件")]
    public GameObject[] hideWhenOpen;

    [Header("Intro 動畫最大等待秒數（防止卡死）")]
    public float introAnimTimeout = 5f;

    public bool IsOpen => _isOpen;
    public event Action Opened;
    public event Action Closed;

    bool _isOpen;
    bool _isAnimating;

    // ★ 用 event 自行追蹤對話狀態，不依賴 DialogueManager.isConversationActive
    //   （後者在某些情況下 Sequencer 指令中斷時會卡住不歸零）
    private bool _isConversationActive;

    AudioSource _audioSource;
    private PlayerInputActions inputActions;
    private PlayerMovement _playerMovement;
    private SoftwareCursorConfined _softwareCursor;

    void Awake()
    {
        inputActions = new PlayerInputActions();
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.loop = false;

        if (panel) panel.SetActive(false);
        if (toolkitRoot) toolkitRoot.SetActive(false);
        if (introImage) introImage.SetActive(false);
        if (kiwi2) kiwi2.gameObject.SetActive(false);
        if (kiwi) kiwi.onClick.AddListener(Open);
        if (kiwi2) kiwi2.onClick.AddListener(Close);
    }

    void OnEnable()
    {
        inputActions?.Player.Enable();

        var dsEvents = GetDialogueSystemEvents();
        if (dsEvents != null)
        {
            dsEvents.conversationEvents.onConversationStart.AddListener(OnConversationStart);
            dsEvents.conversationEvents.onConversationEnd.AddListener(OnConversationEnd);
        }
    }

    void OnDisable()
    {
        inputActions?.Player.Disable();

        var dsEvents = GetDialogueSystemEvents();
        if (dsEvents != null)
        {
            dsEvents.conversationEvents.onConversationStart.RemoveListener(OnConversationStart);
            dsEvents.conversationEvents.onConversationEnd.RemoveListener(OnConversationEnd);
        }
    }

    void OnDestroy() => inputActions?.Dispose();

    private DialogueSystemEvents GetDialogueSystemEvents()
    {
        if (DialogueManager.instance == null) return null;
        return DialogueManager.instance.GetComponent<DialogueSystemEvents>();
    }

    private void OnConversationStart(Transform actor)
    {
        _isConversationActive = true;
        if (kiwi) kiwi.interactable = false;
    }

    private void OnConversationEnd(Transform actor)
    {
        _isConversationActive = false;
        if (kiwi) kiwi.interactable = true;
    }

    void Update()
    {
        if (_isAnimating) return;
        if (_isConversationActive) return;

        if (inputActions.Player.OpenBook.WasPressedThisFrame())
        {
            Debug.Log($"[PanelAnim] OpenBook 按下 | anim={_isAnimating} | conv={_isConversationActive}(DM={DialogueManager.isConversationActive}) | isOpen={_isOpen} | kiwiInteractable={kiwi?.interactable} | kiwiActive={kiwi?.gameObject.activeInHierarchy}");
            if (!_isOpen) Open();
            else Close();
        }
    }

    /// <summary>開啟書本面板。</summary>
    public void Open()
    {
        Debug.Log("[PanelAnim] Open() 被呼叫到了");

        if (_isOpen)
        {
            Debug.Log("[PanelAnim] 攔截：_isOpen=true（書已開）");
            return;
        }

        if (_isAnimating)
        {
            Debug.Log("[PanelAnim] 攔截：_isAnimating=true（動畫卡住）");
            return;
        }

        if (_isConversationActive)
        {
            Debug.Log($"[PanelAnim] 攔截：對話未結束（內部flag={_isConversationActive}, DM={DialogueManager.isConversationActive}）");
            return;
        }

        Debug.Log("[PanelAnim] Open() 通過所有條件，開始開書");
        StartCoroutine(OpenCo());
    }

    /// <summary>關閉書本面板。</summary>
    public void Close()
    {
        if (!_isOpen || _isAnimating) return;
        StartCoroutine(CloseCo());
    }

    /// <summary>場景切換後強制清除動畫鎖，確保書面板可正常開啟。</summary>
    public void ForceReset()
    {
        StopAllCoroutines();
        _isAnimating = false;

        // 若書是開著的狀態（Coroutine 被中斷），也一起強制關閉
        if (_isOpen)
        {
            if (panel) panel.SetActive(false);
            if (toolkitRoot) toolkitRoot.SetActive(false);
            if (kiwi2) kiwi2.gameObject.SetActive(false);
            if (kiwi) kiwi.gameObject.SetActive(true);
            _isOpen = false;
        }

        // ★ 同步清除對話狀態，避免 ForceReset 後仍被攔截
        _isConversationActive = false;
        if (kiwi) kiwi.interactable = true;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        GetSoftwareCursor()?.SetCursorVisible(false);

        Debug.Log("[PanelAnim] ForceReset 完成");
    }

    // ── 私有輔助 ──────────────────────────────────────────────

    private void SetHudVisible(bool visible)
    {
        if (hideWhenOpen == null) return;
        foreach (var go in hideWhenOpen)
        {
            if (go != null) ApplyVisibility(go.transform, visible);
        }
    }

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
            foreach (Transform child in target)
                ApplyVisibility(child, visible);
        }
    }

    private SoftwareCursorConfined GetSoftwareCursor()
    {
        if (_softwareCursor == null)
            _softwareCursor = FindObjectOfType<SoftwareCursorConfined>();
        return _softwareCursor;
    }

    private PlayerMovement GetPlayerMovement()
    {
        if (_playerMovement == null)
            _playerMovement = FindObjectOfType<PlayerMovement>();
        return _playerMovement;
    }

    // ── Coroutines ────────────────────────────────────────────

    IEnumerator OpenCo()
    {
        _isAnimating = true;

        if (kiwi)
        {
            kiwi.interactable = false;
            kiwi.gameObject.SetActive(false);
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        var pm = GetPlayerMovement();
        if (pm != null) pm.isMovementLocked = true;

        SetHudVisible(false);

        if (openSound != null) _audioSource.PlayOneShot(openSound);

        // Intro 動畫播放，加 timeout 防止卡死
        if (playIntro && introImage && introAnimator)
        {
            introImage.SetActive(true);
            introAnimator.Play(introState, 0, 0);

            // 等一幀讓 Animator 初始化
            yield return null;

            float elapsed = 0f;
            while (introAnimator != null &&
                   introAnimator.enabled &&
                   introAnimator.gameObject.activeInHierarchy &&
                   introAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= introAnimTimeout)
                {
                    Debug.LogWarning("[PanelAnim] introAnimator 等待超時，強制結束。");
                    break;
                }
                yield return null;
            }

            introImage.SetActive(false);
        }

        yield return null;

        // ★ 確保 UIRoot 父物件已啟用（Menu 開場動畫可能把整棵 UIRoot 關掉）
        //   層級：panel(EventBookPanel) → PanelAnimCanvas → UIRoot
        if (panel != null)
        {
            var uiRoot = panel.transform.parent?.parent?.gameObject;
            if (uiRoot != null && !uiRoot.activeSelf)
            {
                uiRoot.SetActive(true);
                Debug.Log("[PanelAnim] UIRoot 已自動啟用");
            }
        }

        panel.SetActive(true);
        if (toolkitRoot) toolkitRoot.SetActive(true);

        var uguiTabs = panel.GetComponentInChildren<BookTabsUGUI>(true);
        if (uguiTabs) uguiTabs.GoPage1();

        var tk = panel.GetComponentInChildren<ToolkitChoiceToUGUI>(true);
        if (!uguiTabs && tk) tk.SwitchToFirstPage();

        if (kiwi2) kiwi2.gameObject.SetActive(true);

        _isOpen = true;
        _isAnimating = false;
        Opened?.Invoke();
    }

    IEnumerator CloseCo()
    {
        _isAnimating = true;

        if (closeSound != null) _audioSource.PlayOneShot(closeSound);
        yield return null;

        if (panel) panel.SetActive(false);
        if (toolkitRoot) toolkitRoot.SetActive(false);
        if (kiwi2) kiwi2.gameObject.SetActive(false);

        if (kiwi)
        {
            kiwi.gameObject.SetActive(true);
            kiwi.interactable = true;
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        SetHudVisible(true);

        // 確保游標隱藏（PencilPickupInteraction.OnDisable 會先執行，這裡作為保險）
        GetSoftwareCursor()?.SetCursorVisible(false);

        var pm = GetPlayerMovement();
        if (pm != null) pm.isMovementLocked = false;

        _isOpen = false;
        _isAnimating = false;
        Closed?.Invoke();
    }
}
