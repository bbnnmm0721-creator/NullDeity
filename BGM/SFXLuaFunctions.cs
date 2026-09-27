using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using Language.Lua;

/// <summary>
/// 掛在 persistent 場景的 GameObject 上（與 BGMLuaFunctions 同一個即可）。
/// </summary>
public class SFXLuaFunctions : MonoBehaviour
{
    [Header("SFX 設定")]
    [Tooltip("SFX 同時最多可疊加播放的頻道數")]
    [SerializeField] private int channelCount = 4;

    private AudioSource[] channels;
    private Coroutine[] fadeCoroutines;

    void Awake()
    {
        InitChannels();
    }

    void OnEnable()
    {
        Lua.RegisterFunction("PlaySFX", this, SymbolExtensions.GetMethodInfo(() => PlaySFX(string.Empty, (double)0, (double)0)));
        Lua.RegisterFunction("StopSFX", this, SymbolExtensions.GetMethodInfo(() => StopSFX(string.Empty, (double)0)));
        Lua.RegisterFunction("StopAllSFX", this, SymbolExtensions.GetMethodInfo(() => StopAllSFX((double)0)));
    }

    void OnDisable()
    {
        Lua.UnregisterFunction("PlaySFX");
        Lua.UnregisterFunction("StopSFX");
        Lua.UnregisterFunction("StopAllSFX");
    }

    void InitChannels()
    {
        channels = new AudioSource[channelCount];
        fadeCoroutines = new Coroutine[channelCount];

        for (int i = 0; i < channelCount; i++)
        {
            var obj = new GameObject($"SFX_Channel_{i}");
            obj.transform.SetParent(transform);
            channels[i] = obj.AddComponent<AudioSource>();
            channels[i].loop = false;
            channels[i].playOnAwake = false;
            channels[i].volume = 0f;
        }
    }

    /// <summary>
    /// 播放 SFX，支援漸入。
    /// Script 欄：PlaySFX("Sound/grass_01", 1.0, 1.5)
    /// </summary>
    public void PlaySFX(string clipPath, double volume, double fadeInTime)
    {
        AudioClip clip = Resources.Load<AudioClip>(clipPath);
        if (clip == null)
        {
            Debug.LogWarning($"[SFX Lua] 找不到音效: {clipPath}");
            return;
        }

        int index = GetFreeChannel();
        if (index < 0) { Debug.LogWarning("[SFX Lua] 無空閒頻道"); return; }

        AudioSource src = channels[index];
        src.clip = clip;
        src.volume = 0f;
        src.Play();

        if (fadeCoroutines[index] != null) StopCoroutine(fadeCoroutines[index]);
        fadeCoroutines[index] = StartCoroutine(FadeTo(src, (float)volume, (float)fadeInTime));
    }

    /// <summary>
    /// 停止指定音效（依路徑），支援漸出。
    /// Script 欄：StopSFX("Sound/grass_01", 0.5)
    /// </summary>
    public void StopSFX(string clipPath, double fadeOutTime)
    {
        AudioClip clip = Resources.Load<AudioClip>(clipPath);
        if (clip == null) return;

        for (int i = 0; i < channels.Length; i++)
        {
            if (channels[i].clip == clip && channels[i].isPlaying)
            {
                if (fadeCoroutines[i] != null) StopCoroutine(fadeCoroutines[i]);
                fadeCoroutines[i] = StartCoroutine(FadeTo(channels[i], 0f, (float)fadeOutTime, stopAfter: true));
                return;
            }
        }
    }

    /// <summary>
    /// 停止所有 SFX，支援漸出。
    /// Script 欄：StopAllSFX(1.0)
    /// </summary>
    public void StopAllSFX(double fadeOutTime)
    {
        for (int i = 0; i < channels.Length; i++)
        {
            if (!channels[i].isPlaying) continue;
            if (fadeCoroutines[i] != null) StopCoroutine(fadeCoroutines[i]);
            fadeCoroutines[i] = StartCoroutine(FadeTo(channels[i], 0f, (float)fadeOutTime, stopAfter: true));
        }
    }

    private int GetFreeChannel()
    {
        for (int i = 0; i < channels.Length; i++)
            if (!channels[i].isPlaying) return i;
        return -1;
    }

    private IEnumerator FadeTo(AudioSource src, float target, float duration, bool stopAfter = false)
    {
        float start = src.volume, elapsed = 0f;

        if (duration <= 0f)
        {
            src.volume = target;
        }
        else
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                src.volume = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }
            src.volume = target;
        }

        if (stopAfter && target == 0f) src.Stop();
    }
}
