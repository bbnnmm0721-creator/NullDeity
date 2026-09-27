using UnityEngine;
using UnityEngine.SceneManagement;
using PixelCrushers.DialogueSystem;

[RequireComponent(typeof(CharacterController))]
public class SheepCompanion : MonoBehaviour
{
    public static SheepCompanion Instance { get; private set; }

    [System.Serializable]
    public class CompanionRule
    {
        [Header("任務條件")]
        [Tooltip("任務名稱列表（任一個Active即觸發）")]
        public string[] questNames = new string[0];

        [Header("場景條件")]
        [Tooltip("允許出現的場景關鍵字")]
        public string[] allowedSceneKeywords = new string[0];

        [Header("Spawn 設定")]
        [Tooltip("在符合條件的場景使用的 spawn ID（留空=跟隨玩家的spawn）")]
        public string spawnId = "";

        [Header("手動觸發設定")]
        [Tooltip("Lua 變數名稱（留空=任務Active時自動出現，填寫=需要對話中手動啟用）")]
        public string variableName = "";
    }

    [Header("跟隨設定")]
    [Tooltip("X軸跟隨距離")]
    public float followDistance = 2.5f;
    [Tooltip("X軸停止距離")]
    public float stopDistance = 1.5f;
    [Tooltip("移動速度")]
    public float moveSpeed = 3f;
    [Tooltip("平滑度")]
    public float smoothing = 0.125f;

    [Header("重力設定（類似 PlayerMovement）")]
    [Tooltip("重力加速度")]
    public float gravity = 16f;
    [Tooltip("落地檢測延遲")]
    public float groundedDelay = 0.1f;

    [Header("翻轉設定")]
    [Tooltip("Standby時翻轉方向")]
    public bool flipWhenStandby = true;

    [Header("動畫")]
    public string speedParameter = "speed";
    [Tooltip("速度閾值，低於此值視為靜止")]
    public float speedThreshold = 0.1f;
    [Tooltip("動畫速度平滑時間")]
    public float animSpeedSmoothing = 0.1f;

    [Header("多組任務場景配對")]
    [Tooltip("規則列表：每一組可設定「哪些任務」在「哪些場景」出現")]
    public CompanionRule[] companionRules = new CompanionRule[0];

    [Header("場景載入設定")]
    [Tooltip("場景載入後等待多久才檢查可見性")]
    public float sceneLoadDelay = 0.5f;

    [Header("Spawn 跟隨設定")]
    [Tooltip("跟隨玩家 spawn 時的 X 軸偏移")]
    public float spawnOffsetX = -2f;
    [Tooltip("是否在玩家左側生成（如果 false，會在右側）")]
    public bool spawnOnLeft = true;

    [Header("除錯選項")]
    [Tooltip("可見性檢查間隔（秒）")]
    public float visibilityCheckInterval = 1f;
    [Tooltip("顯示除錯訊息")]
    public bool showDebugLog = false;

    private Transform player;
    private CharacterController cc;
    private CharacterController playerCC;
    private Animator anim;

    private Vector3 velocity;
    private float velocityX = 0f;
    private float currentAnimSpeed = 0f;
    private float animSpeedVelocity = 0f;

    private bool facingRight = true;
    private bool isMoving = false;
    private bool wasMoving = false;
    private bool isGrounded = false;
    private float lastGroundedTime;

    private float lastVisibilityCheck = 0f;
    private string lastCheckedScene = "";
    private int lastMatchedRuleIndex = -1;

    // ── 互動優先級 ──────────────────────────────────────
    private Usable sheepUsable;
    private ProximitySelector playerProximitySelector;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void Start()
    {
        CheckAndUpdateVisibility();
        SetupPlayerCollisionIgnore();

        // 快取自身 Usable 和玩家的 ProximitySelector
        sheepUsable = GetComponent<Usable>();
        if (PlayerMovement.Instance != null)
            playerProximitySelector = PlayerMovement.Instance.GetComponent<ProximitySelector>();
    }

    void Update()
    {
        if (Time.time - lastVisibilityCheck >= visibilityCheckInterval)
        {
            CheckAndUpdateVisibility();
            lastVisibilityCheck = Time.time;
        }

        if (!gameObject.activeSelf) return;

        if (player == null)
        {
            if (PlayerMovement.Instance != null)
            {
                player = PlayerMovement.Instance.transform;
                SetupPlayerCollisionIgnore();
            }
            else
            {
                return;
            }
        }

        UpdateGroundedState();
        FollowPlayer();
        UpdateInteractionPriority();
    }

    void SetupPlayerCollisionIgnore()
    {
        if (PlayerMovement.Instance == null) return;

        if (playerCC == null)
        {
            playerCC = PlayerMovement.Instance.GetComponent<CharacterController>();
        }

        if (cc != null && playerCC != null)
        {
            Physics.IgnoreCollision(cc, playerCC, true);

            if (showDebugLog)
            {
                Debug.Log("[SheepCompanion] 已忽略與玩家 CharacterController 的碰撞（Box Collider Trigger 仍可檢測）");
            }
        }
    }

    /// <summary>
    /// 當玩家範圍內有其他 NPC 的 Usable 時，暫時停用羊的 Usable，
    /// 讓 ProximitySelector 自然選中優先級更高的 NPC 或其 Bark。
    /// </summary>
    void UpdateInteractionPriority()
    {
        if (sheepUsable == null) return;

        // 延遲取得 ProximitySelector（場景載入後才會存在）
        if (playerProximitySelector == null && PlayerMovement.Instance != null)
            playerProximitySelector = PlayerMovement.Instance.GetComponent<ProximitySelector>();

        if (playerProximitySelector == null) return;

        bool otherUsableInRange = false;
        foreach (var usable in playerProximitySelector.usablesInRange)
        {
            if (usable != null && usable != sheepUsable && usable.enabled && usable.gameObject.activeInHierarchy)
            {
                otherUsableInRange = true;
                break;
            }
        }

        // 只在狀態真的改變時才寫入，避免與 ResetDialogueComponents 衝突
        if (sheepUsable.enabled == otherUsableInRange)
        {
            sheepUsable.enabled = !otherUsableInRange;

            if (showDebugLog)
                Debug.Log($"[SheepCompanion] 互動優先級：{(otherUsableInRange ? "其他 NPC 在範圍內，停用羊的 Usable" : "恢復羊的 Usable")}");
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lastCheckedScene = "";
        lastMatchedRuleIndex = -1;
        playerCC = null;

        ResetDialogueComponents();

        if (IsInvoking(nameof(PlaceSheepAfterPlayer)))
        {
            CancelInvoke(nameof(PlaceSheepAfterPlayer));
        }
        Invoke(nameof(PlaceSheepAfterPlayer), sceneLoadDelay);
    }

    void PlaceSheepAfterPlayer()
    {
        CheckAndUpdateVisibility();

        if (gameObject.activeSelf)
        {
            int matchedRuleIndex;
            ShouldBeActive(out matchedRuleIndex);

            if (matchedRuleIndex >= 0 && matchedRuleIndex < companionRules.Length)
            {
                CompanionRule rule = companionRules[matchedRuleIndex];

                if (!string.IsNullOrEmpty(rule.spawnId))
                {
                    SpawnAtPosition(rule.spawnId);
                }
                else
                {
                    SpawnNearPlayer();
                }
            }
            else
            {
                SpawnNearPlayer();
            }
        }
    }

    void CheckAndUpdateVisibility()
    {
        int matchedRuleIndex;
        bool shouldBeActive = ShouldBeActive(out matchedRuleIndex);

        if (shouldBeActive && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            lastMatchedRuleIndex = matchedRuleIndex;

            if (showDebugLog)
            {
                Debug.Log($"[SheepCompanion] 顯示 sheep（規則 {matchedRuleIndex}）");
            }
        }
        else if (!shouldBeActive && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
            lastMatchedRuleIndex = -1;

            if (showDebugLog)
            {
                Debug.Log("[SheepCompanion] 隱藏 sheep");
            }
        }
    }

    bool ShouldBeActive(out int matchedRuleIndex)
    {
        matchedRuleIndex = -1;

        if (companionRules.Length == 0)
        {
            return false;
        }

        string sceneName = SceneManager.GetActiveScene().name;

        for (int i = 0; i < companionRules.Length; i++)
        {
            CompanionRule rule = companionRules[i];

            bool anyQuestActive = false;
            if (rule.questNames.Length > 0)
            {
                foreach (string quest in rule.questNames)
                {
                    if (!string.IsNullOrEmpty(quest))
                    {
                        if (QuestLog.GetQuestState(quest) == QuestState.Active)
                        {
                            anyQuestActive = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                anyQuestActive = true;
            }

            if (!anyQuestActive) continue;

            bool sceneMatches = false;
            if (rule.allowedSceneKeywords.Length > 0)
            {
                foreach (string keyword in rule.allowedSceneKeywords)
                {
                    if (sceneName.Contains(keyword))
                    {
                        sceneMatches = true;
                        break;
                    }
                }
            }
            else
            {
                sceneMatches = true;
            }

            if (!sceneMatches) continue;

            if (!string.IsNullOrEmpty(rule.variableName))
            {
                bool varValue = DialogueLua.GetVariable(rule.variableName).asBool;
                if (!varValue)
                {
                    if (showDebugLog)
                    {
                        Debug.Log($"[SheepCompanion] 規則 {i} 符合，但變數 '{rule.variableName}' = false");
                    }
                    continue;
                }
            }

            matchedRuleIndex = i;
            return true;
        }

        return false;
    }

    void SpawnNearPlayer()
    {
        if (PlayerMovement.Instance == null) return;

        Vector3 spawnPos = PlayerMovement.Instance.transform.position;

        if (spawnOnLeft)
        {
            spawnPos.x += spawnOffsetX;
        }
        else
        {
            spawnPos.x -= spawnOffsetX;
        }

        TeleportTo(spawnPos);

        if (showDebugLog)
        {
            Debug.Log($"[SheepCompanion] 在玩家附近生成：{spawnPos}");
        }
    }

    void SpawnAtPosition(string spawnId)
    {
        if (PlayerMovement.Instance == null) return;

        Vector3 spawnPos;

        if (!string.IsNullOrEmpty(spawnId))
        {
            SceneSpawnPoint targetSpawn = FindSpawnPoint(spawnId);
            if (targetSpawn != null)
            {
                spawnPos = targetSpawn.transform.position;

                if (showDebugLog)
                {
                    Debug.Log($"[SheepCompanion] 使用 Spawn Point: {spawnId}");
                }
            }
            else
            {
                spawnPos = PlayerMovement.Instance.transform.position;

                if (spawnOnLeft)
                {
                    spawnPos.x += spawnOffsetX;
                }
                else
                {
                    spawnPos.x -= spawnOffsetX;
                }

                if (showDebugLog)
                {
                    Debug.LogWarning($"[SheepCompanion] 找不到 Spawn Point: {spawnId}，使用玩家位置");
                }
            }
        }
        else
        {
            spawnPos = PlayerMovement.Instance.transform.position;

            if (spawnOnLeft)
            {
                spawnPos.x += spawnOffsetX;
            }
            else
            {
                spawnPos.x -= spawnOffsetX;
            }
        }

        TeleportTo(spawnPos);
        SetupPlayerCollisionIgnore();
    }

    void TeleportTo(Vector3 position)
    {
        if (cc != null)
        {
            cc.enabled = false;
            transform.position = position;
            cc.enabled = true;
        }
        else
        {
            transform.position = position;
        }

        player = PlayerMovement.Instance != null ? PlayerMovement.Instance.transform : null;
        velocity = Vector3.zero;
        velocityX = 0f;
        lastGroundedTime = Time.time;
    }

    SceneSpawnPoint FindSpawnPoint(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        SceneSpawnPoint[] points = FindObjectsOfType<SceneSpawnPoint>();
        string lowerID = id.ToLowerInvariant();

        foreach (var point in points)
        {
            if (point.id.ToLowerInvariant() == lowerID)
            {
                return point;
            }
        }

        return null;
    }

    void UpdateGroundedState()
    {
        if (cc == null) return;

        bool currentlyGrounded = cc.isGrounded;

        if (currentlyGrounded)
        {
            lastGroundedTime = Time.time;
            isGrounded = true;
        }
        else
        {
            isGrounded = (Time.time - lastGroundedTime) < groundedDelay;
        }
    }

    void FollowPlayer()
    {
        if (cc == null || player == null) return;

        Vector3 currentPos = transform.position;
        Vector3 targetPos = player.position;

        float horizontalDist = Mathf.Abs(targetPos.x - currentPos.x);
        float direction = Mathf.Sign(targetPos.x - currentPos.x);
        float targetAnimSpeed = 0f;

        if (horizontalDist > stopDistance)
        {
            float desiredX = targetPos.x - direction * stopDistance;
            float posError = desiredX - currentPos.x;
            float targetVelocityX;

            if (horizontalDist > followDistance)
            {
                // 追趕模式：距離超出 followDistance，用位置誤差快速追上
                targetVelocityX = posError / smoothing;
            }
            else
            {
                // 同步模式：以玩家實際速度為主，加上微量位置修正防止漂移
                float playerVelX = PlayerMovement.Instance != null
                    ? PlayerMovement.Instance.HorizontalVelocity / moveSpeed
                    : 0f;
                float posCorrection = Mathf.Clamp(posError / smoothing, -1f, 1f) * 0.25f;
                targetVelocityX = playerVelX + posCorrection;
            }

            velocityX = Mathf.Lerp(velocityX, targetVelocityX, Time.deltaTime / smoothing);
            velocity.x = velocityX * moveSpeed * Time.deltaTime;

            float actualSpeed = Mathf.Abs(velocityX);
            targetAnimSpeed = actualSpeed > speedThreshold ? actualSpeed : 0f;
            isMoving = targetAnimSpeed > 0f;

            if (isMoving)
            {
                if (direction > 0 && !facingRight) Flip();
                else if (direction < 0 && facingRight) Flip();
            }
        }
        else
        {
            // 停止區：清除動能，若已侵入則按侵入深度推離玩家
            velocityX = 0f;
            targetAnimSpeed = 0f;
            isMoving = false;

            float intrusion = stopDistance - horizontalDist;
            if (intrusion > 0.02f)
            {
                // 推離方向 = 遠離玩家
                float pushDir = -Mathf.Sign(targetPos.x - currentPos.x);
                float pushSpeed = Mathf.Clamp(intrusion * 4f, 0f, moveSpeed);
                velocity.x = pushDir * pushSpeed * Time.deltaTime;
            }
            else
            {
                velocity.x = 0f;
            }
        }

        if (isGrounded && velocity.y < 0f)
            velocity.y = -1f;
        else
            velocity.y -= gravity * Time.deltaTime;

        velocity.z = 0f;

        if (velocity.magnitude > 0.01f)
            cc.Move(velocity);

        if (wasMoving && !isMoving && flipWhenStandby)
            Flip();

        wasMoving = isMoving;

        currentAnimSpeed = Mathf.SmoothDamp(
            currentAnimSpeed,
            targetAnimSpeed,
            ref animSpeedVelocity,
            animSpeedSmoothing
        );

        if (anim != null)
            anim.SetFloat(speedParameter, currentAnimSpeed);
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    void ResetDialogueComponents()
    {
        var dialogueTrigger = GetComponent<PixelCrushers.DialogueSystem.DialogueSystemTrigger>();
        if (dialogueTrigger != null)
        {
            dialogueTrigger.enabled = false;
            dialogueTrigger.enabled = true;

            if (showDebugLog)
            {
                Debug.Log("[SheepCompanion] 已重置 Dialogue System Trigger");
            }
        }

        var usable = GetComponent<PixelCrushers.DialogueSystem.Usable>();
        if (usable != null)
        {
            usable.enabled = false;
            usable.enabled = true;

            if (showDebugLog)
            {
                Debug.Log("[SheepCompanion] 已重置 Usable 组件");
            }
        }
    }
    void OnBarkLine(Subtitle subtitle)
    {
        if (ScreenSpaceBarkUI.Instance == null) return;
        ScreenSpaceBarkUI.Instance.Bark(subtitle);
    }

    public void EnableByVariable(string variableName)
    {
        if (!string.IsNullOrEmpty(variableName))
        {
            DialogueLua.SetVariable(variableName, true);
        }
        CheckAndUpdateVisibility();
    }

    public void DisableByVariable(string variableName)
    {
        if (!string.IsNullOrEmpty(variableName))
        {
            DialogueLua.SetVariable(variableName, false);
        }
        CheckAndUpdateVisibility();
    }
}
