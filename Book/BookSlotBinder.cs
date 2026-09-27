using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BookSlotBinder : MonoBehaviour
{
    public int itemId;
    public BookTabs pageTag = BookTabs.Page1;

    [Tooltip("如果有指定，就用 CanvasGroup 显/隐藏；没指定就用 CanvasGroup 本身")]
    public GameObject visualRoot;

    public Image icon;
    public TMP_Text title;

    [Tooltip("Single = 单个TMP合并显示 | Multiple = 多个TMP分别显示")]
    public DescriptionMode descMode = DescriptionMode.Single;

    [Tooltip("单描述模式：主描述文本")]
    public TMP_Text desc;

    [Tooltip("多描述模式：额外的描述文本列表")]
    public List<TMP_Text> additionalDescs = new List<TMP_Text>();

    [Tooltip("Replace 模式：隱藏 desc，改顯示此欄（summary）")]
    public TMP_Text suffixText;

    [Tooltip("Add 模式：desc 保持顯示，額外補充此欄（summary）")]
    public TMP_Text addText;

    public GameObject lockedMask;

    public string lockedTitle = "？？？";
    public string lockedDesc = "尚未获得此物品";

    public enum DescriptionMode
    {
        Single,
        Multiple
    }

    CanvasGroup cg;

    void Awake()
    {
        if (visualRoot)
        {
            cg = visualRoot.GetComponent<CanvasGroup>();
            if (!cg) cg = visualRoot.AddComponent<CanvasGroup>();
        }
        else
        {
            cg = GetComponent<CanvasGroup>();
            if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        }
    }

    void OnEnable()
    {
        Subscribe(true);
        Refresh();

        BookPageController.OnItemAsked += HandleItemAsked;

        var ctrl = BookPageController.Inst;
        if (ctrl != null && ctrl.HasAsked(itemId))
            ApplyAskedVisual();
        else
            ResetAskedVisual();
    }

    void OnDisable()
    {
        Subscribe(false);
        BookPageController.OnItemAsked -= HandleItemAsked;
    }

    void Subscribe(bool on)
    {
        if (InventoryManager.Inst == null) return;

        if (on)
        {
            InventoryManager.Inst.OnItemAdded += OnItemAdded;
            InventoryManager.Inst.OnDescriptionActivated += OnDescriptionActivated;
        }
        else
        {
            InventoryManager.Inst.OnItemAdded -= OnItemAdded;
            InventoryManager.Inst.OnDescriptionActivated -= OnDescriptionActivated;
        }
    }

    void OnItemAdded(int id) { if (id == itemId) Refresh(); }

    void OnDescriptionActivated(int id, string descID)
    {
        if (id == itemId)
        {
            Debug.Log($"[BookSlotBinder] 物品 {itemId} 的描述 {descID} 已激活，刷新显示");
            Refresh();
        }
    }

    void HandleItemAsked(int id)
    {
        if (id != itemId) return;
        ApplyAskedVisual();
    }

    [ContextMenu("Refresh Now")]
    public void Refresh()
    {
        var inv = InventoryManager.Inst;
        var db = inv ? inv.database : null;
        bool owned = inv && inv.Owns(itemId);
        var data = db ? db.Get(itemId) : null;

        Sprite vIcon = null;
        string vTitle = null;
        string vDesc = null;
        List<string> vDescList = new List<string>();

        if (data != null)
        {
            if (descMode == DescriptionMode.Multiple)
            {
                // icon/title 仍從 Override 取，描述只取基礎列表，不含 summary
                var v = data.For(pageTag, DescriptionDisplayMode.Separated);
                vIcon = v.icon;
                vTitle = v.title;
                vDescList = data.GetBaseDescriptions(pageTag);
            }
            else
            {
                // icon/title 從 Override 取，描述直接用原始 description，不讓 summary 混入
                var v = data.For(pageTag, DescriptionDisplayMode.Merged);
                vIcon = v.icon;
                vTitle = v.title;
                vDesc = data.description;
            }
        }

        if (!cg) cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        cg.alpha = owned ? 1f : 0f;
        cg.interactable = owned;
        cg.blocksRaycasts = owned;

        if (icon)
        {
            icon.sprite = owned ? vIcon : null;
            var c = icon.color;
            c.a = (owned && icon.sprite) ? 1f : 0f;
            icon.color = c;
        }

        if (title)
            title.text = owned ? (vTitle ?? data?.itemName ?? "") : lockedTitle;

        if (descMode == DescriptionMode.Single)
        {
            if (desc)
                desc.text = owned ? (vDesc ?? data?.description ?? "") : lockedDesc;

            foreach (var txt in additionalDescs)
                if (txt != null) txt.gameObject.SetActive(false);
        }
        else
        {
            if (owned && vDescList.Count > 0)
            {
                if (desc)
                {
                    desc.text = vDescList[0];
                    desc.gameObject.SetActive(true);
                }

                for (int i = 0; i < additionalDescs.Count; i++)
                {
                    if (additionalDescs[i] == null) continue;
                    int descIndex = i + 1;
                    if (descIndex < vDescList.Count)
                    {
                        additionalDescs[i].text = vDescList[descIndex];
                        additionalDescs[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        additionalDescs[i].gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                if (desc)
                {
                    desc.text = lockedDesc;
                    desc.gameObject.SetActive(true);
                }

                foreach (var txt in additionalDescs)
                    if (txt != null) txt.gameObject.SetActive(false);
            }
        }

        if (lockedMask)
            lockedMask.SetActive(!owned);

        // 根據是否已問過，決定覆寫視覺
        var ctrl = BookPageController.Inst;
        if (ctrl != null && ctrl.HasAsked(itemId))
            ApplyAskedVisual();
        else
            ResetAskedVisual();
    }

    /// <summary>問過 Page4 後，根據 Override 模式顯示 suffixText 或 addText。</summary>
    void ApplyAskedVisual()
    {
        var ctrl = BookPageController.Inst;
        if (ctrl == null || !ctrl.HasAsked(itemId)) return;

        var db = ctrl.database;
        var data = db ? db.Get(itemId) : null;
        if (data == null) return;

        var ov = data.GetOverride(pageTag);
        string summary = ov != null ? ov.summary : "";

        if (ov != null && ov.mode == ItemData.DescriptionOverrideMode.Replace)
        {
            // 隱藏原描述，顯示 suffixText
            if (desc) desc.gameObject.SetActive(false);
            if (suffixText)
            {
                suffixText.text = summary;
                suffixText.gameObject.SetActive(true);
            }
            if (addText) addText.gameObject.SetActive(false);
        }
        else if (ov != null && ov.mode == ItemData.DescriptionOverrideMode.Add)
        {
            // 保留原描述，額外顯示 addText
            if (desc) desc.gameObject.SetActive(true);
            if (addText)
            {
                addText.text = summary;
                addText.gameObject.SetActive(true);
            }
            if (suffixText) suffixText.gameObject.SetActive(false);
        }
    }

    /// <summary>未問過時，確保 suffixText 與 addText 均隱藏，desc 恢復顯示。</summary>
    void ResetAskedVisual()
    {
        if (desc) desc.gameObject.SetActive(true);

        if (suffixText)
        {
            suffixText.text = "";
            suffixText.gameObject.SetActive(false);
        }

        if (addText)
        {
            addText.text = "";
            addText.gameObject.SetActive(false);
        }
    }
}
