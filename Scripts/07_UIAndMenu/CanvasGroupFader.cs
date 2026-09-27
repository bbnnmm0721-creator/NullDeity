using UnityEngine;
using System.Collections;

public class CanvasGroupFader : MonoBehaviour
{
    public CanvasGroup grp;
    public float defaultDuration = 0.3f;

    void Awake() { if (!grp) grp = GetComponent<CanvasGroup>(); }

    public void FadeIn(float dur = -1f) { StartCoroutine(FadeTo(1f, dur)); }
    public void FadeOut(float dur = -1f) { StartCoroutine(FadeTo(0f, dur)); }

    IEnumerator FadeTo(float target, float dur)
    {
        if (dur <= 0f) dur = defaultDuration;
        float start = grp ? grp.alpha : 1f;
        if (!grp) grp = gameObject.AddComponent<CanvasGroup>();
        float t = 0f;
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            grp.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        grp.alpha = target;

        // 完全透明就順手關掉
        if (Mathf.Approximately(target, 0f)) gameObject.SetActive(false);
    }
}
//用在過場動畫的Fadein Fade out