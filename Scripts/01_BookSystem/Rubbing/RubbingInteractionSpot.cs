using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;

/// <summary>
/// 放在場景可拓印物件上的觸發器。
/// 玩家進入範圍後發出 Bark 並顯示互動提示，按互動鍵後開啟 RubbingUIController 彈窗。
/// 拓印完成後停用自身 Collider 與提示，其他 RubbingInteractionSpot 不受影響。
/// Bark 沿用 WorldItem 的 BarkStep_Item{id} / BarkPlaying_Item{id} 慣例，
/// 並以 RubbingPhase 區分走近（0）與完成（1）兩個時機。
/// Barker 優先用 barkerActorName 直查，支援跨場景角色，不需手動拖拽。
/// </summary>
[RequireComponent(typeof(Collider))]
public class RubbingInteractionSpot : MonoBehaviour
{
    [Header("資料")]
    [Tooltip("對應 ItemDatabase 中的 ItemData id")]
    public int itemId;

    [Header("任務（選填）")]
    [Tooltip("填入後只有該任務 Active 時才顯示提示與 Bark。留空則永遠觸發。")]
    public string requiredQuestName = "";

    [Header("Bark")]
    [Tooltip("Dialogue System 裡設定的 Bark Conversation 名稱")]
    public string barkConversation = "ItemPickupReactions";

    [Tooltip("強制指定 Barker Transform，留空則改用 barkerActorName 查找")]
    public Transform barker;

    [Tooltip("Bark 發話角色在 Dialogue Database 裡的 Actor 名稱（例如：書）。" +
             "留空則從 Conversation Entry 反查。填入後優先使用此欄位。")]
    public string barkerActorName = "";

    [Tooltip("拓印完成後是否觸發第二次 Bark（RubbingPhase = 1）")]
    public bool barkOnCompleted = true;
    [Tooltip("拓印完成 Bark 顯示幾秒後自動關閉")]
    public float completionBarkDuration = 3f;


    [Header("互動提示 UI（選填）")]
    public GameObject promptUI;

    [Header("觸發標籤")]
    public string[] triggerTags = { "Player" };

    private bool _playerInRange;
    private bool _barkTriggered;
    private bool _isCollected;
    private PlayerInputActions _inputActions;

    // 播放中的 Bark 所用的 Barker，離開時用來立即關閉 BarkUI
    private Transform _currentBarkerT;

    // 跨場景 DialogueActor 快取
    private static readonly List<DialogueActor> _cachedActors = new List<DialogueActor>();

    private const string LogTag = "[RubbingInteractionSpot]";

    // ── 生命週期 ──

    void Awake()
    {
        _inputActions = new PlayerInputActions();

        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"{LogTag} {name} 的 Collider 已自動設為 Trigger");
        }
    }

    void OnEnable()
    {
        _inputActions?.Player.Enable();
        RubbingUIController.OnRubbingCompleted += HandleRubbingCompleted;
        RubbingUIController.OnRubbingCancelled += HandleRubbingCancelled;
        CheckAlreadyCollected();
    }

    void OnDisable()
    {
        _inputActions?.Player.Disable();
        RubbingUIController.OnRubbingCompleted -= HandleRubbingCompleted;
        RubbingUIController.OnRubbingCancelled -= HandleRubbingCancelled;
        HidePrompt();
    }

    void OnDestroy() => _inputActions?.Dispose();

    void Start() => HidePrompt();

    void Update()
    {
        if (!_playerInRange || _isCollected) return;

        // Bark 播放中仍允許互動（Bark 就是走近的反應，A 鍵應該能開啟拓印）
        // 只擋對話進行中 或 拓印 UI 已開啟 的情況
        if (DialogueInputPriority.IsConversationActive || DialogueInputPriority.IsRubbingActive) return;

        if (_inputActions.Player.Interact.WasPressedThisFrame())
            OpenRubbingUI();
    }

    // ── Trigger（支援 3D / 2D）──

    void OnTriggerEnter(Collider other)
    {
        if (!IsValidTag(other.tag)) return;
        OnPlayerEnter();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsValidTag(other.tag)) return;
        OnPlayerExit();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsValidTag(other.tag)) return;
        OnPlayerEnter();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsValidTag(other.tag)) return;
        OnPlayerExit();
    }

    // ── 玩家進出 ──

    void OnPlayerEnter()
    {
        if (_isCollected) return;
        if (!IsQuestActive()) return;

        _playerInRange = true;
        ShowPrompt();

        if (!_barkTriggered)
        {
            _barkTriggered = true;
            TriggerBark(phase: 0);
        }
    }

    void OnPlayerExit()
    {
        _playerInRange = false;
        _barkTriggered = false;
        HidePrompt();
        DismissBark(); // 立即關閉 BarkUI
    }

    // ── 開啟拓印 UI ──

    void OpenRubbingUI()
    {
        if (RubbingUIController.Instance == null)
        {
            Debug.LogWarning($"{LogTag} RubbingUIController.Instance 為 null！請確認 persistent scene 已設置。");
            return;
        }

        DismissBark(); // 開啟拓印時也順手關掉 Bark
        HidePrompt();
        RubbingUIController.Instance.Open(itemId);
        Debug.Log($"{LogTag} 開啟拓印 UI，itemId={itemId}");
    }

    // ── 事件回調 ──

    void HandleRubbingCompleted(int completedItemId)
    {
        if (completedItemId != itemId) return;

        _isCollected = true;

        if (barkOnCompleted)
        {
            TriggerBark(phase: 1);
            Invoke(nameof(DismissBark), completionBarkDuration); // 時間到自動關閉
        }

        DisableSelf();
        Debug.Log($"{LogTag} 拓印完成，停用 spot，itemId={itemId}");
    }


    void HandleRubbingCancelled(int cancelledItemId)
    {
        if (cancelledItemId != itemId) return;
        if (_playerInRange) ShowPrompt();
        Debug.Log($"{LogTag} 拓印取消，spot 保持啟用，itemId={itemId}");
    }

    // ── Bark ──

    /// <summary>
    /// 設定 BarkStep / RubbingPhase 變數後觸發 Bark。
    /// Barker 查找優先順序：barker 欄位 → barkerActorName 直查 → Conversation Entry 反查 → 自身 transform。
    /// </summary>
    void TriggerBark(int phase)
    {
        if (string.IsNullOrEmpty(barkConversation)) return;
        if (DialogueInputPriority.IsConversationActive) return;

        DialogueLua.SetVariable($"BarkStep_Item{itemId}", 0);
        DialogueLua.SetVariable($"BarkPlaying_Item{itemId}", true);
        DialogueLua.SetVariable("RubbingPhase", phase);

        Transform barkerT = barker;
        if (barkerT == null)
        {
            string speakerName = !string.IsNullOrEmpty(barkerActorName)
                ? barkerActorName
                : GetSpeakerFromConversation(phase);

            GameObject speakerObj = GetActorGameObject(speakerName);
            barkerT = speakerObj != null ? speakerObj.transform : transform;
        }

        _currentBarkerT = barkerT; // 記錄以便離開時關閉
        DialogueManager.Bark(barkConversation, barkerT);
        Debug.Log($"{LogTag} Bark: {barkConversation}, phase={phase}, barker={barkerT.name}, itemId={itemId}");
    }

    /// <summary>立即關閉 Barker 上的 BarkUI，讓 Bark 氣泡即時消失。</summary>
    void DismissBark()
    {
        if (_currentBarkerT != null)
        {
            var barkUI = _currentBarkerT.GetComponentInChildren<AbstractBarkUI>();
            if (barkUI != null) barkUI.Hide();
        }

        // 非書角色的 bark 走 ScreenSpaceBarkUI，也要一起關掉
        ScreenSpaceBarkUI.Instance?.Hide();

        _currentBarkerT = null;
    }


    /// <summary>
    /// 從 masterDatabase 中找出符合 BarkStep_Item{itemId} 和 RubbingPhase == phase 條件的 Entry，
    /// 回傳其 Actor 名稱。填寫 barkerActorName 後此方法不會被呼叫。
    /// </summary>
    string GetSpeakerFromConversation(int phase)
    {
        var conversation = DialogueManager.masterDatabase.GetConversation(barkConversation);
        if (conversation == null)
        {
            Debug.LogWarning($"{LogTag} 找不到 Conversation: {barkConversation}");
            return string.Empty;
        }

        string barkStepKey = $"BarkStep_Item{itemId}";
        string rubbingPhaseKey = "RubbingPhase";

        foreach (var entry in conversation.dialogueEntries)
        {
            string cond = entry.conditionsString;
            if (string.IsNullOrEmpty(cond)) continue;

            if (cond.Contains(barkStepKey) &&
                cond.Contains(rubbingPhaseKey) &&
                cond.Contains($"== {phase}"))
            {
                var actor = DialogueManager.masterDatabase.GetActor(entry.ActorID);
                if (actor != null)
                {
                    Debug.Log($"{LogTag} Conversation 反查 phase={phase} → Actor: {actor.Name}");
                    return actor.Name;
                }
            }
        }

        Debug.LogWarning($"{LogTag} 找不到 phase={phase} 對應的 Actor，改用自身 transform");
        return string.Empty;
    }

    /// <summary>
    /// 用 Actor 名稱在所有已載入場景（含 persistent / DontDestroyOnLoad）中找對應的 DialogueActor GameObject。
    /// </summary>
    GameObject GetActorGameObject(string actorName)
    {
        if (string.IsNullOrEmpty(actorName)) return null;

        _cachedActors.RemoveAll(a => a == null);
        foreach (var a in _cachedActors)
            if (a.actor == actorName) return a.gameObject;

        var all = FindObjectsOfType<DialogueActor>();
        foreach (var a in all)
        {
            if (!_cachedActors.Contains(a)) _cachedActors.Add(a);
            if (a.actor == actorName)
            {
                Debug.Log($"{LogTag} 找到 Actor: {actorName} → {a.gameObject.name}");
                return a.gameObject;
            }
        }

        Debug.LogWarning($"{LogTag} 找不到 Actor GameObject: {actorName}");
        return null;
    }

    // ── 工具 ──

    /// <summary>啟用時檢查此 item 是否已收集過，若是則直接停用自身。</summary>
    void CheckAlreadyCollected()
    {
        if (DialogueLua.GetVariable($"Item_{itemId}").asBool)
        {
            _isCollected = true;
            DisableSelf();
            Debug.Log($"{LogTag} itemId={itemId} 已收集，停用 spot");
        }
    }

    bool IsQuestActive()
    {
        if (string.IsNullOrEmpty(requiredQuestName)) return true;
        return QuestLog.GetQuestState(requiredQuestName) == QuestState.Active;
    }

    bool IsValidTag(string tag)
    {
        foreach (var t in triggerTags)
            if (tag == t) return true;
        return false;
    }

    void ShowPrompt() { if (promptUI) promptUI.SetActive(true); }
    void HidePrompt() { if (promptUI) promptUI.SetActive(false); }

    void DisableSelf()
    {
        HidePrompt();
        var col3D = GetComponent<Collider>();
        if (col3D) col3D.enabled = false;
        var col2D = GetComponent<Collider2D>();
        if (col2D) col2D.enabled = false;
    }

    void OnDrawGizmosSelected()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.color = _playerInRange ? Color.green : new Color(1f, 0.5f, 0f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider box)
            Gizmos.DrawWireCube(box.center, box.size);
        else if (col is SphereCollider sphere)
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
    }
}
