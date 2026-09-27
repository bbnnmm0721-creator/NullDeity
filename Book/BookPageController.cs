using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Page4 的中央顯示控制器。
/// 負責：中央文字淡入淡出、眼睛動畫、已問過紀錄（_askedItemIds）。
/// 分頁切換由 ManualPager 負責；每個子頁的框架設置與完成檢查由 BookSubPage 負責。
/// </summary>
public class BookPageController : MonoBehaviour
{
    public static BookPageController Inst { get; private set; }

    /// <summary>當某個物品被問過時觸發，傳入 itemId。</summary>
    public static event Action<int> OnItemAsked;

    [Header("數據 & 篩選")]
    public ItemDatabase database;
    public BookTabs filter = BookTabs.Page4;

    [Header("中央顯示")]
    public TMP_Text centerText;
    public float fadeTime = 0.35f;

    [Header("中央顯示前置動畫 A")]
    [Tooltip("動畫 A 的 GameObject（會在第一次點擊時顯示）")]
    public GameObject animationAObject;
    [Tooltip("播放動畫的 Animator（可選）")]
    public Animator centerAnimator;
    [Tooltip("動畫 A 的名稱（例如：IconAppear）")]
    public string animationAName = "IconAppear";
    [Tooltip("是否啟用前置動畫 A")]
    public bool useAnimationA = true;

    [Header("問過後的提示")]
    [Tooltip("提示文字內容")]
    public string suffixMessage = "物品似乎有新的文字定義...";
    [Tooltip("提示文字淡入時間")]
    public float suffixFadeTime = 0.3f;

    [Header("RedPaper / 條列顯示")]
    [Tooltip("每句停留時間（秒）")]
    public float redPaperStayDuration = 1.0f;
    [Tooltip("每句淡入 / 淡出時間（秒）")]
    public float redPaperLineFadeTime = 0.35f;

    [Header("眼睛動畫")]
    [SerializeField] private EyesController eyesController;

    [Header("觸發冷卻")]
    [Tooltip("中央顯示後幾秒內不能再次觸發")]
    [SerializeField] private float showCooldown = 2f;

    private readonly HashSet<int> _askedItemIds = new HashSet<int>();
    private Coroutine _fadeCo;
    private bool _hasClickedItemOnce = false;
    private float _cooldownUntil = 0f;

    // ── Unity 生命週期 ────────────────────────────────────────

    void Awake()
    {
        Inst = this;
    }

    void OnEnable()
    {
        _hasClickedItemOnce = false;
        _cooldownUntil = 0f;
        if (animationAObject != null) animationAObject.SetActive(false);
        if (centerText)
        {
            centerText.rectTransform.anchoredPosition = Vector2.zero;
            var c = centerText.color; c.a = 0f; centerText.color = c;
            centerText.text = "";
        }
    }

    void OnDisable()
    {
        _hasClickedItemOnce = false;
        _cooldownUntil = 0f;
        if (animationAObject != null) animationAObject.SetActive(false);
    }

    // ── 公開查詢 ───────────────────────────────────────────────

    /// <summary>查詢某物品是否已被問過。</summary>
    public bool HasAsked(int itemId) => _askedItemIds.Contains(itemId);

    // ── 中央顯示（Page4Item 點擊） ────────────────────────────

    /// <summary>
    /// 點擊 Page4Item 後呼叫。
    /// 若物品有設定 linesB，改用條列逐句顯示；否則顯示單段 page2Summary。
    /// </summary>
    public void ShowRightInfo(int id)
    {
        if (Time.unscaledTime < _cooldownUntil) return;

        var data = database ? database.Get(id) : null;
        if (!data) return;

        bool owned = InventoryManager.Inst && InventoryManager.Inst.Owns(id);

        if (!owned)
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(CoShowCenter("尚未获得此道具！", id));
            return;
        }

        // 有 linesB → 條列逐句顯示（先廣播 asked，再觸發動畫）
        var ov = data.GetOverride(filter);
        if (ov != null && ov.linesB != null && ov.linesB.Length > 0)
        {
            RecordAsked(id);
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(CoShowLines(id, ov.linesB));
            return;
        }

        // 無 linesB → 單段文字顯示
        string text = data.For(filter).desc ?? "";
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(CoShowCenter(text, id));
    }

    /// <summary>隱藏中央顯示文字。</summary>
    public void HideRightInfo()
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        if (centerText)
        {
            centerText.rectTransform.anchoredPosition = Vector2.zero;
            var c = centerText.color; c.a = 0f; centerText.color = c;
            centerText.text = "";
        }
    }

    // 記錄已問過並廣播（只在第一次觸發）
    void RecordAsked(int itemID)
    {
        if (_askedItemIds.Contains(itemID)) return;
        _askedItemIds.Add(itemID);
        OnItemAsked?.Invoke(itemID);
    }

    IEnumerator CoShowCenter(string text, int itemID)
    {
        _cooldownUntil = Time.unscaledTime + showCooldown;

        if (eyesController != null)
            yield return StartCoroutine(eyesController.PlayTearsCoroutine());

        if (!_hasClickedItemOnce)
        {
            _hasClickedItemOnce = true;
            if (animationAObject != null) animationAObject.SetActive(true);

            if (useAnimationA && centerAnimator != null && !string.IsNullOrEmpty(animationAName))
            {
                centerAnimator.Play(animationAName, 0, 0f);
                yield return null;
                AnimatorStateInfo stateInfo = centerAnimator.GetCurrentAnimatorStateInfo(0);
                while (stateInfo.IsName(animationAName) && stateInfo.normalizedTime < 1.0f)
                {
                    yield return null;
                    stateInfo = centerAnimator.GetCurrentAnimatorStateInfo(0);
                }
            }
        }

        if (centerText)
        {
            centerText.rectTransform.anchoredPosition = Vector2.zero;
            centerText.text = text;
            var c = centerText.color; c.a = 0f; centerText.color = c;
        }

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.SmoothStep(0f, 1f, t / fadeTime);
            if (centerText) { var c = centerText.color; c.a = a; centerText.color = c; }
            yield return null;
        }
        if (centerText) { var c = centerText.color; c.a = 1f; centerText.color = c; }

        RecordAsked(itemID);
    }

    // 條列顯示（物品的 linesB）
    IEnumerator CoShowLines(int itemID, string[] lines)
    {
        _cooldownUntil = Time.unscaledTime + showCooldown;

        if (eyesController != null)
            yield return StartCoroutine(eyesController.PlayTearsCoroutine());

        if (!_hasClickedItemOnce)
        {
            _hasClickedItemOnce = true;
            if (animationAObject != null) animationAObject.SetActive(true);

            if (useAnimationA && centerAnimator != null && !string.IsNullOrEmpty(animationAName))
            {
                centerAnimator.Play(animationAName, 0, 0f);
                yield return null;
                AnimatorStateInfo stateInfo = centerAnimator.GetCurrentAnimatorStateInfo(0);
                while (stateInfo.IsName(animationAName) && stateInfo.normalizedTime < 1.0f)
                {
                    yield return null;
                    stateInfo = centerAnimator.GetCurrentAnimatorStateInfo(0);
                }
            }
        }

        yield return StartCoroutine(CoPlayLines(lines));
    }

    // ── 中央顯示（RedPaper 點擊） ─────────────────────────────

    /// <summary>點擊 RedPaper 後呼叫，逐句顯示文字。</summary>
    public void ShowRedPaperLines(string[] lines)
    {
        if (lines == null || lines.Length == 0) return;
        if (Time.unscaledTime < _cooldownUntil) return;
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(CoShowRedPaperLines(lines));
    }

    IEnumerator CoShowRedPaperLines(string[] lines)
    {
        _cooldownUntil = Time.unscaledTime + showCooldown;

        if (eyesController != null)
            yield return StartCoroutine(eyesController.PlayRedPaperEnterCoroutine());

        yield return StartCoroutine(CoPlayLines(lines));

        if (eyesController != null)
            yield return StartCoroutine(eyesController.ResumeRedPaperAnimationCoroutine());
    }

    // 共用逐句播放邏輯（供 linesB 與 RedPaper 共用）
    IEnumerator CoPlayLines(string[] lines)
    {
        foreach (string line in lines)
        {
            if (centerText == null) break;
            centerText.text = line;

            float t = 0f;
            while (t < redPaperLineFadeTime)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.SmoothStep(0f, 1f, t / redPaperLineFadeTime);
                var c = centerText.color; c.a = a; centerText.color = c;
                yield return null;
            }
            { var c = centerText.color; c.a = 1f; centerText.color = c; }

            yield return new WaitForSecondsRealtime(redPaperStayDuration);

            t = 0f;
            while (t < redPaperLineFadeTime)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.SmoothStep(1f, 0f, t / redPaperLineFadeTime);
                var c = centerText.color; c.a = a; centerText.color = c;
                yield return null;
            }
            { var c = centerText.color; c.a = 0f; centerText.color = c; }
        }

        if (centerText)
        {
            var c = centerText.color; c.a = 0f; centerText.color = c;
            centerText.text = "";
        }
    }

    // ── 供 BookSubPage 執行的工具 Coroutine ───────────────────

    /// <summary>SuffixText 淡入，由 BookSubPage 啟動。</summary>
    public IEnumerator CoFadeSuffixText(TMP_Text target)
    {
        target.text = suffixMessage;
        var c = target.color; c.a = 0f; target.color = c;
        float t = 0f;
        while (t < suffixFadeTime)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.SmoothStep(0f, 1f, t / suffixFadeTime);
            c = target.color; c.a = a; target.color = c;
            yield return null;
        }
        c = target.color; c.a = 1f; target.color = c;
    }

    // ── 向下相容 ───────────────────────────────────────────────

    public void ShowCenterInfo(int id) => ShowRightInfo(id);
}
