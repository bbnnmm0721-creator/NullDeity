using UnityEngine;

public class SceneMusicConfig : MonoBehaviour
{
    [System.Serializable]
    public class MusicTrack
    {
        [Header("音乐设置")]
        public AudioClip musicClip;

        [Tooltip("音乐标识符（相同ID的音乐不会重新播放）")]
        public string musicID = "";

        [Header("音量设置")]
        [Range(0f, 1f)]
        public float sceneVolume = 0.7f;

        [Header("对话音量")]
        public DialogueVolumeMode dialogueMode = DialogueVolumeMode.ReduceToValue;

        [Range(0f, 1f)]
        public float dialogueVolume = 0.3f;

        [Range(0f, 1f)]
        public float dialogueVolumeFactor = 0.5f;

        [Header("过渡时间")]
        public float fadeInTime = 2f;
        public float fadeOutTime = 1.5f;

        public enum DialogueVolumeMode
        {
            KeepOriginal,
            ReduceToValue,
            MultiplyByFactor
        }

        public float GetDialogueVolume()
        {
            switch (dialogueMode)
            {
                case DialogueVolumeMode.KeepOriginal:
                    return sceneVolume;
                case DialogueVolumeMode.ReduceToValue:
                    return Mathf.Min(dialogueVolume, sceneVolume);
                case DialogueVolumeMode.MultiplyByFactor:
                    return sceneVolume * dialogueVolumeFactor;
                default:
                    return sceneVolume;
            }
        }

        public string GetID()
        {
            if (!string.IsNullOrEmpty(musicID))
                return musicID.Trim();

            if (musicClip != null)
                return musicClip.name.Trim();

            return "";
        }
    }

    [Header("场景音乐轨道")]
    public MusicTrack[] musicTracks;

    [Header("调试")]
    public bool showDebugLog = false;

    void Start()
    {
        if (BGMManager.Instance != null)
        {
            BGMManager.Instance.OnSceneLoaded(this);
        }
        else
        {
            Debug.LogWarning("[SceneMusicConfig] BGMManager 未找到！");
        }
    }
}
