using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class FootstepAudioController : MonoBehaviour
{
    [System.Serializable]
    public class SurfaceSound
    {
        public string surfaceTag = "Untagged";
        public AudioClip[] footstepClips;
        [Range(0f, 1f)]
        public float volume = 0.7f;
    }

    [Header("地面音效设置")]
    [Tooltip("不同地面的脚步声")]
    public SurfaceSound[] surfaceSounds;

    [Tooltip("默认脚步声（无匹配地面时使用）")]
    public AudioClip[] defaultFootstepClips;

    [Header("播放设置")]
    [Tooltip("正常行走的脚步间隔")]
    public float normalFootstepInterval = 0.5f;

    [Tooltip("跑步时的脚步间隔")]
    public float runFootstepInterval = 0.3f;

    [Tooltip("判定为跑步的速度阈值")]
    public float runSpeedThreshold = 0.5f;

    [Tooltip("默认音量")]
    [Range(0f, 1f)]
    public float defaultVolume = 0.7f;

    [Tooltip("开始移动时立即播放第一步")]
    public bool playImmediatelyOnStart = true;

    [Header("地面检测")]
    [Tooltip("向下检测的距离")]
    public float groundCheckDistance = 0.5f;

    [Tooltip("地面检测的 Layer（-1 = Everything）")]
    public LayerMask groundLayer = -1;

    [Tooltip("Raycast 起始偏移高度")]
    public float raycastOffsetY = 0.1f;

    [Header("Animator 参数名称")]
    public string walkDirectionParam = "walkDirection";
    public string groundedParam = "grounded";
    public string speedParam = "speed";

    [Header("调试")]
    public bool enableDebugLog = false;
    public bool showRaycastGizmo = true;

    private AudioSource audioSource;
    private Animator animator;
    private float lastFootstepTime;
    private string currentSurfaceTag = "Untagged";
    private string lastDetectedTag = "";
    private Dictionary<string, SurfaceSound> surfaceDictionary;
    private bool wasPlayingFootsteps = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();

        if (audioSource == null)
        {
            Debug.LogError("[FootstepAudioController] 找不到 AudioSource 组件！");
            return;
        }

        if (animator == null)
        {
            Debug.LogError("[FootstepAudioController] 找不到 Animator 组件！");
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0.2f;

        BuildSurfaceDictionary();

        lastFootstepTime = -normalFootstepInterval;

        if (enableDebugLog)
        {
            Debug.Log($"[FootstepAudioController] 初始化完成，已配置 {surfaceSounds.Length} 种地面音效");
            LogSurfaceConfiguration();
        }
    }

    void BuildSurfaceDictionary()
    {
        surfaceDictionary = new Dictionary<string, SurfaceSound>();

        foreach (var surface in surfaceSounds)
        {
            if (!surfaceDictionary.ContainsKey(surface.surfaceTag))
            {
                surfaceDictionary.Add(surface.surfaceTag, surface);
            }
            else
            {
                Debug.LogWarning($"[FootstepAudioController] 重复的地面 Tag: {surface.surfaceTag}");
            }
        }
    }

    void LogSurfaceConfiguration()
    {
        Debug.Log("=== 地面音效配置 ===");
        foreach (var kvp in surfaceDictionary)
        {
            string clipsInfo = kvp.Value.footstepClips != null ?
                $"{kvp.Value.footstepClips.Length} 个音效" : "无音效";
            Debug.Log($"  Tag '{kvp.Key}': {clipsInfo}, 音量 {kvp.Value.volume}");
        }
        string defaultInfo = defaultFootstepClips != null ?
            $"{defaultFootstepClips.Length} 个音效" : "无音效";
        Debug.Log($"  默认音效: {defaultInfo}");
        Debug.Log("==================");
    }

    void Update()
    {
        if (audioSource == null || animator == null)
            return;

        bool shouldPlayFootsteps = ShouldPlayFootsteps();

        if (shouldPlayFootsteps)
        {
            if (!wasPlayingFootsteps)
            {
                if (enableDebugLog)
                    Debug.Log("[FootstepAudioController] 开始移动");

                if (playImmediatelyOnStart)
                {
                    DetectCurrentSurface();
                    PlayFootstepSound();
                    lastFootstepTime = Time.time;
                }
            }

            DetectCurrentSurface();
            UpdateFootstepTiming();
        }
        else
        {
            if (wasPlayingFootsteps)
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }

                lastFootstepTime = Time.time - normalFootstepInterval;

                if (enableDebugLog)
                    Debug.Log("[FootstepAudioController] 停止移动");
            }
        }

        wasPlayingFootsteps = shouldPlayFootsteps;
    }

    bool ShouldPlayFootsteps()
    {
        if (animator == null)
            return false;

        int walkDirection = animator.GetInteger(walkDirectionParam);
        bool grounded = animator.GetBool(groundedParam);

        return (walkDirection != 0) && grounded;
    }

    void DetectCurrentSurface()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * raycastOffsetY;

        if (Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance, groundLayer))
        {
            string detectedTag = hit.collider.tag;

            if (detectedTag != lastDetectedTag)
            {
                lastDetectedTag = detectedTag;
                currentSurfaceTag = detectedTag;

                if (enableDebugLog)
                {
                    Debug.Log($"[FootstepAudioController] 检测到地面变化: {hit.collider.name} (Tag: {detectedTag})");

                    if (surfaceDictionary.ContainsKey(detectedTag))
                    {
                        var surface = surfaceDictionary[detectedTag];
                        if (surface.footstepClips == null || surface.footstepClips.Length == 0)
                        {
                            Debug.LogWarning($"[FootstepAudioController] Tag '{detectedTag}' 已配置但没有音效！将使用默认音效");
                        }
                        else
                        {
                            Debug.Log($"[FootstepAudioController] 使用 Tag '{detectedTag}' 的音效 ({surface.footstepClips.Length} 个)");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[FootstepAudioController] Tag '{detectedTag}' 未配置，将使用默认音效");
                    }
                }
            }
        }
        else
        {
            if (currentSurfaceTag != "Untagged")
            {
                currentSurfaceTag = "Untagged";
                lastDetectedTag = "Untagged";

                if (enableDebugLog)
                    Debug.LogWarning("[FootstepAudioController] 未检测到地面！使用默认音效");
            }
        }
    }

    void UpdateFootstepTiming()
    {
        float speed = animator.GetFloat(speedParam);
        float interval = speed > runSpeedThreshold ? runFootstepInterval : normalFootstepInterval;

        if (Time.time - lastFootstepTime >= interval)
        {
            PlayFootstepSound();
            lastFootstepTime = Time.time;
        }
    }

    void PlayFootstepSound()
    {
        AudioClip clip = GetFootstepClip();
        float volume = GetFootstepVolume();

        if (clip != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(clip, volume);

            if (enableDebugLog)
                Debug.Log($"[FootstepAudioController] 播放: {clip.name}, 地面: {currentSurfaceTag}, 音量: {volume:F2}");
        }
        else
        {
            if (enableDebugLog)
                Debug.LogError($"[FootstepAudioController] 无法播放脚步声！Clip: {(clip != null ? clip.name : "NULL")}, 地面: {currentSurfaceTag}");
        }
    }

    AudioClip GetFootstepClip()
    {
        if (surfaceDictionary.ContainsKey(currentSurfaceTag))
        {
            var surface = surfaceDictionary[currentSurfaceTag];
            if (surface.footstepClips != null && surface.footstepClips.Length > 0)
            {
                return surface.footstepClips[Random.Range(0, surface.footstepClips.Length)];
            }
            else
            {
                if (enableDebugLog)
                    Debug.LogWarning($"[FootstepAudioController] Tag '{currentSurfaceTag}' 的音效数组为空，使用默认音效");
            }
        }

        if (defaultFootstepClips != null && defaultFootstepClips.Length > 0)
        {
            return defaultFootstepClips[Random.Range(0, defaultFootstepClips.Length)];
        }

        if (enableDebugLog)
            Debug.LogError("[FootstepAudioController] 没有可用的脚步声音效！");

        return null;
    }

    float GetFootstepVolume()
    {
        if (surfaceDictionary.ContainsKey(currentSurfaceTag))
        {
            return surfaceDictionary[currentSurfaceTag].volume;
        }

        return defaultVolume;
    }

    void OnDrawGizmos()
    {
        if (!showRaycastGizmo) return;

        Vector3 origin = transform.position + Vector3.up * raycastOffsetY;
        Vector3 end = origin + Vector3.down * groundCheckDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, 0.1f);
    }

    [ContextMenu("测试当前地面")]
    void TestCurrentSurface()
    {
        DetectCurrentSurface();
        Debug.Log($"当前地面 Tag: {currentSurfaceTag}");

        AudioClip clip = GetFootstepClip();
        if (clip != null)
        {
            Debug.Log($"将播放音效: {clip.name}");
        }
        else
        {
            Debug.LogError("没有可用的音效！");
        }
    }

    [ContextMenu("显示配置信息")]
    void ShowConfiguration()
    {
        LogSurfaceConfiguration();
    }
}
