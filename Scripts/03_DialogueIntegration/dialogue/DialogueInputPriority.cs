using UnityEngine;
using PixelCrushers.DialogueSystem;

public class DialogueInputPriority : MonoBehaviour
{
    private static DialogueInputPriority _instance;
    private static bool _isQuitting = false;

    /// <summary>安全檢查：Singleton 存在且非退出中，不會觸發重建。</summary>
    public static bool HasInstance => _instance != null && !_isQuitting;

    public static DialogueInputPriority Instance
    {
        get
        {
            if (_instance == null)
            {
                if (_isQuitting) return null;
                var go = new GameObject("DialogueInputPriority");
                _instance = go.AddComponent<DialogueInputPriority>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private bool isInConversation = false;
    private bool isInBark = false;
    private bool isInRubbing = false;
    private bool isBookOpen = false;

    private ProximitySelector proximitySelector;

    public static bool IsConversationActive => HasInstance && Instance.isInConversation;
    public static bool IsBarkActive => HasInstance && Instance.isInBark;
    public static bool IsRubbingActive => HasInstance && Instance.isInRubbing;
    public static bool IsBookOpen => HasInstance && Instance.isBookOpen;
    public static bool ShouldBlockWorldItemInput => IsConversationActive || IsBarkActive || IsRubbingActive || IsBookOpen;
    public static bool ShouldBlockBarkInput => IsConversationActive;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        Lua.RegisterFunction(nameof(IsConversationActiveLua), this,
            SymbolExtensions.GetMethodInfo(() => IsConversationActiveLua()));

        SubscribeToEvents();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        Lua.UnregisterFunction(nameof(IsConversationActiveLua));
        UnsubscribeFromEvents();
    }

    void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    /// <summary>供 Lua 腳本查詢目前是否在對話中。</summary>
    public bool IsConversationActiveLua()
    {
        return DialogueManager.isConversationActive;
    }

    void SubscribeToEvents()
    {
        if (DialogueManager.instance == null) return;

        var events = DialogueManager.instance.GetComponent<DialogueSystemEvents>();
        if (events == null) return;

        events.conversationEvents.onConversationStart.AddListener(OnConversationStart);
        events.conversationEvents.onConversationEnd.AddListener(OnConversationEnd);
        events.barkEvents.onBarkStart.AddListener(OnBarkStart);
        events.barkEvents.onBarkEnd.AddListener(OnBarkEnd);
    }

    void UnsubscribeFromEvents()
    {
        if (DialogueManager.instance == null) return;

        var events = DialogueManager.instance.GetComponent<DialogueSystemEvents>();
        if (events == null) return;

        events.conversationEvents.onConversationStart.RemoveListener(OnConversationStart);
        events.conversationEvents.onConversationEnd.RemoveListener(OnConversationEnd);
        events.barkEvents.onBarkStart.RemoveListener(OnBarkStart);
        events.barkEvents.onBarkEnd.RemoveListener(OnBarkEnd);
    }

    void OnConversationStart(Transform actor)
    {
        isInConversation = true;
        SetProximitySelector(false);
    }

    void OnConversationEnd(Transform actor)
    {
        isInConversation = false;
        if (!isInBark && !isInRubbing && !isBookOpen) SetProximitySelector(true);
    }

    void OnBarkStart(Transform actor)
    {
        isInBark = true;
        SetProximitySelector(false);
    }

    void OnBarkEnd(Transform actor)
    {
        isInBark = false;
        if (!isInConversation && !isInRubbing && !isBookOpen) SetProximitySelector(true);
    }

    /// <summary>設定 Bark 狀態，並同步 ProximitySelector 開關。</summary>
    public static void SetBarkActive(bool active)
    {
        if (!HasInstance) return;
        Instance.isInBark = active;
        if (active)
            Instance.SetProximitySelector(false);
        else if (!Instance.isInConversation && !Instance.isInRubbing && !Instance.isBookOpen)
            Instance.SetProximitySelector(true);
    }

    /// <summary>設定摩擦互動狀態，並同步 ProximitySelector 開關。</summary>
    public static void SetRubbingActive(bool active)
    {
        if (!HasInstance) return;
        Instance.isInRubbing = active;
        if (active)
        {
            Instance.SetProximitySelector(false);
            PlayerMovement.Instance?.SetMovementLocked(true, "摩擦互動");
        }
        else
        {
            if (!Instance.isInConversation && !Instance.isInBark && !Instance.isBookOpen)
                Instance.SetProximitySelector(true);
            PlayerMovement.Instance?.SetMovementLocked(false, "摩擦互動");
        }
    }

    /// <summary>設定圖鑑開啟狀態，並同步 ProximitySelector 開關。</summary>
    public static void SetBookOpen(bool active)
    {
        if (!HasInstance) return;
        Instance.isBookOpen = active;
        if (active)
            Instance.SetProximitySelector(false);
        else if (!Instance.isInConversation && !Instance.isInBark && !Instance.isInRubbing)
            Instance.SetProximitySelector(true);
    }

    void SetProximitySelector(bool enabled)
    {
        if (proximitySelector == null && PlayerMovement.Instance != null)
            proximitySelector = PlayerMovement.Instance.GetComponent<ProximitySelector>();

        if (proximitySelector != null)
            proximitySelector.enabled = enabled;
    }
}
