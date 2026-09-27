using UnityEngine;
using PixelCrushers.DialogueSystem;
using Language.Lua;

public class BGMLuaFunctions : MonoBehaviour
{
    void OnEnable()
    {
        Lua.RegisterFunction("PlayBGM", this, SymbolExtensions.GetMethodInfo(() => PlayBGM(string.Empty, (double)0)));
        Lua.RegisterFunction("StopBGM", this, SymbolExtensions.GetMethodInfo(() => StopBGM((double)0)));
        Lua.RegisterFunction("FadeOutBGM", this, SymbolExtensions.GetMethodInfo(() => FadeOutBGM((double)0)));
        Lua.RegisterFunction("FadeInBGM", this, SymbolExtensions.GetMethodInfo(() => FadeInBGM((double)0)));
        Lua.RegisterFunction("SetBGMVolume", this, SymbolExtensions.GetMethodInfo(() => SetBGMVolume((double)0)));
        Lua.RegisterFunction("PlayBGMOnce", this, SymbolExtensions.GetMethodInfo(() => PlayBGMOnce(string.Empty, string.Empty, (double)0, (double)0)));
    }

    void OnDisable()
    {
        Lua.UnregisterFunction("PlayBGM");
        Lua.UnregisterFunction("StopBGM");
        Lua.UnregisterFunction("FadeOutBGM");
        Lua.UnregisterFunction("FadeInBGM");
        Lua.UnregisterFunction("SetBGMVolume");
        Lua.UnregisterFunction("PlayBGMOnce");
    }

    /// <summary>
    /// 循環播放 BGM，支援淡入。Script 欄：PlayBGM("Music/church", 2.0)
    /// </summary>
    public void PlayBGM(string clipPath, double fadeTime)
    {
        if (BGMManager.Instance == null) return;

        AudioClip clip = Resources.Load<AudioClip>(clipPath);
        if (clip != null)
        {
            BGMManager.Instance.PlayMusic(clip, (float)fadeTime);
            Debug.Log($"[BGM Lua] 播放音乐: {clipPath}, 淡入时间: {fadeTime}");
        }
        else
        {
            Debug.LogWarning($"[BGM Lua] 找不到音乐文件: {clipPath}");
        }
    }

    /// <summary>
    /// 停止所有 BGM 並淡出。Script 欄：StopBGM(2.0)
    /// </summary>
    public void StopBGM(double fadeTime)
    {
        if (BGMManager.Instance == null) return;

        BGMManager.Instance.StopMusic((float)fadeTime);
        Debug.Log($"[BGM Lua] 停止音乐, 淡出时间: {fadeTime}");
    }

    /// <summary>
    /// 將目前 BGM 音量淡出至 0（不停止）。Script 欄：FadeOutBGM(1.5)
    /// </summary>
    public void FadeOutBGM(double duration)
    {
        if (BGMManager.Instance == null) return;

        BGMManager.Instance.FadeOut((float)duration);
        Debug.Log($"[BGM Lua] 淡出音乐, 时长: {duration}");
    }

    /// <summary>
    /// 將目前 BGM 音量淡入至正常值。Script 欄：FadeInBGM(1.5)
    /// </summary>
    public void FadeInBGM(double duration)
    {
        if (BGMManager.Instance == null) return;

        BGMManager.Instance.FadeIn((float)duration);
        Debug.Log($"[BGM Lua] 淡入音乐, 时长: {duration}");
    }

    /// <summary>
    /// 設定所有 BGM 音量。Script 欄：SetBGMVolume(0.5)
    /// </summary>
    public void SetBGMVolume(double volume)
    {
        if (BGMManager.Instance == null) return;

        BGMManager.Instance.SetDefaultVolume((float)volume);
        Debug.Log($"[BGM Lua] 设置音量: {volume}");
    }

    /// <summary>
    /// 播放一次（不循環），播完自動淡出並切換下一首。
    /// Script 欄：PlayBGMOnce("Music/age ceremony 3_3", "Music/church", 2.0, 1.5)
    /// nextClipPath 傳空字串代表播完就停：PlayBGMOnce("Music/age ceremony 3_3", "", 2.0, 1.5)
    /// </summary>
    public void PlayBGMOnce(string clipPath, string nextClipPath, double fadeInTime, double fadeOutTime)
    {
        if (BGMManager.Instance == null) return;

        AudioClip clip = Resources.Load<AudioClip>(clipPath);
        if (clip == null)
        {
            Debug.LogWarning($"[BGM Lua] 找不到音乐: {clipPath}");
            return;
        }

        BGMManager.Instance.PlayMusicOnce(clip, (float)fadeInTime, nextClipPath, (float)fadeOutTime);
        Debug.Log($"[BGM Lua] 播一次後切換: {clipPath} → {nextClipPath}");
    }
}
