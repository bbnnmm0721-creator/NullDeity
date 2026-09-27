using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class InteractUnlockUI : MonoBehaviour
{
    [Header("要被開啟的 UIRoot")]
    public GameObject uiRoot;

    [Header("互動提示（可選）")]
    public GameObject prompt;

    [Header("使用設定")]
    public bool destroyAfterUse = true;

    [Header("調試設定")]
    public bool enableDebugLog = true;

    private bool inRange;
    private bool used;
    private PlayerInputActions inputActions;

    void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    void OnEnable()
    {
        inputActions?.Player.Enable();
    }

    void OnDisable()
    {
        inputActions?.Player.Disable();
    }

    void OnDestroy()
    {
        inputActions?.Dispose();
    }

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Start()
    {
        if (uiRoot == null)
        {
            uiRoot = GameObject.Find("UIRoot");
            if (enableDebugLog)
            {
                if (uiRoot != null)
                    Debug.Log($"[InteractUnlockUI] 自動找到 UIRoot: {uiRoot.name}");
                else
                    Debug.LogWarning("[InteractUnlockUI] 找不到 UIRoot 物件！");
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (used) return;
        if (!other.CompareTag("Player")) return;

        inRange = true;
        if (prompt) prompt.SetActive(true);

        if (enableDebugLog)
            Debug.Log("[InteractUnlockUI] 玩家進入互動範圍");
    }

    void OnTriggerExit(Collider other)
    {
        if (used) return;
        if (!other.CompareTag("Player")) return;

        inRange = false;
        if (prompt) prompt.SetActive(false);

        if (enableDebugLog)
            Debug.Log("[InteractUnlockUI] 玩家離開互動範圍");
    }

    void Update()
    {
        if (used || !inRange) return;
        if (inputActions.Player.Interact.WasPressedThisFrame())
            Activate();
    }

    void Activate()
    {
        used = true;

        if (enableDebugLog)
            Debug.Log("[InteractUnlockUI] 開始處理 UIRoot");

        if (uiRoot)
        {
            uiRoot.SetActive(true);
            PanelCanvasToggle.SetUnlocked(true);

            if (enableDebugLog)
                Debug.Log($"[InteractUnlockUI] UIRoot 已處理: {uiRoot.name}，解鎖狀態已設為 true");
        }
        else
        {
            Debug.LogError("[InteractUnlockUI] uiRoot 物件未設定！");
        }

        if (prompt) prompt.SetActive(false);

        if (destroyAfterUse)
        {
            if (enableDebugLog)
                Debug.Log("[InteractUnlockUI] 標記使用後刪除，即將刪除");
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void SetUIRoot(GameObject target)
    {
        uiRoot = target;
        if (enableDebugLog)
            Debug.Log($"[InteractUnlockUI] UIRoot 參照已手動設定為: {target?.name}");
    }
}
