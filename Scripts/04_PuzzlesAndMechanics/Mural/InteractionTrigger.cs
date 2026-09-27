using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class InteractionTrigger : MonoBehaviour
{
    [Header("Interaction")]
    public string interactionText = "按 E 互動";

    [Header("Events")]
    public UnityEvent onInteract;

    [Header("UI (Optional)")]
    public GameObject promptUI;

    [Header("Detection")]
    public string[] triggerTags = { "Player", "MainCamera" };

    private bool playerInRange = false;
    private GameObject currentTriggerObject = null;
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

    void Start()
    {
        Debug.Log($"[InteractionTrigger] {gameObject.name} 初始化");

        Collider col = GetComponent<Collider>();
        if (!col)
        {
            Debug.LogError($"[InteractionTrigger] {gameObject.name} 缺少 Collider 組件！");
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning($"[InteractionTrigger] {gameObject.name} 的 Collider 沒有設為 Trigger，自動設定");
            col.isTrigger = true;
        }

        if (promptUI)
        {
            promptUI.SetActive(false);
        }

        if (onInteract.GetPersistentEventCount() == 0)
        {
            Debug.LogWarning($"[InteractionTrigger] {gameObject.name} 的 onInteract 事件沒有設定！");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        bool isValidTrigger = false;

        if (triggerTags.Length == 0)
        {
            isValidTrigger = true;
        }
        else
        {
            foreach (string tag in triggerTags)
            {
                if (other.CompareTag(tag))
                {
                    isValidTrigger = true;
                    break;
                }
            }
        }

        if (isValidTrigger)
        {
            playerInRange = true;
            currentTriggerObject = other.gameObject;
            Debug.Log($"[InteractionTrigger] {other.name} 進入 {gameObject.name} 互動範圍");
            if (promptUI) promptUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (currentTriggerObject == other.gameObject)
        {
            playerInRange = false;
            currentTriggerObject = null;
            Debug.Log($"[InteractionTrigger] {other.name} 離開 {gameObject.name} 互動範圍");
            if (promptUI) promptUI.SetActive(false);
        }
    }

    void Update()
    {
        if (playerInRange && inputActions.Player.Interact.WasPressedThisFrame())
        {
            Debug.Log($"[InteractionTrigger] {gameObject.name} 互動觸發！");
            onInteract?.Invoke();
        }
    }

    void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col)
        {
            Gizmos.color = playerInRange ? Color.green : Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
    }
}
