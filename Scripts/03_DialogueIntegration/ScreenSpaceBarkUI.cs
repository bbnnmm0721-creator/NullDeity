using System.Collections;
using TMPro;
using UnityEngine;
using PixelCrushers.DialogueSystem;

/// <summary>
/// 固定在螢幕空間的 Bark UI，不隨角色位置移動。
/// 等待外部呼叫 Hide()（玩家按 A 後由 WorldItem 觸發）才淡出。
/// </summary>
public class ScreenSpaceBarkUI : AbstractBarkUI
{
    public static ScreenSpaceBarkUI Instance { get; private set; }

    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI speakerText; // 說話角色名稱
    public TextMeshProUGUI barkText;    // 台詞內容

    [Header("設定")]
    public float fadeTime = 0.25f;

    private Coroutine _displayCoroutine;
    private bool _isPlaying;
    private bool _forceHide;

    public override bool isPlaying => _isPlaying;

    /// <summary>顯示說話者名稱與台詞，等待 Hide() 訊號後才淡出。</summary>
    public override void Bark(Subtitle subtitle)
    {
        if (_displayCoroutine != null)
            StopCoroutine(_displayCoroutine);

        string speaker = subtitle.speakerInfo != null ? subtitle.speakerInfo.Name : string.Empty;
        string text = subtitle.formattedText.text;

        _forceHide = false;
        _displayCoroutine = StartCoroutine(DisplayRoutine(speaker, text));
    }

    /// <summary>通知開始淡出（HideAllBarkUIs 或 DismissBark 呼叫）。</summary>
    public override void Hide()
    {
        if (_displayCoroutine != null)
            _forceHide = true;  // DisplayRoutine 偵測到後自行淡出並結束
        else
        {
            if (canvasGroup) canvasGroup.alpha = 0f;
            _isPlaying = false;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) return; // 靜默忽略複製品
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        if (canvasGroup) canvasGroup.alpha = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    IEnumerator DisplayRoutine(string speaker, string text)
    {
        _isPlaying = true;
        _forceHide = false;

        if (speakerText) speakerText.text = speaker;
        if (barkText) barkText.text = text;

        yield return Fade(0f, 1f);

        while (!_forceHide) yield return null; // 等玩家按 A → Hide() → _forceHide = true

        yield return Fade(canvasGroup ? canvasGroup.alpha : 1f, 0f);

        _isPlaying = false;
        _displayCoroutine = null;
    }

    IEnumerator Fade(float from, float to)
    {
        if (!canvasGroup) yield break;
        float elapsed = 0f;
        canvasGroup.alpha = from;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeTime);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}

/// <summary>
/// BarkController 用的空實作，防止 null/destroyed 崩潰。
/// 實際顯示由 NPCBarkDisplayer 透過 OnBarkLine 轉交 ScreenSpaceBarkUI。
/// </summary>
public class NullBarkUI : AbstractBarkUI
{
    public override bool isPlaying => false;
    public override void Bark(Subtitle subtitle) { }
    public override void Hide() { }
}
