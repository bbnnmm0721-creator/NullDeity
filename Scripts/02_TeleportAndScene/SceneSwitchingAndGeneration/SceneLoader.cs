using UnityEngine;
using UnityEngine.SceneManagement;
using PixelCrushers.DialogueSystem;

public class SceneLoader : MonoBehaviour
{
    const string Scene_Chapter1 = "Chapter1-1";
    const string Scene_Chapter2 = "Chapter1-2";
    const string Scene_Chapter3 = "Chapter1-3";
    const string Scene_Chapter4 = "Chapter1-4R";
    const string Scene_Chapter5 = "Chapter1-4-1R";
    const string Scene_Chapter6 = "Chapter1-4-2R";
    const string Scene_Chapter7 = "Chapter1-4-3R";
    const string Scene_Chapter8 = "Chapter1-4-4";
    const string Scene_Chapter10 = "Chapter1-5";
    const string Scene_Chapter11 = "Chapter1-6";
    const string Scene_Chapter14 = "Chapter1-6-1";
    const string Scene_Chapter15 = "Chapter1-6-2";
    const string Scene_Chapter12 = "Chapter1-7";
    const string Scene_Chapter13 = "Chapter1-4L";
    const string Scene_Chapter16 = "Chapter2-3";
    const string Scene_Elevator = "elevator";

    [Header("條件檢查設定")]
    [Tooltip("需要檢查的必要物件（如書本）")]
    public GameObject requiredObject;
    [Tooltip("必要物件的名稱（如果為空則自動搜尋 UIRoot）")]
    public string requiredObjectName = "UIRoot";
    [Tooltip("當條件不滿足時顯示的對話標題")]
    public string warningConversation = "MissingBookWarning";
    [Tooltip("是否啟用條件檢查")]
    public bool enableConditionCheck = false;

    static bool isLoading;
    static float lastLoadTime;
    const float LOAD_TIMEOUT = 5f;

    void OnTriggerEnter(Collider other)
    {
        if (isLoading && Time.time - lastLoadTime > LOAD_TIMEOUT)
        {
            Debug.LogWarning($"<color=orange>[SceneLoader] ⚠️ 檢測到加載超時（{LOAD_TIMEOUT}秒），強制重置 isLoading</color>");
            isLoading = false;
        }

        Debug.Log($"<color=cyan>[SceneLoader] {gameObject.name} 觸發碰撞</color> - 對象: {other.name}, Tag: {other.tag}, isLoading: {isLoading}");

        if (!other.CompareTag("Player"))
        {
            Debug.LogWarning($"[SceneLoader] 忽略非 Player 對象: {other.name} (Tag: {other.tag})");
            return;
        }

        if (isLoading)
        {
            Debug.LogWarning($"[SceneLoader] ⚠️ isLoading = true，正在加載場景，忽略觸發");
            return;
        }

        if (enableConditionCheck && !CheckConditions())
        {
            Debug.Log("[SceneLoader] 條件不滿足，顯示提醒對話");
            ShowWarningDialogue();
            return;
        }

        isLoading = true;
        lastLoadTime = Time.time;

        Debug.Log($"<color=green>[SceneLoader] ✅ 觸發傳送門成功！</color> Tag: {gameObject.tag}");

        switch (gameObject.tag)
        {
            case "PortalA":
                TravelData.SetNextSpawn("Default");
                GlobalSceneTransition.Go("Chapter1-1", "Default");
                break;

            case "PortalB":
                TravelData.SetNextSpawn("RightEnd");
                GlobalSceneTransition.Go("Chapter1-2", "RightEnd");
                break;

            case "PortalC":
                TravelData.SetNextSpawn("RightEnd");
                GlobalSceneTransition.Go("Chapter1-3", "RightEnd");
                break;

            case "ToCorridorFromA":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-2", "LeftEnd");
                break;

            case "ToCorridorFromC":
                TravelData.SetNextSpawn("RightEnd");
                GlobalSceneTransition.Go("Chapter1-2", "RightEnd");
                break;

            case "ToCorridorFromChurch":
                TravelData.SetNextSpawn("Default");
                GlobalSceneTransition.Go("Chapter1-2", "Default");
                break;

            case "ToRightSecondFloor":
                TravelData.SetNextSpawn("RightEnd");
                GlobalSceneTransition.Go("Chapter1-4R", "RightEnd");
                break;

            case "ToLeftSecondFloor":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-4L", "LeftEnd");
                break;

            case "ToGarden":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-1", "LeftEnd");
                break;

            case "Trigger_Ghost":
                TravelData.SetNextSpawn("RightEnd");
                GlobalSceneTransition.Go("Chapter1-4-1R", "RightEnd");
                break;

            case "Trigger_Libary":
                TravelData.SetNextSpawn("RightEnd");
                GlobalSceneTransition.Go("Chapter1-7", "RightEnd");
                break;

            case "Toghost_2":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-4-2R", "LeftEnd");
                break;

            case "Toghost_3":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-4-3R", "LeftEnd");
                break;

            case "Ghost2_3":
                TravelData.SetNextSpawn("RightEnd");
                GlobalSceneTransition.Go("Chapter1-4-3R", "RightEnd");
                break;

            case "Ghost3_R":
                TravelData.SetNextSpawn("RightEnd");
                GlobalSceneTransition.Go("Chapter1-4R", "RightEnd");
                break;

            case "ToHole":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-5", "LeftEnd");
                break;

            case "ToGardenRight":
                TravelData.SetNextSpawn("RightEnd");
                GlobalSceneTransition.Go("Chapter1-1", "RightEnd");
                break;

            case "ToChurch":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-6", "LeftEnd");
                break;
            case "ToChurch_2":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-6-1", "LeftEnd");
                break;
            case "ToChurch_3":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-6-2", "LeftEnd");
                break;

            case "Trigger_Balcony":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-8", "LeftEnd");
                break;

            case "Balcony_Right":
                TravelData.SetNextSpawn("RightEnd");
                GlobalSceneTransition.Go("Chapter1-8", "RightEnd");
                break;

            case "BackTwoFloor":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-4R", "LeftEnd");
                break;

            case "BackTwoFloor2":
                TravelData.SetNextSpawn("Default");
                GlobalSceneTransition.Go("Chapter1-4R", "Default");
                break;

            case "ToThreeFloor":
                TravelData.SetNextSpawn("LeftEnd");
                GlobalSceneTransition.Go("Chapter1-9", "LeftEnd");
                break;

            case "ToChapter2":
                TravelData.SetNextSpawn("Default");
                GlobalSceneTransition.Go("Chapter2-3", "Default");
                break;

            default:
                isLoading = false;
                Debug.LogWarning($"[SceneLoader] 未知的 Tag: {gameObject.tag}");
                break;
        }
    }

    bool CheckConditions()
    {
        if (requiredObject == null && !string.IsNullOrEmpty(requiredObjectName))
        {
            requiredObject = GameObject.Find(requiredObjectName);
        }

        if (requiredObject == null)
        {
            Debug.LogWarning($"[SceneLoader] 找不到必要物件: {requiredObjectName}");
            return true;
        }

        bool isActive = requiredObject.activeInHierarchy;

        Debug.Log($"[SceneLoader] 檢查 {requiredObject.name} 狀態: {(isActive ? "Active" : "Inactive")}");

        return isActive;
    }

    void ShowWarningDialogue()
    {
        if (string.IsNullOrEmpty(warningConversation))
        {
            Debug.LogWarning("[SceneLoader] 未設定警告對話名稱");
            return;
        }

        DialogueManager.StartConversation(warningConversation);

        Debug.Log($"[SceneLoader] 播放警告對話: {warningConversation}");
    }

    public bool CanTransition()
    {
        if (!enableConditionCheck) return true;
        return CheckConditions();
    }

    public static void ResetLoadingState()
    {
        isLoading = false;
        lastLoadTime = 0f;
        Debug.Log($"<color=yellow>[SceneLoader] 🔄 強制重置 isLoading = false</color>");
    }
}
