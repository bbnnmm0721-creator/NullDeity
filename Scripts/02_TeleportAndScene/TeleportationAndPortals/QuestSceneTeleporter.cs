using UnityEngine;
using PixelCrushers.DialogueSystem;
using System.Collections;

public class QuestSceneTeleporter : MonoBehaviour
{
    public static QuestSceneTeleporter Instance { get; private set; }

    [System.Serializable]
    public class TeleportRule
    {
        [Header("觸發條件")]
        [Tooltip("監聽的任務名稱")]
        public string questName = "";

        [Tooltip("當任務變為此狀態時觸發")]
        public QuestState triggerOnState = QuestState.Active;

        [Header("傳送目標")]
        [Tooltip("目標場景名稱（需與Build Settings中一致）")]
        public string targetScene = "";

        [Tooltip("目標出生點ID（Default/LeftEnd/RightEnd等）")]
        public string targetSpawnId = "Default";

        [Header("選項")]
        [Tooltip("是否只觸發一次（避免重複傳送）")]
        public bool triggerOnce = true;

        [Tooltip("傳送延遲（秒）")]
        public float delay = 0f;

        [HideInInspector]
        public bool hasTriggered = false;
    }

    [Header("傳送規則")]
    [Tooltip("任務觸發的傳送規則列表")]
    public TeleportRule[] teleportRules = new TeleportRule[0];

    [Header("除錯選項")]
    [Tooltip("傳送後等待多久檢查玩家狀態（秒）")]
    public float playerCheckDelay = 2.5f;

    private bool isTeleporting = false;
    private float delayTimer = 0f;
    private TeleportRule pendingRule = null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (isTeleporting)
        {
            if (pendingRule != null && delayTimer > 0f)
            {
                delayTimer -= Time.deltaTime;
                if (delayTimer <= 0f)
                {
                    ExecuteTeleport(pendingRule.targetScene, pendingRule.targetSpawnId);
                    pendingRule = null;
                }
            }
            return;
        }

        CheckTeleportRules();
    }

    void CheckTeleportRules()
    {
        foreach (var rule in teleportRules)
        {
            if (string.IsNullOrEmpty(rule.questName)) continue;
            if (rule.triggerOnce && rule.hasTriggered) continue;

            QuestState currentState = QuestLog.GetQuestState(rule.questName);

            if (currentState == rule.triggerOnState)
            {
                Debug.Log($"[QuestSceneTeleporter] 任務 '{rule.questName}' 觸發傳送 → {rule.targetScene} @ {rule.targetSpawnId}");

                if (rule.triggerOnce)
                {
                    rule.hasTriggered = true;
                }

                if (rule.delay > 0f)
                {
                    isTeleporting = true;
                    delayTimer = rule.delay;
                    pendingRule = rule;
                }
                else
                {
                    ExecuteTeleport(rule.targetScene, rule.targetSpawnId);
                }

                break;
            }
        }
    }

    public void TeleportToScene(string sceneName, string spawnId = "Default", float delay = 0f)
    {
        if (isTeleporting)
        {
            Debug.LogWarning("[QuestSceneTeleporter] 已經在傳送中，忽略此次請求");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[QuestSceneTeleporter] 場景名稱為空！");
            return;
        }

        Debug.Log($"[QuestSceneTeleporter] 手動觸發傳送 → {sceneName} @ {spawnId}");

        if (delay > 0f)
        {
            isTeleporting = true;
            delayTimer = delay;
            pendingRule = new TeleportRule
            {
                targetScene = sceneName,
                targetSpawnId = spawnId
            };
        }
        else
        {
            ExecuteTeleport(sceneName, spawnId);
        }
    }

    void ExecuteTeleport(string sceneName, string spawnId)
    {
        if (GlobalSceneTransition.Inst == null)
        {
            Debug.LogError("[QuestSceneTeleporter] GlobalSceneTransition.Inst 不存在！使用基本場景載入");
            TravelData.SetNextSpawn(spawnId);
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            return;
        }

        isTeleporting = true;
        TravelData.SetNextSpawn(spawnId);
        GlobalSceneTransition.Go(sceneName, spawnId);

        StartCoroutine(EnsurePlayerEnabled());
    }

    IEnumerator EnsurePlayerEnabled()
    {
        yield return new WaitForSeconds(playerCheckDelay);

        var player = PlayerMovement.Instance;
        if (player != null)
        {
            if (!player.enabled)
            {
                player.enabled = true;
                Debug.Log("[QuestSceneTeleporter] ✅ 強制啟用 PlayerMovement 腳本");
            }

            if (player.isMovementLocked)
            {
                player.isMovementLocked = false;
                Debug.Log("[QuestSceneTeleporter] ✅ 解鎖玩家移動");
            }
        }
        else
        {
            Debug.LogWarning("[QuestSceneTeleporter] ⚠️ 找不到 PlayerMovement.Instance");
        }

        isTeleporting = false;
        pendingRule = null;

        Debug.Log("[QuestSceneTeleporter] 傳送完成，玩家狀態已檢查");
    }

    public void ResetTeleportRule(string questName)
    {
        foreach (var rule in teleportRules)
        {
            if (rule.questName == questName)
            {
                rule.hasTriggered = false;
                Debug.Log($"[QuestSceneTeleporter] 重置任務 '{questName}' 的傳送規則");
                break;
            }
        }
    }

    public void ResetAllTeleportRules()
    {
        foreach (var rule in teleportRules)
        {
            rule.hasTriggered = false;
        }
        Debug.Log("[QuestSceneTeleporter] 重置所有傳送規則");
    }
}
