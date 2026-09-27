using UnityEngine;
using System.Collections.Generic;

[System.Flags]
public enum BookTabs
{
    None = 0,
    Page1 = 1 << 0,
    Page2 = 1 << 1,
    Page3 = 1 << 2,
    Page4 = 1 << 3,
    All = ~0
}

public enum DescriptionDisplayMode
{
    Merged,
    Separated
}

[CreateAssetMenu]
public class ItemData : ScriptableObject
{
    public int id;
    public string itemName;

    [Header("多描述系统")]
    [Tooltip("是否启用多描述模式")]
    public bool useMultipleDescriptions = false;

    [Tooltip("单一描述（旧版兼容）")]
    [TextArea] public string description;

    [Tooltip("多描述列表")]
    public List<DescriptionEntry> descriptions = new List<DescriptionEntry>();

    public Sprite icon;

    [Header("拓印字符設定")]
    [Tooltip("勾選後此物品代表一個可拓印的字符")]
    public bool isRubbingSymbol = false;

    [Tooltip("計入任務進度（false 為隱藏收藏，只收錄書本，不算任務數）")]
    public bool countsTowardQuest = true;

    [Tooltip("拓印底圖，游標滑過時顯示字符（Texture Import Settings 需開啟 Read/Write Enabled）")]
    public Texture2D rubbingSourceTexture;

    [Header("此物品在哪些页显示")]
    public BookTabs showIn = BookTabs.Page1;

    [System.Serializable]
    public class DescriptionEntry
    {
        [Tooltip("描述的唯一ID")]
        public string descriptionID = "desc_1";

        [Tooltip("描述内容")]
        [TextArea] public string content = "";

        [Tooltip("是否默认显示")]
        public bool activeByDefault = true;
    }

    [System.Serializable]
    public class PerPageOverride
    {
        [Tooltip("要用於哪些页")]
        public BookTabs pages;

        public Sprite icon;
        public string title;

        [Header("描述覆盖模式")]
        [Tooltip("覆盖模式")]
        public DescriptionOverrideMode mode = DescriptionOverrideMode.Replace;

        [Tooltip("当 mode=UpdateSpecific 时，指定要覆盖的 descriptionID")]
        public string targetDescriptionID = "";

        [Tooltip("顯示於指定頁面 SuffixText 的摘要文字")]
        [TextArea] public string summary;

        [Tooltip("当 mode=Add 时，新描述的ID")]
        public string newDescriptionID = "";

        [Header("Page4 條列顯示")]
        [Tooltip("Page4 點擊後逐句顯示的內容，每個元素為一句；留空則退回單段文字顯示")]
        [TextArea(2, 5)]
        public string[] linesB;
    }
    // ItemData.cs 新增這個方法
    /// <summary>取得基礎描述列表，不套 Override summary（供 BookSlotBinder 使用）。</summary>
    public List<string> GetBaseDescriptions(BookTabs page)
    {
        return GetBaseDescriptionList();
    }

    public enum DescriptionOverrideMode
    {
        Replace,
        Add,
        UpdateSpecific
    }

    [Header("每页覆写")]
    public List<PerPageOverride> overrides = new List<PerPageOverride>();

    [System.NonSerialized]
    private HashSet<string> runtimeActivatedDescriptions = new HashSet<string>();

    public bool VisibleOn(BookTabs page) => (showIn & page) != 0;

    /// <summary>取得指定頁的圖示／標題／摘要（summary 欄位）。</summary>
    public (Sprite icon, string title, string desc) For(BookTabs page, DescriptionDisplayMode displayMode = DescriptionDisplayMode.Merged)
    {
        for (int i = overrides.Count - 1; i >= 0; i--)
        {
            var ov = overrides[i];
            if ((ov.pages & page) == 0) continue;

            string finalDesc = GetFinalDescription(ov, displayMode);

            return (
                ov.icon ? ov.icon : icon,
                string.IsNullOrEmpty(ov.title) ? itemName : ov.title,
                finalDesc
            );
        }

        return (icon, itemName, GetBaseDescription(displayMode));
    }

    /// <summary>取得指定頁的 PerPageOverride（找不到回傳 null）。</summary>
    public PerPageOverride GetOverride(BookTabs page)
    {
        for (int i = overrides.Count - 1; i >= 0; i--)
        {
            if ((overrides[i].pages & page) != 0) return overrides[i];
        }
        return null;
    }

    /// <summary>取得獨立的描述列表（用于多个TMP）。</summary>
    public List<string> GetDescriptionList(BookTabs page)
    {
        for (int i = overrides.Count - 1; i >= 0; i--)
        {
            var ov = overrides[i];
            if ((ov.pages & page) == 0) continue;
            return GetFinalDescriptionList(ov);
        }

        return GetBaseDescriptionList();
    }

    string GetBaseDescription(DescriptionDisplayMode mode)
    {
        if (!useMultipleDescriptions || descriptions.Count == 0)
            return description;

        if (mode == DescriptionDisplayMode.Merged)
        {
            List<string> activeDescs = new List<string>();
            foreach (var entry in descriptions)
            {
                if (entry.activeByDefault || runtimeActivatedDescriptions.Contains(entry.descriptionID))
                {
                    if (!string.IsNullOrEmpty(entry.content))
                        activeDescs.Add(entry.content);
                }
            }
            return string.Join("\n\n", activeDescs);
        }

        return description;
    }

    List<string> GetBaseDescriptionList()
    {
        List<string> results = new List<string>();

        if (!useMultipleDescriptions || descriptions.Count == 0)
        {
            if (!string.IsNullOrEmpty(description))
                results.Add(description);
            return results;
        }

        foreach (var entry in descriptions)
        {
            if (entry.activeByDefault || runtimeActivatedDescriptions.Contains(entry.descriptionID))
            {
                if (!string.IsNullOrEmpty(entry.content))
                    results.Add(entry.content);
            }
        }

        return results;
    }

    string GetFinalDescription(PerPageOverride ov, DescriptionDisplayMode mode)
    {
        if (!useMultipleDescriptions)
            return string.IsNullOrEmpty(ov.summary) ? description : ov.summary;

        if (mode == DescriptionDisplayMode.Separated)
            return string.IsNullOrEmpty(ov.summary) ? description : ov.summary;

        switch (ov.mode)
        {
            case DescriptionOverrideMode.Replace:
                return string.IsNullOrEmpty(ov.summary) ? GetBaseDescription(mode) : ov.summary;

            case DescriptionOverrideMode.Add:
                string baseDesc = GetBaseDescription(mode);
                if (string.IsNullOrEmpty(ov.summary)) return baseDesc;
                return string.IsNullOrEmpty(baseDesc) ? ov.summary : $"{baseDesc}\n\n{ov.summary}";

            case DescriptionOverrideMode.UpdateSpecific:
                return GetDescriptionWithSpecificUpdate(ov.targetDescriptionID, ov.summary, mode);

            default:
                return GetBaseDescription(mode);
        }
    }

    List<string> GetFinalDescriptionList(PerPageOverride ov)
    {
        List<string> results = new List<string>();

        if (!useMultipleDescriptions)
        {
            if (!string.IsNullOrEmpty(ov.summary))
                results.Add(ov.summary);
            else if (!string.IsNullOrEmpty(description))
                results.Add(description);
            return results;
        }

        switch (ov.mode)
        {
            case DescriptionOverrideMode.Replace:
                if (!string.IsNullOrEmpty(ov.summary))
                    results.Add(ov.summary);
                else
                    return GetBaseDescriptionList();
                break;

            case DescriptionOverrideMode.Add:
                results = GetBaseDescriptionList();
                if (!string.IsNullOrEmpty(ov.summary))
                    results.Add(ov.summary);
                break;

            case DescriptionOverrideMode.UpdateSpecific:
                foreach (var entry in descriptions)
                {
                    bool isActive = entry.activeByDefault || runtimeActivatedDescriptions.Contains(entry.descriptionID);

                    if (entry.descriptionID == ov.targetDescriptionID)
                    {
                        if (!string.IsNullOrEmpty(ov.summary))
                            results.Add(ov.summary);
                    }
                    else if (isActive && !string.IsNullOrEmpty(entry.content))
                    {
                        results.Add(entry.content);
                    }
                }
                break;
        }

        return results;
    }

    string GetDescriptionWithSpecificUpdate(string targetID, string newContent, DescriptionDisplayMode mode)
    {
        List<string> finalDescs = new List<string>();

        foreach (var entry in descriptions)
        {
            bool isActive = entry.activeByDefault || runtimeActivatedDescriptions.Contains(entry.descriptionID);

            if (entry.descriptionID == targetID)
            {
                if (!string.IsNullOrEmpty(newContent))
                    finalDescs.Add(newContent);
            }
            else if (isActive && !string.IsNullOrEmpty(entry.content))
            {
                finalDescs.Add(entry.content);
            }
        }

        return string.Join("\n\n", finalDescs);
    }

    public void GetFor(BookTabs page, out Sprite outIcon, out string outTitle, out string outDesc)
    {
        var v = For(page);
        outIcon = v.icon; outTitle = v.title; outDesc = v.desc;
    }

    #region 运行时描述管理 API

    public void ActivateDescription(string descriptionID)
    {
        if (runtimeActivatedDescriptions.Contains(descriptionID))
        {
            Debug.LogWarning($"[ItemData] 描述 '{descriptionID}' 已经激活");
            return;
        }

        bool found = descriptions.Exists(d => d.descriptionID == descriptionID);
        if (!found)
        {
            Debug.LogError($"[ItemData] 找不到 descriptionID='{descriptionID}'");
            return;
        }

        runtimeActivatedDescriptions.Add(descriptionID);
        Debug.Log($"[ItemData] 已激活描述: {descriptionID}");
    }

    public void DeactivateDescription(string descriptionID)
    {
        if (runtimeActivatedDescriptions.Remove(descriptionID))
            Debug.Log($"[ItemData] 已取消激活描述: {descriptionID}");
    }

    public void AddRuntimeDescription(string descriptionID, string content, bool activateImmediately = true)
    {
        if (descriptions.Exists(d => d.descriptionID == descriptionID))
        {
            Debug.LogWarning($"[ItemData] 描述ID '{descriptionID}' 已存在，将更新内容");
            var existing = descriptions.Find(d => d.descriptionID == descriptionID);
            existing.content = content;

            if (activateImmediately)
                ActivateDescription(descriptionID);
            return;
        }

        descriptions.Add(new DescriptionEntry
        {
            descriptionID = descriptionID,
            content = content,
            activeByDefault = false
        });

        if (activateImmediately)
            ActivateDescription(descriptionID);

        Debug.Log($"[ItemData] 已新增运行时描述: {descriptionID}");
    }

    public void ResetRuntimeState()
    {
        runtimeActivatedDescriptions.Clear();
    }

    #endregion
}
