using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using UnityEngine.SceneManagement;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("对话设置")]
    public float dialogueTransitionTime = 1f;

    [Header("场景切换设置")]
    [Tooltip("异步加载开始时的淡出时间")]
    public float sceneTransitionFadeOutTime = 1f;

    [Tooltip("加载到90%时的淡入时间")]
    public float sceneTransitionFadeInTime = 1.5f;

    [Header("调试")]
    public bool enableDebugLog = false;

    private class Track
    {
        public AudioSource source;
        public SceneMusicConfig.MusicTrack config;
        public string id;
        public bool isInDialogue;
        public Coroutine fadeCoroutine;
        public MonoBehaviour owner;

        public Track(MonoBehaviour owner, SceneMusicConfig.MusicTrack config)
        {
            this.owner = owner;
            this.config = config;
            this.id = config.GetID();

            GameObject obj = new GameObject($"Track_{id}");
            obj.transform.SetParent(owner.transform);

            source = obj.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.clip = config.musicClip;
            source.volume = 0f;
        }

        public void Play(float fadeTime)
        {
            if (!source.isPlaying)
            {
                source.volume = 0f;
                source.Play();
            }
            FadeTo(GetTargetVolume(), fadeTime);
        }

        public void Stop(float fadeTime)
        {
            FadeTo(0f, fadeTime, true);
        }

        public void SetDialogue(bool inDialogue, float time)
        {
            isInDialogue = inDialogue;
            FadeTo(GetTargetVolume(), time);
        }

        public void UpdateVolume(float newVolume, float time)
        {
            config.sceneVolume = newVolume;
            FadeTo(GetTargetVolume(), time);
        }

        public float GetTargetVolume()
        {
            return isInDialogue ? config.GetDialogueVolume() : config.sceneVolume;
        }

        public void FadeTo(float target, float duration, bool stopAtEnd = false)
        {
            if (fadeCoroutine != null)
                owner.StopCoroutine(fadeCoroutine);
            fadeCoroutine = owner.StartCoroutine(FadeCoroutine(target, duration, stopAtEnd));
        }

        IEnumerator FadeCoroutine(float target, float duration, bool stopAtEnd)
        {
            float start = source.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            source.volume = target;
            if (stopAtEnd && target == 0f)
                source.Stop();

            fadeCoroutine = null;
        }

        public void Destroy()
        {
            if (source != null && source.gameObject != null)
                Object.Destroy(source.gameObject);
        }
    }

    private List<Track> tracks = new List<Track>();
    private List<Track> fadingOutTracks = new List<Track>();
    private bool isInDialogue = false;
    private bool dialogueEventsRegistered = false;
    private string pendingSceneName = "";
    private Coroutine playOnceCoroutine = null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (enableDebugLog)
            Debug.Log("[BGMManager] 初始化完成");
    }

    void OnEnable()
    {
        RegisterDialogueEvents();
    }

    void Start()
    {
        RegisterDialogueEvents();
    }

    void OnDisable()
    {
        UnregisterDialogueEvents();
    }

    void RegisterDialogueEvents()
    {
        if (dialogueEventsRegistered) return;

        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted += OnConversationStart;
            DialogueManager.instance.conversationEnded += OnConversationEnd;
            dialogueEventsRegistered = true;

            if (enableDebugLog)
                Debug.Log("[BGMManager] 对话事件已注册");
        }
    }

    void UnregisterDialogueEvents()
    {
        if (!dialogueEventsRegistered) return;

        if (DialogueManager.instance != null)
        {
            DialogueManager.instance.conversationStarted -= OnConversationStart;
            DialogueManager.instance.conversationEnded -= OnConversationEnd;
        }

        dialogueEventsRegistered = false;
    }

    public void OnSceneTransitionStart(string targetSceneName)
    {
        pendingSceneName = targetSceneName;

        if (enableDebugLog)
            Debug.Log($"[BGMManager] 场景过渡开始 → {targetSceneName}，淡出当前音乐");

        fadingOutTracks.Clear();
        fadingOutTracks.AddRange(tracks);

        foreach (var track in fadingOutTracks)
        {
            track.FadeTo(0f, sceneTransitionFadeOutTime, false);
        }

        tracks.Clear();
    }

    public void OnSceneLoadedAt90Percent(string sceneName)
    {
        if (enableDebugLog)
            Debug.Log($"[BGMManager] 场景加载到90%: {sceneName}，准备淡入新音乐");

        StartCoroutine(FadeInNewSceneMusic(sceneName));
    }

    IEnumerator FadeInNewSceneMusic(string sceneName)
    {
        yield return new WaitForSeconds(0.1f);

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (enableDebugLog)
                Debug.LogWarning($"[BGMManager] 场景 {sceneName} 尚未加载完成");

            pendingSceneName = "";
            yield break;
        }

        SceneMusicConfig config = FindSceneMusicConfigInScene(scene);
        if (config != null)
        {
            ApplySceneMusicWithFadeIn(config);
        }
        else
        {
            if (enableDebugLog)
                Debug.Log($"[BGMManager] 场景 {sceneName} 没有音乐配置");
        }

        foreach (var track in fadingOutTracks)
        {
            StartCoroutine(DestroyAfter(track, sceneTransitionFadeOutTime + 0.5f));
        }
        fadingOutTracks.Clear();

        pendingSceneName = "";
    }

    SceneMusicConfig FindSceneMusicConfigInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            SceneMusicConfig config = root.GetComponentInChildren<SceneMusicConfig>(true);
            if (config != null)
                return config;
        }
        return null;
    }

    void ApplySceneMusicWithFadeIn(SceneMusicConfig config)
    {
        if (config.musicTracks == null || config.musicTracks.Length == 0)
        {
            if (enableDebugLog)
                Debug.Log("[BGMManager] 新场景没有音乐配置");
            return;
        }

        if (enableDebugLog)
        {
            Debug.Log($"[BGMManager] 应用 {config.musicTracks.Length} 个音乐轨道（从90%开始淡入）");
            Debug.Log($"[BGMManager] 当前 fadingOutTracks 数量: {fadingOutTracks.Count}");
            foreach (var t in fadingOutTracks)
                Debug.Log($"[BGMManager]   - fadingOut: {t.id} (clip: {t.config.musicClip?.name})");
        }

        foreach (var trackConfig in config.musicTracks)
        {
            if (trackConfig.musicClip == null) continue;

            string newId = trackConfig.GetID();

            if (enableDebugLog)
                Debug.Log($"[BGMManager] 处理新轨道: ID='{newId}', Clip='{trackConfig.musicClip.name}'");

            Track existingFading = FindTrackByIdOrClip(fadingOutTracks, newId, trackConfig.musicClip);

            if (existingFading != null)
            {
                if (enableDebugLog)
                    Debug.Log($"[BGMManager] ✓ 找到匹配的淡出音乐，恢复: {existingFading.id} → {newId}");

                existingFading.id = newId;
                existingFading.UpdateVolume(trackConfig.sceneVolume, sceneTransitionFadeInTime);
                existingFading.config = trackConfig;
                tracks.Add(existingFading);
                fadingOutTracks.Remove(existingFading);
            }
            else
            {
                if (enableDebugLog)
                    Debug.Log($"[BGMManager] ✗ 未找到匹配，播放新音乐: {newId}");

                Track newTrack = new Track(this, trackConfig);
                newTrack.SetDialogue(isInDialogue, 0f);
                newTrack.Play(sceneTransitionFadeInTime);
                tracks.Add(newTrack);
            }
        }
    }

    Track FindTrackByIdOrClip(List<Track> trackList, string id, AudioClip clip)
    {
        if (string.IsNullOrEmpty(id) && clip == null)
            return null;

        foreach (var track in trackList)
        {
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(track.id))
            {
                if (string.Equals(track.id, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (enableDebugLog)
                        Debug.Log($"[BGMManager]   → ID 匹配: '{track.id}' == '{id}'");
                    return track;
                }
            }

            if (clip != null && track.config.musicClip != null)
            {
                if (string.Equals(track.config.musicClip.name, clip.name, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (enableDebugLog)
                        Debug.Log($"[BGMManager]   → Clip 名称匹配: '{track.config.musicClip.name}' == '{clip.name}'");
                    return track;
                }
            }
        }

        return null;
    }

    public void OnSceneLoaded(SceneMusicConfig config)
    {
        if (config == null) return;

        if (enableDebugLog)
            Debug.Log($"[BGMManager] OnSceneLoaded 被调用，pendingSceneName = '{pendingSceneName}'");

        if (string.IsNullOrEmpty(pendingSceneName))
        {
            if (enableDebugLog)
                Debug.Log("[BGMManager] 非场景过渡加载（游戏开始），直接应用音乐");

            ApplySceneMusicDirectly(config);
        }
        else
        {
            if (enableDebugLog)
                Debug.Log("[BGMManager] 场景过渡中，等待90%信号");
        }
    }

    void ApplySceneMusicDirectly(SceneMusicConfig config)
    {
        if (config.musicTracks == null || config.musicTracks.Length == 0)
        {
            if (enableDebugLog)
                Debug.Log("[BGMManager] 场景没有音乐配置");
            return;
        }

        if (enableDebugLog)
            Debug.Log($"[BGMManager] 直接应用 {config.musicTracks.Length} 个音乐轨道");

        foreach (var trackConfig in config.musicTracks)
        {
            if (trackConfig.musicClip == null) continue;

            string id = trackConfig.GetID();
            Track existing = FindTrackByIdOrClip(tracks, id, trackConfig.musicClip);

            if (existing != null)
            {
                if (enableDebugLog)
                    Debug.Log($"[BGMManager] 更新音乐 {id}: {existing.config.sceneVolume:F2} → {trackConfig.sceneVolume:F2}");

                existing.id = id;
                existing.UpdateVolume(trackConfig.sceneVolume, trackConfig.fadeInTime * 0.5f);
                existing.config = trackConfig;
            }
            else
            {
                if (enableDebugLog)
                    Debug.Log($"[BGMManager] 播放新音乐 {id}");

                Track newTrack = new Track(this, trackConfig);
                newTrack.SetDialogue(isInDialogue, 0f);
                newTrack.Play(trackConfig.fadeInTime);
                tracks.Add(newTrack);
            }
        }
    }

    IEnumerator DestroyAfter(Track track, float delay)
    {
        yield return new WaitForSeconds(delay);
        track.Destroy();
    }

    void StopAll(float fadeTime)
    {
        foreach (var track in tracks)
        {
            track.Stop(fadeTime);
            StartCoroutine(DestroyAfter(track, fadeTime + 0.5f));
        }
        tracks.Clear();
    }

    void OnConversationStart(Transform actor)
    {
        isInDialogue = true;

        if (enableDebugLog)
            Debug.Log("[BGMManager] 对话开始，调整音量");

        foreach (var track in tracks)
        {
            track.SetDialogue(true, dialogueTransitionTime);

            if (enableDebugLog)
                Debug.Log($"  {track.id}: {track.config.sceneVolume:F2} → {track.config.GetDialogueVolume():F2} ({track.config.dialogueMode})");
        }
    }

    void OnConversationEnd(Transform actor)
    {
        isInDialogue = false;

        if (enableDebugLog)
            Debug.Log("[BGMManager] 对话结束，恢复音量");

        foreach (var track in tracks)
        {
            track.SetDialogue(false, dialogueTransitionTime);

            if (enableDebugLog)
                Debug.Log($"  {track.id}: 恢复至 {track.config.sceneVolume:F2}");
        }
    }

    /// <summary>
    /// 循環播放音樂（透過 Lua PlayBGM 呼叫）。
    /// </summary>
    public void PlayMusic(AudioClip clip, float fadeTime)
    {
        if (clip == null)
        {
            Debug.LogWarning("[BGMManager] PlayMusic: clip 为空");
            return;
        }

        if (enableDebugLog)
            Debug.Log($"[BGMManager] Lua 播放音乐: {clip.name}");

        var tempConfig = new SceneMusicConfig.MusicTrack
        {
            musicClip = clip,
            musicID = clip.name,
            sceneVolume = 0.7f,
            dialogueMode = SceneMusicConfig.MusicTrack.DialogueVolumeMode.ReduceToValue,
            dialogueVolume = 0.3f,
            fadeInTime = fadeTime > 0 ? fadeTime : 2f,
            fadeOutTime = 1.5f
        };

        Track existing = tracks.Find(t => t.id == clip.name);
        if (existing != null)
        {
            existing.UpdateVolume(tempConfig.sceneVolume, fadeTime);
        }
        else
        {
            Track newTrack = new Track(this, tempConfig);
            newTrack.SetDialogue(isInDialogue, 0f);
            newTrack.Play(fadeTime);
            tracks.Add(newTrack);
        }
    }

    /// <summary>
    /// 播放一次（不循環），播完自動淡出並接續播放下一首。
    /// </summary>
    public void PlayMusicOnce(AudioClip clip, float fadeIn, string nextClipPath, float fadeOut)
    {
        if (clip == null)
        {
            Debug.LogWarning("[BGMManager] PlayMusicOnce: clip 为空");
            return;
        }

        if (playOnceCoroutine != null)
            StopCoroutine(playOnceCoroutine);

        playOnceCoroutine = StartCoroutine(PlayOnceCoroutine(clip, fadeIn, nextClipPath, fadeOut));
    }

    private IEnumerator PlayOnceCoroutine(AudioClip clip, float fadeIn, string nextClipPath, float fadeOut)
    {
        // 停止目前所有循環音樂
        StopAll(fadeIn);

        // 建立一次性不循環的 AudioSource
        var obj = new GameObject($"TempOnce_{clip.name}");
        obj.transform.SetParent(transform);
        var src = obj.AddComponent<AudioSource>();
        src.loop = false;
        src.playOnAwake = false;
        src.clip = clip;
        src.volume = 0f;
        src.Play();

        // 淡入
        const float targetVolume = 0.7f;
        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.deltaTime;
            src.volume = Mathf.Lerp(0f, targetVolume, elapsed / fadeIn);
            yield return null;
        }
        src.volume = targetVolume;

        // 等到快結束前 fadeOut 秒開始淡出
        float waitTime = clip.length - fadeIn - fadeOut;
        if (waitTime > 0f)
            yield return new WaitForSeconds(waitTime);

        // 淡出
        elapsed = 0f;
        float startVol = src.volume;
        while (elapsed < fadeOut)
        {
            elapsed += Time.deltaTime;
            src.volume = Mathf.Lerp(startVol, 0f, elapsed / fadeOut);
            yield return null;
        }
        src.Stop();
        Destroy(obj);

        // 接續播放下一首（如果有指定）
        if (!string.IsNullOrEmpty(nextClipPath))
        {
            AudioClip nextClip = Resources.Load<AudioClip>(nextClipPath);
            if (nextClip != null)
                PlayMusic(nextClip, fadeOut);
            else
                Debug.LogWarning($"[BGMManager] PlayMusicOnce: 找不到下一首 {nextClipPath}");
        }

        playOnceCoroutine = null;

        if (enableDebugLog)
            Debug.Log($"[BGMManager] PlayMusicOnce 完成: {clip.name} → {nextClipPath}");
    }

    public void StopMusic(float fadeTime)
    {
        if (enableDebugLog)
            Debug.Log("[BGMManager] Lua 停止所有音乐");

        StopAll(fadeTime > 0 ? fadeTime : 1.5f);
    }

    public void FadeOut(float duration)
    {
        if (enableDebugLog)
            Debug.Log($"[BGMManager] Lua 淡出所有音乐 {duration}s");

        foreach (var track in tracks)
        {
            track.FadeTo(0f, duration, false);
        }
    }

    public void FadeIn(float duration)
    {
        if (enableDebugLog)
            Debug.Log($"[BGMManager] Lua 淡入所有音乐 {duration}s");

        foreach (var track in tracks)
        {
            if (!track.source.isPlaying)
                track.source.Play();

            track.FadeTo(track.GetTargetVolume(), duration);
        }
    }

    public void SetDefaultVolume(float volume)
    {
        if (enableDebugLog)
            Debug.Log($"[BGMManager] Lua 设置所有音乐音量 {volume}");

        foreach (var track in tracks)
        {
            track.UpdateVolume(volume, 0.5f);
        }
    }
}
