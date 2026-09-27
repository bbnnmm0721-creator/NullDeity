using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractiveScenePortal : MonoBehaviour
{
    [System.Serializable]
    public class PortalDestination
    {
        [Header("场景设置")]
        public string sceneName = "Chapter1-1";
        public string spawnPoint = "Default";

        [Header("方向设置")]
        [Tooltip("使用向上（W/D-pad上）或向下（S/D-pad下）触发")]
        public PortalDirection direction = PortalDirection.Up;

        [Header("显示名称（仅用于 Debug）")]
        public string displayName = "前往走廊";

        [Header("Legacy - 已废弃")]
        [Tooltip("仅用于兼容旧代码，不再使用")]
        public KeyCode activationKey = KeyCode.None;
    }

    public enum PortalDirection
    {
        Up,     // W 键 / D-pad 上
        Down    // S 键 / D-pad 下
    }

    [Header("传送选项（可添加多个）")]
    public PortalDestination[] destinations;

    [Header("提示 UI 设置")]
    public GameObject[] interactPrompts;

    [Header("特效设置")]
    public ParticleSystem teleportEffect;
    public AudioSource audioSource;
    public AudioClip teleportSound;
    public Animator portalAnimator;
    public string activeTrigger = "Activate";

    [Header("进阶设置")]
    public string requiredTag = "Player";
    public bool enableDebugLog = false;
    public float triggerCooldown = 0.5f;

    static bool isLoading;
    bool playerInRange;
    private PlayerInputActions inputActions;
    private float lastTriggerTime = -999f;

    void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Enable();
        }
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Dispose();
        }
    }

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col)
        {
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Log("⚠️ 已自动将 Collider 设为 Trigger");
            }
        }
        else
        {
            Debug.LogError($"❌ [{gameObject.name}] 缺少 Collider 组件！");
        }

        SetPromptsVisible(false);
        ValidateDestinations();
        Log("✅ 初始化完成");
    }

    void Update()
    {
        if (!playerInRange || isLoading) return;
        if (Time.time - lastTriggerTime < triggerCooldown) return;
        if (destinations == null || destinations.Length == 0) return;

        foreach (var dest in destinations)
        {
            bool shouldTeleport = false;

            if (dest.direction == PortalDirection.Up && inputActions.Player.NavigateUp.WasPressedThisFrame())
            {
                shouldTeleport = true;
                Log($"✅ 检测到向上输入 (W / D-pad Up)");
            }
            else if (dest.direction == PortalDirection.Down && inputActions.Player.NavigateDown.WasPressedThisFrame())
            {
                shouldTeleport = true;
                Log($"✅ 检测到向下输入 (S / D-pad Down)");
            }

            if (shouldTeleport)
            {
                lastTriggerTime = Time.time;
                TeleportToScene(dest);
                break;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(requiredTag)) return;
        if (GlobalSceneTransition.IsBusy)
        {
            Log($"场景转换中，忽略 {other.name} 的触发");
            return;
        }

        playerInRange = true;
        SetPromptsVisible(true);

        if (portalAnimator && !string.IsNullOrEmpty(activeTrigger))
        {
            portalAnimator.SetTrigger(activeTrigger);
        }

        Log($"✅ {other.name} 进入传送门区域");
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(requiredTag)) return;
        playerInRange = false;
        SetPromptsVisible(false);
        Log($"❌ {other.name} 离开传送门区域");
    }

    void TeleportToScene(PortalDestination dest)
    {
        if (isLoading || GlobalSceneTransition.IsBusy)
        {
            Log("场景转换中，忽略传送请求");
            return;
        }

        isLoading = true;
        Log($"🌀 开始传送到 {dest.sceneName} @ {dest.spawnPoint}");
        SetPromptsVisible(false);

        if (teleportEffect) teleportEffect.Play();
        PlayTeleportSound();

        TravelData.SetNextSpawn(dest.spawnPoint);
        GlobalSceneTransition.Go(dest.sceneName, dest.spawnPoint);
    }

    void SetPromptsVisible(bool visible)
    {
        if (interactPrompts == null || interactPrompts.Length == 0) return;

        int promptsChanged = 0;
        foreach (var prompt in interactPrompts)
        {
            if (prompt != null)
            {
                prompt.SetActive(visible);
                promptsChanged++;
            }
        }

        if (promptsChanged > 0)
        {
            Log($"🖼️ {(visible ? "显示" : "隐藏")} {promptsChanged} 个提示 UI");
        }
    }

    void PlayTeleportSound()
    {
        if (!audioSource) return;
        if (teleportSound)
        {
            audioSource.clip = teleportSound;
            audioSource.Play();
        }
        else if (audioSource.clip)
        {
            audioSource.Play();
        }
    }

    void ValidateDestinations()
    {
        if (destinations == null || destinations.Length == 0)
        {
            Debug.LogWarning($"[InteractiveScenePortal] {gameObject.name} 没有配置传送目的地！");
            return;
        }

        Log($"📊 已配置 {destinations.Length} 个传送目的地:");

        for (int i = 0; i < destinations.Length; i++)
        {
            var dest = destinations[i];
            if (string.IsNullOrEmpty(dest.sceneName))
            {
                Debug.LogWarning($"[InteractiveScenePortal] 目的地 [{i}] ({dest.displayName}) 场景名称为空！");
            }
            else
            {
                string inputInfo = dest.direction == PortalDirection.Up ? "↑ (W/D-pad上)" : "↓ (S/D-pad下)";
                Log($"  {inputInfo} {dest.displayName} → {dest.sceneName} @ {dest.spawnPoint}");
            }
        }
    }

    void Log(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"<color=cyan>[InteractiveScenePortal {gameObject.name}]</color> {message}");
        }
    }

    public static void ResetLoadingState()
    {
        isLoading = false;
    }

    [ContextMenu("测试第一个传送")]
    public void TestFirstDestination()
    {
        if (destinations != null && destinations.Length > 0)
        {
            TeleportToScene(destinations[0]);
        }
        else
        {
            Debug.LogWarning("[InteractiveScenePortal] 没有配置传送目的地！");
        }
    }
}
