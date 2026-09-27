using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 掛在 ManualPager 的每一個子頁 GameObject 上。
/// 負責：設定框架按鈕與顯示內容、追蹤本頁 pin/文字視覺、
/// 檢查全頁完成條件（Inspector 設定的所有 frame 都擁有且點過）、控制本頁 RedPaper。
/// </summary>
public class BookSubPage : MonoBehaviour
{
    [Header("框架（此分頁的 Frame GameObjects，順序對應槽位 0~N）")]
    public GameObject[] frames;

    [Header("每個框架對應的 itemId（與 frames 一對一）")]
    public int[] itemIds;

    [Header("子物件名稱前綴")]
    public string page4ItemChildName = "Page4Item";
    public string pinChildNamePrefix = "pin";
    public string paperTextChildName = "PaperText";
    public string suffixChildName = "SuffixText";

    [Header("全部 Frame 都問完後顯示的完成圖示")]
    public GameObject completionImage;

    [Header("此分頁的 RedPaper（可複數，全部問完才開啟）")]
    public GameObject[] redPaperObjects;

    private readonly Dictionary<int, GameObject> _pinMap = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, TMP_Text> _paperTextMap = new Dictionary<int, TMP_Text>();
    private readonly Dictionary<int, TMP_Text> _suffixTextMap = new Dictionary<int, TMP_Text>();

    private SoftwareCursorConfined _cursor;

    // ── Unity 生命週期 ────────────────────────────────────────

    void OnEnable()
    {
        SetRedPapers(false);
        if (completionImage) completionImage.SetActive(false);

        BookPageController.OnItemAsked += HandleItemAsked;

        SetupFrames();
        RefreshCompletion();
    }

    void OnDisable()
    {
        BookPageController.OnItemAsked -= HandleItemAsked;

        SetRedPapers(false);
        if (completionImage) completionImage.SetActive(false);
    }

    // ── 框架設置 ───────────────────────────────────────────────

    void SetupFrames()
    {
        _pinMap.Clear();
        _paperTextMap.Clear();
        _suffixTextMap.Clear();

        var ctrl = BookPageController.Inst;
        if (ctrl == null)
        {
            Debug.LogWarning("[BookSubPage] BookPageController.Inst 為 null。");
            return;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            if (frame == null) continue;

            if (i >= itemIds.Length)
            {
                Debug.LogWarning($"[BookSubPage] frames[{i}] 沒有對應的 itemId，請確認 itemIds 長度與 frames 相同。");
                frame.SetActive(false);
                continue;
            }

            int itemId = itemIds[i];
            bool owned = InventoryManager.Inst && InventoryManager.Inst.Owns(itemId);
            bool asked = ctrl.HasAsked(itemId);

            // 未獲得：整個 Frame 隱藏，不進行後續設置
            if (!owned)
            {
                frame.SetActive(false);
                continue;
            }

            frame.SetActive(true);

            // --- Pin ---
            var pin = FindChildByPrefix(frame.transform, pinChildNamePrefix);
            if (pin != null)
            {
                pin.SetActive(asked);
                _pinMap[itemId] = pin;
            }

            // --- Page4Item ---
            var page4Item = FindChildByPrefix(frame.transform, page4ItemChildName);
            if (page4Item == null) continue;

            page4Item.SetActive(true);

            var cg = page4Item.GetComponent<CanvasGroup>();
            if (cg) cg.alpha = 1f;

            // 從資料庫讀取 ItemData
            var db = ctrl.database;
            var data = db ? db.Get(itemId) : null;

            // --- Image ---
            if (data != null)
            {
                var view = data.For(ctrl.filter);
                var img = page4Item.GetComponentInChildren<Image>(true);
                if (img) { img.sprite = view.icon; img.color = Color.white; }
            }

            // --- PaperText ---
            var paperTx = FindTMPInDirectChildren(page4Item.transform, paperTextChildName);
            if (paperTx != null)
            {
                _paperTextMap[itemId] = paperTx;
                paperTx.text = data != null
                    ? (data.For(ctrl.filter).title ?? data.itemName ?? "")
                    : "";

                var tc = paperTx.color; tc.a = 1f; paperTx.color = tc;

                if (asked) paperTx.fontStyle |= FontStyles.Strikethrough;
                else paperTx.fontStyle &= ~FontStyles.Strikethrough;
            }

            // --- SuffixText ---
            var suffixTx = FindTMPInDirectChildren(page4Item.transform, suffixChildName);
            if (suffixTx != null)
            {
                _suffixTextMap[itemId] = suffixTx;
                suffixTx.text = ctrl.suffixMessage;
                var sc = suffixTx.color; sc.a = asked ? 1f : 0f; suffixTx.color = sc;
            }

            // --- Button ---
            var btn = page4Item.GetComponent<Button>() ?? page4Item.AddComponent<Button>();
            int idCapture = itemId;
            RectTransform frameRect = frame.GetComponent<RectTransform>();

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                // 只有游標熱點真正落在 Frame 螢幕矩形內才觸發
                if (!IsHotspotInsideFrame(frameRect)) return;
                ctrl.ShowRightInfo(idCapture);
            });
        }
    }

    // ── 事件處理 ───────────────────────────────────────────────

    /// <summary>收到 BookPageController 廣播後，更新本頁受影響的視覺。</summary>
    void HandleItemAsked(int itemId)
    {
        bool mine = _pinMap.ContainsKey(itemId)
                 || _paperTextMap.ContainsKey(itemId)
                 || _suffixTextMap.ContainsKey(itemId);

        if (!mine) return;

        if (_pinMap.TryGetValue(itemId, out var pin) && pin != null)
            pin.SetActive(true);

        if (_paperTextMap.TryGetValue(itemId, out var paperTx) && paperTx != null)
            paperTx.fontStyle |= FontStyles.Strikethrough;

        if (_suffixTextMap.TryGetValue(itemId, out var suffixTx) && suffixTx != null)
            StartCoroutine(BookPageController.Inst.CoFadeSuffixText(suffixTx));

        RefreshCompletion();
    }

    // ── 完成檢查 ───────────────────────────────────────────────

    /// <summary>
    /// Inspector 設定的所有 Frame，必須全部擁有且全部點過才算完成。
    /// 任何一個未擁有或未問過都回傳 false。
    /// </summary>
    void RefreshCompletion()
    {
        var ctrl = BookPageController.Inst;
        if (ctrl == null) return;

        bool allDone = IsAllAsked(ctrl);
        if (completionImage) completionImage.SetActive(allDone);
        SetRedPapers(allDone);
    }

    bool IsAllAsked(BookPageController ctrl)
    {
        bool hasAnyValidFrame = false;

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == null) continue;
            if (i >= itemIds.Length) continue;

            hasAnyValidFrame = true;

            int itemId = itemIds[i];
            bool owned = InventoryManager.Inst && InventoryManager.Inst.Owns(itemId);
            if (!owned || !ctrl.HasAsked(itemId)) return false;
        }

        return hasAnyValidFrame;
    }

    void SetRedPapers(bool active)
    {
        foreach (var rp in redPaperObjects)
            if (rp != null) rp.SetActive(active);
    }

    // ── 熱點驗證 ───────────────────────────────────────────────

    /// <summary>
    /// 檢查鉛筆游標熱點是否落在指定 Frame 的螢幕矩形內。
    /// 找不到游標時視為通過（保守降級，不擋住操作）。
    /// </summary>
    private bool IsHotspotInsideFrame(RectTransform frameRect)
    {
        if (frameRect == null) return true;

        if (_cursor == null)
            _cursor = FindObjectOfType<SoftwareCursorConfined>();
        if (_cursor == null) return true;

        Vector2 hotspot = _cursor.HotspotScreenPosition;

        Canvas canvas = frameRect.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                     ? canvas.worldCamera
                     : null;

        return RectTransformUtility.RectangleContainsScreenPoint(frameRect, hotspot, cam);
    }

    // ── 工具方法 ───────────────────────────────────────────────

    GameObject FindChildByPrefix(Transform parent, string prefix)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith(prefix)) return child.gameObject;
        }
        return null;
    }

    TMP_Text FindTMPInDirectChildren(Transform parent, string prefix)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith(prefix)) return child.GetComponent<TMP_Text>();
        }
        return null;
    }
}
