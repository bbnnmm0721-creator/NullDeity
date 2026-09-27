using System.Collections;
using UnityEngine;

/// <summary>
/// 掛在 page4 GameObject 上。
/// 進入 Page4 時隨機觸發 Blink、LeftRightSee、Scream。
/// 點擊物品時由 BookPageController 呼叫 PlayTearsCoroutine()。
/// 點擊 RedPaper 時由 BookPageController 依序呼叫
///   PlayRedPaperEnterCoroutine()  → 播到第 redPaperPauseFrame 幀後暫停
///   ResumeRedPaperAnimationCoroutine() → 播完剩餘動畫，接回隨機循環
/// </summary>
public class EyesController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator eyesAnimator;

    [Header("隨機觸發間隔（秒）")]
    [SerializeField] private float minInterval = 1f;
    [SerializeField] private float maxInterval = 10f;

    [Header("出現機率（值越大越常出現）")]
    [SerializeField] private float weightBlink = 5f;
    [SerializeField] private float weightLeftRightSee = 3f;
    [SerializeField] private float weightScream = 1f;

    [Header("BG 入場動畫（進入 Page4 時觸發）")]
    [SerializeField] private Animator bgAnimator;
    [SerializeField] private string bgEnterState01 = "PlayPage4Enter01";
    [SerializeField] private Animator hand02Animator;
    [SerializeField] private string bgEnterState02 = "PlayPage4Enter02";

    [Header("RedPaper 動畫")]
    [Tooltip("點擊 RedPaper 後播放的動畫 Trigger 名稱")]
    [SerializeField] private string redPaperEnterTrigger = "PlayRedPaperEnter";
    [Tooltip("暫停的目標幀數（動畫播到此幀後停住，等 RedPaper 內容結束再繼續）")]
    [SerializeField] private int redPaperPauseFrame = 34;

    // ── Trigger / State hashes ──
    private static readonly int HashPlayBlink = Animator.StringToHash("PlayBlink");
    private static readonly int HashPlayLeftRightSee = Animator.StringToHash("PlayLeftRightSee");
    private static readonly int HashPlayScream = Animator.StringToHash("PlayScream");
    private static readonly int HashPlayTears = Animator.StringToHash("PlayTears");
    private static readonly int StateEyesStandby = Animator.StringToHash("EyesStandby");

    private int _hashBgEnter01;
    private int _hashBgEnter02;
    private int _hashRedPaperEnter;

    private Coroutine _randomCoroutine;

    void Awake()
    {
        _hashBgEnter01 = Animator.StringToHash(bgEnterState01);
        _hashBgEnter02 = Animator.StringToHash(bgEnterState02);
        _hashRedPaperEnter = Animator.StringToHash(redPaperEnterTrigger);
    }

    void OnEnable()
    {
        if (eyesAnimator == null)
        {
            Debug.LogWarning("[EyesController] 缺少 eyesAnimator");
            return;
        }

        if (bgAnimator != null) bgAnimator.SetTrigger(_hashBgEnter01);
        if (hand02Animator != null) hand02Animator.SetTrigger(_hashBgEnter02);

        StopRandomCoroutine();
        ResetAllTriggers();
        _randomCoroutine = StartCoroutine(RandomTriggerRoutine());
    }

    void OnDisable()
    {
        // 確保 speed 不會留在 0
        if (eyesAnimator != null) eyesAnimator.speed = 1f;
        StopRandomCoroutine();
    }

    // ── 內部工具 ──────────────────────────────────────────────

    private void ResetAllTriggers()
    {
        eyesAnimator.ResetTrigger(HashPlayBlink);
        eyesAnimator.ResetTrigger(HashPlayLeftRightSee);
        eyesAnimator.ResetTrigger(HashPlayScream);
        eyesAnimator.ResetTrigger(HashPlayTears);
        eyesAnimator.ResetTrigger(_hashRedPaperEnter);
    }

    private void StopRandomCoroutine()
    {
        if (_randomCoroutine != null)
        {
            StopCoroutine(_randomCoroutine);
            _randomCoroutine = null;
        }
    }

    private IEnumerator RandomTriggerRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
            eyesAnimator.SetTrigger(GetWeightedRandomTrigger());
            yield return null;
            yield return new WaitUntil(IsInStandby);
        }
    }

    /// <summary>依權重回傳一個隨機 Trigger hash。</summary>
    private int GetWeightedRandomTrigger()
    {
        float total = weightBlink + weightLeftRightSee + weightScream;
        float roll = Random.Range(0f, total);

        if (roll < weightBlink)
            return HashPlayBlink;

        if (roll < weightBlink + weightLeftRightSee)
            return HashPlayLeftRightSee;

        return HashPlayScream;
    }

    /// <summary>確認目前在 EyesStandby 且不在 Transition 中。</summary>
    private bool IsInStandby()
    {
        return !eyesAnimator.IsInTransition(0)
            && eyesAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == StateEyesStandby;
    }

    // ── 公開 Coroutine ──────────────────────────────────────

    /// <summary>
    /// 播放 Tears 動畫，結束後自動恢復隨機循環。
    /// 由 BookPageController 在點擊物品前以 yield return StartCoroutine(...) 呼叫。
    /// </summary>
    public IEnumerator PlayTearsCoroutine()
    {
        if (eyesAnimator == null) yield break;

        StopRandomCoroutine();
        ResetAllTriggers();

        eyesAnimator.SetTrigger(HashPlayTears);
        yield return null;
        yield return new WaitUntil(IsInStandby);

        _randomCoroutine = StartCoroutine(RandomTriggerRoutine());
    }

    /// <summary>
    /// RedPaper 入場：停止隨機循環，播放動畫直到第 redPaperPauseFrame 幀後暫停。
    /// 由 BookPageController 在開始逐句顯示前以 yield return StartCoroutine(...) 呼叫。
    /// </summary>
    public IEnumerator PlayRedPaperEnterCoroutine()
    {
        if (eyesAnimator == null) yield break;

        StopRandomCoroutine();
        ResetAllTriggers();

        eyesAnimator.speed = 1f;
        eyesAnimator.SetTrigger(_hashRedPaperEnter);

        // 等 Animator 確實進入 RedPaper 動畫狀態
        yield return null;
        yield return new WaitUntil(() =>
        {
            var info = eyesAnimator.GetCurrentAnimatorStateInfo(0);
            return !eyesAnimator.IsInTransition(0) && info.shortNameHash != StateEyesStandby;
        });

        // 計算第 redPaperPauseFrame 幀的 normalized time
        float pauseNormalizedTime = GetPauseNormalizedTime();

        // 等到動畫播到目標幀
        yield return new WaitUntil(() =>
        {
            var info = eyesAnimator.GetCurrentAnimatorStateInfo(0);
            return info.normalizedTime >= pauseNormalizedTime;
        });

        // 暫停在此幀
        eyesAnimator.speed = 0f;
    }

    /// <summary>
    /// RedPaper 收尾：恢復動畫速度，播完剩餘動畫，接回隨機循環。
    /// 由 BookPageController 在最後一句結束後以 yield return StartCoroutine(...) 呼叫。
    /// </summary>
    public IEnumerator ResumeRedPaperAnimationCoroutine()
    {
        if (eyesAnimator == null) yield break;

        eyesAnimator.speed = 1f;
        yield return null;
        yield return new WaitUntil(IsInStandby);

        _randomCoroutine = StartCoroutine(RandomTriggerRoutine());
    }

    /// <summary>根據動畫 Clip 的 frameRate 計算暫停目標的 normalizedTime。</summary>
    private float GetPauseNormalizedTime()
    {
        var clipInfos = eyesAnimator.GetCurrentAnimatorClipInfo(0);
        if (clipInfos.Length == 0)
        {
            Debug.LogWarning("[EyesController] 無法取得 ClipInfo，使用預設 normalizedTime");
            return 0.5f;
        }

        var clip = clipInfos[0].clip;
        float totalFrames = clip.length * clip.frameRate;
        return Mathf.Clamp01(redPaperPauseFrame / totalFrames);
    }
}
