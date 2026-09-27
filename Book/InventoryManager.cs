using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Inst;
    public ItemDatabase database;

    [HideInInspector]
    public List<int> ownedIds = new List<int>();

    public event System.Action<int> OnItemAdded;
    public event System.Action<int, string> OnDescriptionActivated;

    void Awake()
    {
        if (Inst != null) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        ownedIds = new List<int>();
    }

    /// <summary>加入物品到背包並觸發 OnItemAdded 事件（書本自動刷新）。</summary>
    public void Add(int id)
    {
        if (ownedIds.Contains(id)) return;
        ownedIds.Add(id);
        OnItemAdded?.Invoke(id);
        Debug.Log($"添加物品 {id}  (Listener = {OnItemAdded?.GetInvocationList()?.Length ?? 0})");
    }

    public bool Owns(int id) => ownedIds.Contains(id);

    #region 描述管理 API

    /// <summary>激活指定物品的某個描述。</summary>
    public void ActivateItemDescription(int itemID, string descriptionID)
    {
        var item = database?.Get(itemID);
        if (item == null)
        {
            Debug.LogError($"[InventoryManager] 找不到 itemID={itemID}");
            return;
        }

        if (!item.useMultipleDescriptions)
        {
            Debug.LogWarning($"[InventoryManager] 物品 {itemID} 未启用多描述模式");
            return;
        }

        item.ActivateDescription(descriptionID);
        OnDescriptionActivated?.Invoke(itemID, descriptionID);

        Debug.Log($"[InventoryManager] 已激活物品 {itemID} 的描述: {descriptionID}");
    }

    /// <summary>為指定物品新增運行時描述。</summary>
    public void AddItemRuntimeDescription(int itemID, string descriptionID, string content, bool activateImmediately = true)
    {
        var item = database?.Get(itemID);
        if (item == null)
        {
            Debug.LogError($"[InventoryManager] 找不到 itemID={itemID}");
            return;
        }

        if (!item.useMultipleDescriptions)
        {
            Debug.LogWarning($"[InventoryManager] 物品 {itemID} 未启用多描述模式，正在自动启用");
            item.useMultipleDescriptions = true;
        }

        item.AddRuntimeDescription(descriptionID, content, activateImmediately);

        Debug.Log($"[InventoryManager] 已为物品 {itemID} 新增描述: {descriptionID}");
    }

    #endregion

    #region 拓印系統

    const string RubbingQuestName = "尋找可拓印物品";
    const string NextQuestName = "探索樹洞";
    const int RequiredCount = 4;

    /// <summary>
    /// 拓印完成後呼叫。沿用 WorldItem 的 Item_{id} 變數規則，
    /// 不產生額外 per-symbol 變數，並自動更新任務計數。
    /// 達到 4 個後自動完成任務並開啟「探索樹洞」。
    /// </summary>
    public void CollectRubbingSymbol(int itemId)
    {
        var item = database?.Get(itemId);
        if (item == null || !item.isRubbingSymbol)
        {
            Debug.LogWarning($"[InventoryManager] CollectRubbingSymbol: id={itemId} 不是拓印字符或找不到");
            return;
        }

        if (Owns(itemId)) return;

        DialogueLua.SetVariable($"Item_{itemId}", true);
        DialogueLua.SetVariable("LastRubbedItem", itemId);

        Add(itemId);

        if (item.countsTowardQuest)
        {
            float current = DialogueLua.GetVariable("RubbingQuestCount").asFloat;
            float newCount = current + 1;
            DialogueLua.SetVariable("RubbingQuestCount", newCount);

            Debug.Log($"[InventoryManager] RubbingQuestCount → {newCount}");

            if (newCount >= RequiredCount &&
                QuestLog.GetQuestState(RubbingQuestName) != QuestState.Success)
            {
                QuestLog.SetQuestState(RubbingQuestName, QuestState.Success);
                QuestLog.SetQuestState(NextQuestName, QuestState.Active);
                Debug.Log($"[InventoryManager] ✅ {RubbingQuestName} 完成，開啟 {NextQuestName}");
            }

            DialogueManager.SendUpdateTracker();
        }

        Debug.Log($"[InventoryManager] CollectRubbingSymbol 完成 itemId={itemId}");
    }

    #endregion
}
