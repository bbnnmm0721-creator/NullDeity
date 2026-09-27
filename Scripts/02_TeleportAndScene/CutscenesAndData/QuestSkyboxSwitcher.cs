using UnityEngine;
using UnityEngine.Rendering;
using PixelCrushers.DialogueSystem;

public class QuestSkyboxSwitcher : MonoBehaviour
{
    [Header("天空盒材質")]
    [Tooltip("早晨天空盒")]
    public Material morningSkybox;
    
    [Tooltip("下午天空盒")]
    public Material afternoonSkybox;
    
    [Tooltip("夜晚天空盒")]
    public Material nightSkybox;

    [Header("任務設定")]
    [Tooltip("任務名稱")]
    public string questName = "MainHall_C1_6";
    
    [Tooltip("根據任務狀態切換天空盒")]
    public QuestSkyboxMapping[] questMappings = new QuestSkyboxMapping[]
    {
        new QuestSkyboxMapping { questState = QuestState.Unassigned, skyboxMaterial = null },
        new QuestSkyboxMapping { questState = QuestState.Active, skyboxMaterial = null },
        new QuestSkyboxMapping { questState = QuestState.Success, skyboxMaterial = null }
    };

    [Header("或根據任務條目狀態")]
    [Tooltip("使用任務條目狀態而非任務狀態")]
    public bool useQuestEntryState = false;
    
    [Tooltip("任務條目編號")]
    public int entryNumber = 1;

    [Header("平滑過渡")]
    [Tooltip("是否使用漸變過渡")]
    public bool useFade = true;
    
    [Tooltip("過渡時間（秒）")]
    public float fadeDuration = 2f;

    Material currentSkybox;
    bool isTransitioning;

    void OnEnable()
    {
        Lua.RegisterFunction(nameof(SetSkyboxByName), this, SymbolExtensions.GetMethodInfo(() => SetSkyboxByName(string.Empty)));
    }

    void OnDisable()
    {
        Lua.UnregisterFunction(nameof(SetSkyboxByName));
    }

    void Start()
    {
        UpdateSkybox();
    }

    public void OnQuestStateChange(string questName)
    {
        if (this.questName == questName)
        {
            UpdateSkybox();
        }
    }

    public void OnQuestEntryStateChange(QuestEntryArgs args)
    {
        if (useQuestEntryState && args.questName == questName && args.entryNumber == entryNumber)
        {
            UpdateSkybox();
        }
    }

    void UpdateSkybox()
    {
        Material targetSkybox = null;

        if (useQuestEntryState)
        {
            QuestState entryState = QuestLog.GetQuestEntryState(questName, entryNumber);
            targetSkybox = GetSkyboxForQuestState(entryState);
        }
        else
        {
            QuestState questState = QuestLog.GetQuestState(questName);
            targetSkybox = GetSkyboxForQuestState(questState);
        }

        if (targetSkybox != null && targetSkybox != currentSkybox)
        {
            if (useFade && Application.isPlaying && !isTransitioning)
            {
                StartCoroutine(FadeSkybox(targetSkybox));
            }
            else
            {
                SetSkybox(targetSkybox);
            }
        }
    }

    Material GetSkyboxForQuestState(QuestState state)
    {
        foreach (var mapping in questMappings)
        {
            if (mapping.questState == state)
            {
                return mapping.skyboxMaterial;
            }
        }
        return null;
    }

    void SetSkybox(Material skybox)
    {
        RenderSettings.skybox = skybox;
        DynamicGI.UpdateEnvironment();
        currentSkybox = skybox;
        
        Debug.Log($"[QuestSkyboxSwitcher] 切換天空盒: {(skybox ? skybox.name : "null")}");
    }

    System.Collections.IEnumerator FadeSkybox(Material targetSkybox)
    {
        isTransitioning = true;

        Material startSkybox = currentSkybox;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            
            if (startSkybox != null && targetSkybox != null)
            {
                RenderSettings.skybox.Lerp(startSkybox, targetSkybox, t);
                DynamicGI.UpdateEnvironment();
            }
            
            yield return null;
        }

        SetSkybox(targetSkybox);
        isTransitioning = false;
    }

    public void SetSkyboxByName(string skyboxName)
    {
        Material targetSkybox = null;

        switch (skyboxName.ToLower())
        {
            case "morning":
                targetSkybox = morningSkybox;
                break;
            case "afternoon":
                targetSkybox = afternoonSkybox;
                break;
            case "night":
                targetSkybox = nightSkybox;
                break;
            default:
                Debug.LogWarning($"[QuestSkyboxSwitcher] 未知的天空盒名稱: {skyboxName}");
                break;
        }

        if (targetSkybox != null)
        {
            if (useFade && Application.isPlaying && !isTransitioning)
            {
                StartCoroutine(FadeSkybox(targetSkybox));
            }
            else
            {
                SetSkybox(targetSkybox);
            }
        }
    }

    public void SetMorningSkybox() => SetSkyboxByName("morning");
    public void SetAfternoonSkybox() => SetSkyboxByName("afternoon");
    public void SetNightSkybox() => SetSkyboxByName("night");
}

[System.Serializable]
public class QuestSkyboxMapping
{
    public QuestState questState;
    public Material skyboxMaterial;
}
