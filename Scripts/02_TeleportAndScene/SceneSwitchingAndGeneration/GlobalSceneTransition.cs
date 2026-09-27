using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GlobalSceneTransition : MonoBehaviour
{
    public static GlobalSceneTransition Inst { get; private set; }

    [Header("常駐黑幕")]
    public CanvasGroup screenFader;
    public float fadeTime = 0.25f;
    public Slider progressBar;

    Image faderImage;
    Coroutine activeFade;
    static bool busy;

    public static bool IsBusy => busy;

    const float FadeWaitTimeout = 15f;

    void Awake()
    {
        if (Inst && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        EnsureFader();

        if (faderImage == null && screenFader != null)
            faderImage = screenFader.GetComponent<Image>();

        screenFader.alpha = 0f;
        screenFader.blocksRaycasts = false;
        screenFader.interactable = false;
    }

    void EnsureFader()
    {
        if (screenFader) return;

        var goCanvas = new GameObject("ScreenFaderCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        goCanvas.transform.SetParent(transform, false);
        var canvas = goCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        var goImg = new GameObject("Black", typeof(RectTransform),
            typeof(Image), typeof(CanvasGroup));
        goImg.transform.SetParent(goCanvas.transform, false);
        var rt = (RectTransform)goImg.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var img = goImg.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;

        screenFader = goImg.GetComponent<CanvasGroup>();
        faderImage = img;
    }

    /// <summary>一般過場：淡黑 → 載入 → 淡出。</summary>
    public static void Go(string sceneName, string spawnId)
    {
        if (!Inst) { Debug.LogError("[GST] 沒有 GlobalSceneTransition 物件"); return; }
        if (busy) { Debug.LogWarning("[GST] 忙碌中，忽略重複呼叫"); return; }
        Inst.StartCoroutine(Inst.CoGo(sceneName, spawnId));
    }

    /// <summary>單純淡到指定顏色，不切換場景。若有其他 Fade 正在執行會先停止。</summary>
    public static void FadeToColor(Color color, float duration)
    {
        if (!Inst) return;
        if (Inst.faderImage) Inst.faderImage.color = color;
        if (Inst.activeFade != null) Inst.StopCoroutine(Inst.activeFade);
        Inst.activeFade = Inst.StartCoroutine(Inst.Fade(1f, duration));
    }

    /// <summary>從目前顏色淡回透明，不切換場景。若有其他 Fade 正在執行會先停止。</summary>
    public static void FadeFromColor(float duration)
    {
        if (!Inst) return;
        if (Inst.activeFade != null) Inst.StopCoroutine(Inst.activeFade);
        Inst.activeFade = Inst.StartCoroutine(Inst.Fade(0f, duration));
    }

    /// <summary>切換場景，保持螢幕遮蔽直到 Fade 完成且相機 zoom 結束。不顯示進度條，不自動淡出。</summary>
    public static void GoStayBlack(string sceneName, string spawnId)
    {
        if (!Inst) { Debug.LogError("[GST] 沒有 GlobalSceneTransition 物件"); return; }
        if (busy) { Debug.LogWarning("[GST] 忙碌中，忽略重複呼叫"); return; }
        Inst.StartCoroutine(Inst.CoGoStayBlack(sceneName, spawnId));
    }

    IEnumerator CoGo(string sceneName, string spawnId)
    {
        PlayerMovement playerMovement = null;
        try
        {
            busy = true;

            yield return Fade(1f, fadeTime);

            playerMovement = FindObjectOfType<PlayerMovement>();
            if (playerMovement)
            {
                playerMovement.isMovementLocked = true;
                Debug.Log("[GST] 🔒 黑幕後鎖定舊場景玩家");
            }

            if (BGMManager.Instance != null)
                BGMManager.Instance.OnSceneTransitionStart(sceneName);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                if (progressBar) progressBar.value = op.progress / 0.9f;
                yield return null;
            }
            if (progressBar) progressBar.value = 1f;

            if (BGMManager.Instance != null)
                BGMManager.Instance.OnSceneLoadedAt90Percent(sceneName);

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            var newScene = SceneManager.GetSceneByName(sceneName);
            SceneManager.SetActiveScene(newScene);

            yield return null;
            DynamicGI.UpdateEnvironment();
            Debug.Log($"[GST] ✓ 已設置 Active Scene: {sceneName}");

            var player = PlayerMovement.Instance != null
                ? PlayerMovement.Instance.transform
                : FindInSceneByTag(newScene, "Player");

            var spawn = FindSpawnIn(newScene, spawnId);

            if (player && spawn)
            {
                var cc = player.GetComponent<CharacterController>();
                bool ccWasEnabled = cc && cc.enabled;
                if (ccWasEnabled) cc.enabled = false;

                player.position = spawn.transform.position;
                var s = player.localScale;
                s.x = Mathf.Abs(s.x) * (spawn.faceRight ? 1 : -1);
                player.localScale = s;

                if (ccWasEnabled) cc.enabled = true;

                playerMovement = player.GetComponent<PlayerMovement>();
                if (playerMovement)
                {
                    playerMovement.ForceResetAnimation();
                    playerMovement.isMovementLocked = true;
                    Debug.Log("[GST] 🔒 已重置動畫並鎖定新場景玩家");
                }
            }

            Transform cam = Camera.main ? Camera.main.transform : null;
            if (!cam)
            {
                cam = newScene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Camera>(true))
                    .Where(c => c.enabled).Select(c => c.transform).FirstOrDefault();
            }

            var ssc = FindObjectOfType<SideScrollCamera>(true);
            if (ssc && player)
            {
                ssc.target = player;
                ForceSnapSideScroll(ssc, player);
            }
            else if (cam && player)
            {
                var z = cam.position.z;
                cam.position = new Vector3(player.position.x, player.position.y, z);
            }

            float t = 0f;
            while (!(player && (ssc || cam)) && t < 1f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;

            if (spawn && spawn.waitForCameraZoom && ssc)
                yield return WaitForCameraZoom(ssc, spawn.maxWaitTime);

            yield return Fade(0f, fadeTime);
            yield return new WaitForSeconds(0.1f);

            Debug.Log("[GST] 場景切換完成");
        }
        finally
        {
            busy = false;
            if (playerMovement)
            {
                playerMovement.isMovementLocked = false;
                Debug.Log("[GST] 🔓 解鎖玩家移動");
            }
            InteractiveScenePortal.ResetLoadingState();
            SceneLoader.ResetLoadingState();
            Debug.Log("[GST] busy 狀態已重置");
        }
    }

    IEnumerator CoGoStayBlack(string sceneName, string spawnId)
    {
        PlayerMovement playerMovement = null;
        try
        {
            busy = true;

            if (screenFader)
            {
                screenFader.blocksRaycasts = true;
                screenFader.interactable = false;
            }

            if (progressBar) progressBar.gameObject.SetActive(false);

            playerMovement = FindObjectOfType<PlayerMovement>();
            if (playerMovement) playerMovement.isMovementLocked = true;

            if (BGMManager.Instance != null)
                BGMManager.Instance.OnSceneTransitionStart(sceneName);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
                yield return null;

            if (BGMManager.Instance != null)
                BGMManager.Instance.OnSceneLoadedAt90Percent(sceneName);

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            var newScene = SceneManager.GetSceneByName(sceneName);
            SceneManager.SetActiveScene(newScene);

            yield return null;
            DynamicGI.UpdateEnvironment();

            var player = PlayerMovement.Instance != null
                ? PlayerMovement.Instance.transform
                : FindInSceneByTag(newScene, "Player");

            var spawn = FindSpawnIn(newScene, spawnId);

            if (player && spawn)
            {
                var cc = player.GetComponent<CharacterController>();
                bool ccWasEnabled = cc && cc.enabled;
                if (ccWasEnabled) cc.enabled = false;

                player.position = spawn.transform.position;
                var s = player.localScale;
                s.x = Mathf.Abs(s.x) * (spawn.faceRight ? 1 : -1);
                player.localScale = s;

                if (ccWasEnabled) cc.enabled = true;

                playerMovement = player.GetComponent<PlayerMovement>();
                if (playerMovement)
                {
                    playerMovement.ForceResetAnimation();
                    playerMovement.isMovementLocked = true;
                }
            }

            var ssc = FindObjectOfType<SideScrollCamera>(true);
            if (ssc && player)
            {
                ssc.target = player;
                ForceSnapSideScroll(ssc, player);
            }

            // ★ 計算相機 zoom 的截止時間（場景載入完就開始計時）
            float camZoomDeadline = Time.unscaledTime;
            if (spawn != null && spawn.waitForCameraZoom && ssc)
                camZoomDeadline = Time.unscaledTime + Mathf.Min(ssc.zoomDuration, spawn.maxWaitTime);

            // ★ 同時等待：Fade 到不透明 AND 相機 zoom 結束，兩個都滿足才繼續
            float waitDeadline = Time.unscaledTime + FadeWaitTimeout;
            while (Time.unscaledTime < waitDeadline)
            {
                bool fadeReady = screenFader == null || screenFader.alpha >= 0.99f;
                bool camReady = Time.unscaledTime >= camZoomDeadline;
                if (fadeReady && camReady) break;
                yield return null;
            }

            if (screenFader != null && screenFader.alpha < 0.99f)
            {
                Debug.LogWarning("[GST] ScreenFade 等待超時，強制設為不透明");
                screenFader.alpha = 1f;
            }

            // ★ 不呼叫 Fade(0f)，由新場景的 DelayedDialogueTrigger 負責淡出
        }
        finally
        {
            busy = false;
            if (playerMovement) playerMovement.isMovementLocked = false;
            InteractiveScenePortal.ResetLoadingState();
            SceneLoader.ResetLoadingState();
            Debug.Log("[GST] CoGoStayBlack 完成，busy 已重置");

        }
    }

    IEnumerator WaitForCameraZoom(SideScrollCamera ssc, float maxWaitTime)
    {
        float waitStartTime = Time.unscaledTime;
        float endTime = waitStartTime + Mathf.Min(ssc.zoomDuration, maxWaitTime);

        while (Time.unscaledTime < endTime)
        {
            if (Time.unscaledTime - waitStartTime >= maxWaitTime) break;
            yield return null;
        }
    }

    void ForceSnapSideScroll(SideScrollCamera ssc, Transform player)
    {
        Vector3 desired = new(
            player.position.x + ssc.offset.x,
            player.position.y + ssc.offset.y + ssc.normalOffsetY,
            player.position.z + ssc.offset.z + ssc.normalOffsetZ
        );
        desired.x = Mathf.Clamp(desired.x, ssc.minX, ssc.maxX);
        desired.y = Mathf.Clamp(desired.y, ssc.minY, ssc.maxY);
        desired.z = Mathf.Clamp(desired.z, ssc.minZ, ssc.maxZ);
        ssc.transform.position = desired;
    }

    IEnumerator Fade(float to, float time)
    {
        if (!screenFader) yield break;
        screenFader.blocksRaycasts = true;

        float from = screenFader.alpha;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            screenFader.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        screenFader.alpha = to;
        screenFader.blocksRaycasts = (to > 0.99f);
    }

    SceneSpawnPoint FindSpawnIn(Scene scene, string spawnId)
    {
        var spawns = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<SceneSpawnPoint>(true))
            .ToArray();
        if (spawns.Length == 0) return null;

        var s = spawns.FirstOrDefault(x =>
            string.Equals(x.id, spawnId, System.StringComparison.OrdinalIgnoreCase));
        return s ? s : spawns[0];
    }

    Transform FindInSceneByTag(Scene scene, string tag)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindWithTagRecursive(root.transform, tag);
            if (t) return t;
        }
        return null;
    }

    Transform FindWithTagRecursive(Transform parent, string tag)
    {
        if (parent.CompareTag(tag)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindWithTagRecursive(parent.GetChild(i), tag);
            if (r) return r;
        }
        return null;
    }
}
