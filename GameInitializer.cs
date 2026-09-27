using UnityEngine;
using UnityEngine.SceneManagement;

public class GameInitializer : MonoBehaviour
{
    [Header("启动时加载的场景")]
    public string persistentSceneName = "persistent";

    void Awake()
    {
        if (!SceneManager.GetSceneByName(persistentSceneName).isLoaded)
        {
            Debug.Log($"[GameInitializer] 附加加载场景: {persistentSceneName}");
            SceneManager.LoadScene(persistentSceneName, LoadSceneMode.Additive);
        }
        else
        {
            Debug.Log($"[GameInitializer] 场景 {persistentSceneName} 已加载，跳过");
        }
    }
}
