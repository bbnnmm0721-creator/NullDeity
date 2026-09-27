using UnityEngine;

public class StairSystem : MonoBehaviour
{
    [System.Serializable]
    public class StairGroup
    {
        [Tooltip("这组楼梯的名称（用于 Debug）")]
        public string name = "Stair A";

        [Tooltip("二楼碰撞器")]
        public BoxCollider secondFloorCollider;

        [Tooltip("楼梯碰撞器（可以添加多个）")]
        public BoxCollider[] stairColliders;
    }

    [Header("楼梯组设定")]
    [Tooltip("可以添加多组楼梯")]
    public StairGroup[] stairGroups;

    [Header("楼梯 Order 控制")]
    [Tooltip("需要改变层级的楼梯物体")]
    public GameObject[] stairObjects;
    public int stairOrderA = 0;
    public int stairOrderB = 5;

    [Header("围栏 Order 控制（受 E 键状态控制）")]
    [Tooltip("需要改变层级的围栏物体（如二楼栏杆）")]
    public GameObject[] fenceObjects;
    [Tooltip("OnStairs 状态时的围栏层级（按 E 后）")]
    public int fenceOrderA = 1;
    [Tooltip("Normal 状态时的围栏层级（初始状态）")]
    public int fenceOrderB = 10;

    [Header("输入设置")]
    [Tooltip("使用 New Input System")]
    public bool useNewInputSystem = true;

    [Tooltip("Legacy 互动按键")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("提示物件（可以设置多个，如楼上、楼下各放一个）")]
    public GameObject[] interactPrompts;

    [Header("Debug 设定")]
    public bool enableDebugLogs = true;

    enum StairState { Normal, OnStairs }
    StairState currentState = StairState.Normal;

    bool playerInStairZone = false;
    bool playerInSecondFloor = false;
    bool hasActivatedStairs = false;

    SpriteRenderer[] cachedStairRenderers;
    SpriteRenderer[] cachedFenceRenderers;
    private PlayerInputActions inputActions;

    void Awake()
    {
        if (useNewInputSystem)
        {
            inputActions = new PlayerInputActions();
        }
    }

    void OnEnable()
    {
        if (useNewInputSystem && inputActions != null)
        {
            inputActions.Player.Enable();
        }
    }

    void OnDisable()
    {
        if (useNewInputSystem && inputActions != null)
        {
            inputActions.Player.Disable();
        }
    }

    void Start()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        if (trigger == null)
        {
            Debug.LogError("❌ StairSystem 物件上缺少 Box Collider！");
            return;
        }

        if (!trigger.isTrigger)
        {
            trigger.isTrigger = true;
            Debug.LogWarning("⚠️ 已自动将 Box Collider 设为 Trigger");
        }

        ValidateStairGroups();
        CacheRenderers();
        SetState(StairState.Normal);
        SetStairOrderInLayer(stairOrderA);
        SetFenceOrderInLayer(fenceOrderB);

        SetPromptsVisible(false);

        DebugLog("✅ 初始化完成");
    }

    void ValidateStairGroups()
    {
        if (stairGroups == null || stairGroups.Length == 0)
        {
            Debug.LogError("❌ 没有设定任何楼梯组！");
            return;
        }

        int validGroups = 0;
        foreach (var group in stairGroups)
        {
            if (group.secondFloorCollider != null && group.stairColliders != null && group.stairColliders.Length > 0)
            {
                validGroups++;
                DebugLog($"✅ 楼梯组 '{group.name}' 设定完整（{group.stairColliders.Length} 个楼梯碰撞器）");
            }
            else
            {
                Debug.LogWarning($"⚠️ 楼梯组 '{group.name}' 缺少碰撞器设定");
            }
        }

        DebugLog($"📊 总共有 {validGroups}/{stairGroups.Length} 组楼梯设定完整");
    }

    void CacheRenderers()
    {
        cachedStairRenderers = CacheRenderersFromObjects(stairObjects, "楼梯");
        cachedFenceRenderers = CacheRenderersFromObjects(fenceObjects, "围栏");
    }

    SpriteRenderer[] CacheRenderersFromObjects(GameObject[] objects, string objectTypeName)
    {
        if (objects == null || objects.Length == 0)
        {
            DebugLog($"⚠️ 没有设定 {objectTypeName} Objects");
            return new SpriteRenderer[0];
        }

        System.Collections.Generic.List<SpriteRenderer> rendererList = new System.Collections.Generic.List<SpriteRenderer>();

        foreach (var obj in objects)
        {
            if (obj == null) continue;
            var renderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
            rendererList.AddRange(renderers);
        }

        SpriteRenderer[] result = rendererList.ToArray();
        DebugLog($"✅ 快取 {result.Length} 个 {objectTypeName} SpriteRenderer");
        return result;
    }

    void Update()
    {
        bool interactPressed = false;

        if (useNewInputSystem && inputActions != null)
        {
            interactPressed = inputActions.Player.Interact.WasPressedThisFrame();
        }
        else
        {
            interactPressed = Input.GetKeyDown(interactKey);
        }

        if (interactPressed)
        {
            DebugLog($"🔍 按下交互键 | playerInStairZone={playerInStairZone} | currentState={currentState}");

            if (playerInStairZone && currentState == StairState.Normal)
            {
                DebugLog("▶️ 切换到楼梯状态");
                hasActivatedStairs = true;
                SetState(StairState.OnStairs);
                UpdateOrderInLayers();

                SetPromptsVisible(false);
            }
        }
    }

    public void OnPlayerEnterZone()
    {
        playerInStairZone = true;
        DebugLog("✅ 玩家进入楼梯区域");

        if (currentState == StairState.Normal)
        {
            SetPromptsVisible(true);
        }
    }

    public void OnPlayerExitZone()
    {
        playerInStairZone = false;
        hasActivatedStairs = false;
        DebugLog("❌ 玩家离开楼梯区域");

        SetPromptsVisible(false);

        if (currentState == StairState.OnStairs)
        {
            DebugLog("◀️ 切回正常状态（碰撞器）");
            SetState(StairState.Normal);
        }

        UpdateOrderInLayers();
    }

    public void OnPlayerEnterSecondFloor()
    {
        playerInSecondFloor = true;
        DebugLog("🏢 玩家进入二楼区域");
        UpdateOrderInLayers();
    }

    public void OnPlayerExitSecondFloor()
    {
        playerInSecondFloor = false;
        DebugLog("🏢 玩家离开二楼区域");
        UpdateOrderInLayers();
    }

    void SetPromptsVisible(bool visible)
    {
        if (interactPrompts == null || interactPrompts.Length == 0)
        {
            return;
        }

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
            DebugLog($"🖼️ {(visible ? "显示" : "隐藏")} {promptsChanged} 个提示UI");
        }
    }

    void SetState(StairState newState)
    {
        currentState = newState;

        if (stairGroups == null || stairGroups.Length == 0)
        {
            Debug.LogError("❌ 没有楼梯组可以控制");
            return;
        }

        foreach (var group in stairGroups)
        {
            if (group.secondFloorCollider == null || group.stairColliders == null || group.stairColliders.Length == 0)
            {
                DebugLog($"⚠️ 跳过楼梯组 '{group.name}'（碰撞器未设定）");
                continue;
            }

            switch (newState)
            {
                case StairState.Normal:
                    group.secondFloorCollider.enabled = true;
                    SetCollidersEnabled(group.stairColliders, false);
                    DebugLog($"🏢 [{group.name}] 状态 A (Normal) → 二楼碰撞器 ON，{group.stairColliders.Length} 个楼梯碰撞器 OFF");
                    break;

                case StairState.OnStairs:
                    group.secondFloorCollider.enabled = false;
                    SetCollidersEnabled(group.stairColliders, true);
                    DebugLog($"🪜 [{group.name}] 状态 B (OnStairs) → 二楼碰撞器 OFF，{group.stairColliders.Length} 个楼梯碰撞器 ON");
                    break;
            }
        }
    }

    void SetCollidersEnabled(BoxCollider[] colliders, bool enabled)
    {
        foreach (var collider in colliders)
        {
            if (collider != null)
            {
                collider.enabled = enabled;
            }
        }
    }

    void UpdateOrderInLayers()
    {
        bool shouldUseStairOrderB = (hasActivatedStairs && playerInStairZone) || playerInSecondFloor;
        int stairOrder = shouldUseStairOrderB ? stairOrderB : stairOrderA;

        int fenceOrder = (currentState == StairState.OnStairs) ? fenceOrderA : fenceOrderB;

        SetStairOrderInLayer(stairOrder);
        SetFenceOrderInLayer(fenceOrder);

        DebugLog($"📊 Order 更新：currentState={currentState}");
        DebugLog($"   → 楼梯 Order={stairOrder} (原逻辑), 围栏 Order={fenceOrder} (状态同步)");
    }

    void SetStairOrderInLayer(int order)
    {
        SetOrderInLayerForRenderers(cachedStairRenderers, order, "楼梯");
    }

    void SetFenceOrderInLayer(int order)
    {
        SetOrderInLayerForRenderers(cachedFenceRenderers, order, "围栏");
    }

    void SetOrderInLayerForRenderers(SpriteRenderer[] renderers, int order, string typeName)
    {
        if (renderers == null || renderers.Length == 0)
            return;

        foreach (var renderer in renderers)
        {
            if (renderer != null)
                renderer.sortingOrder = order;
        }
    }

    void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"<color=cyan>[StairSystem]</color> {message}");
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Dispose();
        }
    }
}
