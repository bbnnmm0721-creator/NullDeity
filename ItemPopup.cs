using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class ItemPopup : MonoBehaviour
{
    static ItemPopup inst;

    [Header("UI References")]
    public GameObject itemPopupPanel;
    public Image background;
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text descText;
    public Button closeBtn;

    private PlayerInputActions inputActions;

    void Awake()
    {
        if (inst != null && inst != this)
        {
            Debug.LogWarning($"[ItemPopup] 偵測到重複的 ItemPopup，銷毀 {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        inst = this;

        DontDestroyOnLoad(gameObject);
        Debug.Log($"[ItemPopup] ✓ {gameObject.name} 已移至 DontDestroyOnLoad");

        inputActions = new PlayerInputActions();

        if (closeBtn)
        {
            closeBtn.onClick.AddListener(Hide);
        }

        if (itemPopupPanel)
        {
            itemPopupPanel.SetActive(false);
        }

        Debug.Log($"[ItemPopup] Awake 完成，監控中...");
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

        if (inst == this)
        {
            inst = null;
            Debug.Log("[ItemPopup] ItemPopup Singleton 已重置");
        }
    }

    void Update()
    {
        if (itemPopupPanel != null && itemPopupPanel.activeSelf && inputActions != null && inputActions.Player.Interact.WasPressedThisFrame())
        {
            Hide();
        }
    }

    public static void Show(int itemID)
    {
        if (inst == null)
        {
            Debug.LogError("[ItemPopup] ItemPopup Instance 為 null，請確保 persistent 場景中有 GetItemCanvas 並掛載 ItemPopup.cs");
            return;
        }

        if (inst.itemPopupPanel == null)
        {
            Debug.LogError("[ItemPopup] ItemPopupPanel 引用為 null，請在 Inspector 中綁定");
            return;
        }

        if (InventoryManager.Inst == null)
        {
            Debug.LogError("[ItemPopup] InventoryManager.Inst 為 null，請確保場景中有 InventoryManager");
            return;
        }

        var db = InventoryManager.Inst.database;
        if (db == null)
        {
            Debug.LogError("[ItemPopup] InventoryManager.database 為 null，請檢查 InventoryManager 是否已指定資料庫");
            return;
        }

        var data = db.Get(itemID);
        if (data == null)
        {
            Debug.LogError($"[ItemPopup] 找不到 itemID={itemID} 的物品資料，請檢查 ItemDatabase");
            return;
        }

        Debug.Log($"[ItemPopup] 顯示物品：id={itemID}, name={data.itemName}");

        if (inst.icon) inst.icon.sprite = data.icon;
        if (inst.nameText) inst.nameText.text = data.itemName;
        if (inst.descText) inst.descText.text = data.description;

        inst.itemPopupPanel.SetActive(true);
    }

    public void Hide()
    {
        if (itemPopupPanel != null)
        {
            itemPopupPanel.SetActive(false);
            Debug.Log("[ItemPopup] ItemPopupPanel 已關閉");
        }
    }
}
