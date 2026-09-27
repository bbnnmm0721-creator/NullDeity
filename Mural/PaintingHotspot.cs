using UnityEngine;
using UnityEngine.Events;
using PixelCrushers.DialogueSystem;

[RequireComponent(typeof(Collider))]
public class PaintingHotspot : MonoBehaviour
{
    [Tooltip("线索ID或名称（选填）")]
    public string clueId;

    [Header("Quest Control")]
    [Tooltip("允许互动的任务名称（空=不检查）")]
    public string requiredQuestName = "CollectPaintings";

    [Tooltip("任务必须是 Active 状态才能互动")]
    public bool requireQuestActive = true;

    [Header("Inventory Integration")]
    [Tooltip("要添加到 Inventory 的物品 ID")]
    public int itemId = -1;

    [Tooltip("是否在点击后自动添加物品到 Inventory")]
    public bool addToInventoryOnPick = true;

    [Tooltip("添加物品后是否显示获得提示")]
    public bool showItemPopup = true;

    [Tooltip("是否只能获得一次（防止重复添加）")]
    public bool pickOnce = true;

    [Header("Visual Feedback")]
    [Tooltip("获得物品后是否禁用此热点")]
    public bool disableAfterPick = false;

    [Tooltip("获得物品后改变的材质（可选）")]
    public Material pickedMaterial;

    [Header("UI Feedback")]
    [Tooltip("任务未激活时的提示文字")]
    public string questNotActiveMessage = "現在還不能調查這裡...";

    [Header("Completion Settings")]
    [Tooltip("收集完成后要激活的 Conversation 名称")]
    public string completionConversation = "AfterMirual";

    [Tooltip("收集完成后延迟多久再退出并启动对话（秒）")]
    public float delayBeforeConversation = 2f;

    [Header("Events")]
    [Tooltip("被点击时触发")]
    public UnityEvent onPicked;

    [Tooltip("成功添加物品到 Inventory 后触发")]
    public UnityEvent onItemAdded;

    [Tooltip("任务未激活时触发")]
    public UnityEvent onQuestNotActive;

    [Tooltip("所有线索收集完成时触发")]
    public UnityEvent onAllCluesCollected;

    private bool hasBeenPicked = false;
    private Renderer[] renderers;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();

        if (itemId > 0 && addToInventoryOnPick)
        {
            ValidateItemId();
        }
    }

    void ValidateItemId()
    {
        if (InventoryManager.Inst != null && InventoryManager.Inst.database != null)
        {
            var itemData = InventoryManager.Inst.database.Get(itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[PaintingHotspot] ItemID {itemId} 在资料库中找不到！", this);
            }
            else
            {
                Debug.Log($"[PaintingHotspot] 已设定物品: {itemData.itemName} (ID: {itemId})", this);
            }
        }
    }

    public bool CanInteract()
    {
        if (string.IsNullOrEmpty(requiredQuestName))
        {
            return true;
        }

        QuestState questState = QuestLog.GetQuestState(requiredQuestName);

        if (requireQuestActive)
        {
            bool canInteract = (questState == QuestState.Active);
            if (!canInteract)
            {
                Debug.Log($"[PaintingHotspot] 任务 '{requiredQuestName}' 状态: {questState}，需要 Active", this);
            }
            return canInteract;
        }
        else
        {
            bool canInteract = (questState != QuestState.Unassigned);
            if (!canInteract)
            {
                Debug.Log($"[PaintingHotspot] 任务 '{requiredQuestName}' 尚未分配", this);
            }
            return canInteract;
        }
    }

    public void InvokePicked()
    {
        Debug.Log($"[Hotspot] 尝试拾取: {clueId}", this);

        if (!CanInteract())
        {
            Debug.Log($"[Hotspot] 任务未激活，无法拾取 {clueId}", this);

            if (!string.IsNullOrEmpty(questNotActiveMessage))
            {
                DialogueManager.ShowAlert(questNotActiveMessage);
            }

            onQuestNotActive?.Invoke();
            return;
        }

        if (pickOnce && hasBeenPicked)
        {
            Debug.Log($"[Hotspot] {clueId} 已经被获得过了", this);
            return;
        }

        PaintingManager.Instance?.DiscoverClue(clueId);

        if (addToInventoryOnPick && itemId > 0)
        {
            AddItemToInventory();
        }

        onPicked?.Invoke();

        hasBeenPicked = true;

        ApplyVisualFeedback();

        CheckAllCluesCollected();
    }

    void CheckAllCluesCollected()
    {
        if (PaintingManager.Instance == null) return;

        PaintingHotspot[] allHotspots = FindObjectsOfType<PaintingHotspot>(true);
        int totalClues = 0;
        int collectedClues = 0;

        Debug.Log($"[PaintingHotspot] 找到 {allHotspots.Length} 个 PaintingHotspot");
        Debug.Log($"[PaintingHotspot] 当前任务: '{requiredQuestName}'");

        foreach (var hotspot in allHotspots)
        {
            if (!string.IsNullOrEmpty(hotspot.clueId) &&
                hotspot.requiredQuestName == requiredQuestName)
            {
                bool hasClue = PaintingManager.Instance.HasClue(hotspot.clueId);
                Debug.Log($"[PaintingHotspot] - {hotspot.clueId}: {(hasClue ? "✓已收集" : "✗未收集")} (Active: {hotspot.gameObject.activeSelf})");
                totalClues++;
                if (hasClue)
                {
                    collectedClues++;
                }
            }
        }

        Debug.Log($"[PaintingHotspot] 收集進度: {collectedClues}/{totalClues}");

        if (totalClues > 0 && collectedClues >= totalClues)
        {
            DialogueLua.SetVariable("AllPaintingsCollected", true);
            Debug.Log("[PaintingHotspot] 所有壁画线索已收集完成！");

            if (!string.IsNullOrEmpty(requiredQuestName))
            {
                QuestLog.SetQuestState(requiredQuestName, QuestState.Success);
                Debug.Log($"[PaintingHotspot] 任务 '{requiredQuestName}' 已完成！");
            }

            QuestLog.SetQuestState("再次詢問老師", QuestState.Active);
            Debug.Log("[PaintingHotspot] 已激活任务 '再次詢問老師'");

            DialogueManager.ShowAlert("所有壁畫線索已收集完成！回去找老師吧。");

            onAllCluesCollected?.Invoke();

            if (PaintingManager.Instance != null && !string.IsNullOrEmpty(completionConversation))
            {
                PaintingManager.Instance.StartConversationAfterDelay(completionConversation, delayBeforeConversation);
            }
        }
    }

    void AddItemToInventory()
    {
        if (InventoryManager.Inst == null)
        {
            Debug.LogError("[PaintingHotspot] InventoryManager.Inst 为空！", this);
            return;
        }

        if (InventoryManager.Inst.Owns(itemId))
        {
            Debug.Log($"[PaintingHotspot] 物品 ID {itemId} 已经在 Inventory 中", this);
            if (!pickOnce)
            {
                InventoryManager.Inst.Add(itemId);
            }
            return;
        }

        InventoryManager.Inst.Add(itemId);
        Debug.Log($"[PaintingHotspot] 成功添加物品 ID {itemId} 到 Inventory", this);

        onItemAdded?.Invoke();

        if (showItemPopup)
        {
            ShowItemPopup();
        }
    }

    void ShowItemPopup()
    {
        if (InventoryManager.Inst?.database == null)
        {
            Debug.LogWarning("[PaintingHotspot] InventoryManager 或 database 为空", this);
            return;
        }

        var itemData = InventoryManager.Inst.database.Get(itemId);
        if (itemData != null)
        {
            ItemPopup.Show(itemId);
            Debug.Log($"[PaintingHotspot] 显示物品弹窗: {itemData.itemName}");
        }
        else
        {
            Debug.LogWarning($"[PaintingHotspot] 找不到 ItemID {itemId} 的资料", this);
        }
    }

    void ApplyVisualFeedback()
    {
        if (pickedMaterial != null && renderers != null)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.material = pickedMaterial;
                }
            }
            Debug.Log($"[PaintingHotspot] 已更改材质为已获得状态", this);
        }

        if (disableAfterPick)
        {
            gameObject.SetActive(false);
            Debug.Log($"[PaintingHotspot] 热点已禁用", this);
        }
    }

    [ContextMenu("Reset Hotspot")]
    public void ResetHotspot()
    {
        hasBeenPicked = false;
        gameObject.SetActive(true);
        Debug.Log("[PaintingHotspot] 热点状态已重置");
    }

    public bool IsItemOwned()
    {
        if (itemId <= 0 || InventoryManager.Inst == null) return false;
        return InventoryManager.Inst.Owns(itemId);
    }

    public void ForceAddItem()
    {
        if (itemId > 0 && InventoryManager.Inst != null)
        {
            InventoryManager.Inst.Add(itemId);
            onItemAdded?.Invoke();
            Debug.Log($"[PaintingHotspot] 强制添加物品 ID {itemId}");
        }
    }
}
