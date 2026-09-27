using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class EventBookUI : MonoBehaviour
{
    public BookTabs filter = BookTabs.Page1;

    public Transform listParent;
    public GameObject listEntryPrefab;
    public Image infoIcon;
    public TMP_Text infoText;

    [Tooltip("Replace 模式：隱藏 infoText，改顯示此文字（summary）")]
    public TMP_Text suffixText;
    [Tooltip("Add 模式：infoText 保持顯示，額外補充此文字（summary）")]
    public TMP_Text addText;

    [SerializeField] ScrollRect scrollRect;

    readonly List<Button> entryBtns = new();
    readonly Dictionary<int, TMP_Text> id2Text = new();

    private const string SuffixObjectName = "SuffixText_Page2";

    void Awake()
    {
        EnsureSuffixText();
        ResetInfoDisplay();
        StartCoroutine(InitWhenManagerReady());
    }

    void OnEnable()
    {
        if (InventoryManager.Inst != null)
            InventoryManager.Inst.OnItemAdded += RefreshEntry;
    }

    void OnDisable()
    {
        if (InventoryManager.Inst != null)
            InventoryManager.Inst.OnItemAdded -= RefreshEntry;
    }

    void OnDestroy()
    {
        if (InventoryManager.Inst != null)
            InventoryManager.Inst.OnItemAdded -= RefreshEntry;
    }

    IEnumerator InitWhenManagerReady()
    {
        while (InventoryManager.Inst == null ||
               InventoryManager.Inst.database == null)
            yield return null;

        BuildList();
        InventoryManager.Inst.OnItemAdded += RefreshEntry;
    }

    void BuildList()
    {
        Debug.Log($"[BuildList] prefab={listEntryPrefab} parent={listParent}");

        var db = InventoryManager.Inst.database;

        foreach (var item in db.items)
        {
            if ((item.showIn & filter) == 0) continue;

            if (item == null)
            {
                Debug.LogWarning("[BuildList] Database 有空白項目，已略過！");
                continue;
            }

            GameObject go = Instantiate(listEntryPrefab, listParent);
            Button btn = go.GetComponent<Button>();
            TMP_Text tx = go.GetComponentInChildren<TMP_Text>(true);

            if (tx == null)
            {
                Debug.LogError($"[BuildList] 找不到 TMP_Text！檢查 {listEntryPrefab.name} 結構");
                continue;
            }

            bool owned = InventoryManager.Inst.Owns(item.id);
            tx.text = owned ? item.itemName : "？？？";

            int capturedId = item.id;
            btn.onClick.AddListener(() => OnEntryClick(capturedId));

            entryBtns.Add(btn);
            id2Text[item.id] = tx;
        }
    }

    void RefreshList()
    {
        foreach (var kv in id2Text)
        {
            bool owned = InventoryManager.Inst.Owns(kv.Key);
            kv.Value.text = owned
                ? InventoryManager.Inst.database.Get(kv.Key).itemName
                : "？？？";
        }
    }

    void RefreshEntry(int id)
    {
        if (!id2Text.TryGetValue(id, out TMP_Text tx)) return;

        tx.text = InventoryManager.Inst.database.Get(id).itemName;

        RectTransform row = tx.transform.parent.GetComponent<RectTransform>();
        float viewportH = scrollRect.viewport.rect.height;
        float contentH = scrollRect.content.rect.height;
        float rowY = -row.anchoredPosition.y;
        float normalized = 1f - Mathf.Clamp01((rowY - viewportH * 0.5f) / (contentH - viewportH));
        scrollRect.verticalNormalizedPosition = normalized;
    }

    void OnEntryClick(int id)
    {
        var data = InventoryManager.Inst.database.Get(id);
        bool own = InventoryManager.Inst.Owns(id);

        infoIcon.sprite = own ? data.icon : null;
        Color iconColor = infoIcon.color;
        iconColor.a = own ? 1f : 0f;
        infoIcon.color = iconColor;

        infoText.text = own ? data.description : "尚未取得此物品…";

        // 還原 infoText alpha（ResetInfoDisplay 在 Awake 設為 0，點擊後必須還原）
        Color textColor = infoText.color;
        textColor.a = 1f;
        infoText.color = textColor;

        ClearOverride();

        var ctrl = BookPageController.Inst;
        if (own && ctrl != null && ctrl.HasAsked(id))
            ApplyOverride(id);
    }

    /// <summary>收到 Page4 問過廣播；只有右側正在顯示同一 item 時才立即更新。</summary>
    public void OnItemAsked(int id)
    {
        ApplyOverride(id);
    }

    /// <summary>
    /// Replace 模式：隱藏 infoText，顯示 suffixText（summary）。
    /// Add 模式：infoText 保持，顯示 addText（summary）。
    /// suffixText 與 addText 不會同時出現。
    /// </summary>
    void ApplyOverride(int id)
    {
        var ctrl = BookPageController.Inst;
        var db = ctrl ? ctrl.database : null;
        var ov = db ? db.Get(id)?.GetOverride(BookTabs.Page2) : null;

        string summary = ov != null ? ov.summary : "";

        if (ov != null && ov.mode == ItemData.DescriptionOverrideMode.Replace)
        {
            if (infoText) infoText.gameObject.SetActive(false);
            if (suffixText) suffixText.text = summary ?? "";
            if (addText) addText.text = "";
        }
        else
        {
            if (addText) addText.text = summary ?? "";
            if (suffixText) suffixText.text = "";
        }
    }

    /// <summary>切換 item 時清除覆寫視覺，恢復 infoText。</summary>
    void ClearOverride()
    {
        if (infoText) infoText.gameObject.SetActive(true);
        if (suffixText) suffixText.text = "";
        if (addText) addText.text = "";
    }

    /// <summary>初始化時隱藏 infoIcon 與 infoText，確保無選取物件時 RightInfo 為空白。</summary>
    void ResetInfoDisplay()
    {
        if (infoIcon != null)
        {
            Color c = infoIcon.color;
            c.a = 0f;
            infoIcon.color = c;
        }

        if (infoText != null)
        {
            Color c = infoText.color;
            c.a = 0f;
            infoText.color = c;
        }
    }

    void EnsureSuffixText()
    {
        if (suffixText != null) return;

        Transform parent = infoText != null ? infoText.transform.parent : transform;
        Transform existing = parent.Find(SuffixObjectName);
        if (existing != null)
        {
            suffixText = existing.GetComponent<TMP_Text>();
            return;
        }

        GameObject go = new GameObject(SuffixObjectName, typeof(RectTransform), typeof(TMP_Text));
        go.transform.SetParent(parent, false);
        suffixText = go.GetComponent<TMP_Text>();
        suffixText.text = "";
    }
}
