using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 暫存在 DDOL 的重置執行者。
/// 負責摧毀所有舊的 DDOL 物件、重載 persistent scene，達成與剛開遊戲相同的初始狀態。
/// </summary>
public class GameResetBootstrap : MonoBehaviour
{
    private string _targetScene;

    /// <summary>由 GameStateManager 呼叫，傳入目標章節場景名稱後開始重置。</summary>
    public void Execute(string targetScene)
    {
        _targetScene = targetScene;
        DontDestroyOnLoad(gameObject);
        StartCoroutine(DoReset());
    }

    IEnumerator DoReset()
    {
        // Step 1：摧毀所有其他 DDOL 根物件
        // 它們的 OnDestroy 會自動清理 Instance 和 DontDestroyEverything._aliveKeys
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root != gameObject)
                Destroy(root);
        }

        // 等一幀讓所有 OnDestroy 跑完
        yield return null;
        yield return null;

        // Step 2：重載 persistent scene（Single 模式同時卸載 Menu / Chapter 等場景）
        // 因為 _aliveKeys 和 Instance 都已被清空，persistent 的所有物件可正常初始化
        yield return SceneManager.LoadSceneAsync("persistent", LoadSceneMode.Single);

        // Step 3：等 persistent 的 Awake/Start 跑完
        yield return new WaitForEndOfFrame();
        yield return null;

        // Step 4：透過剛重建的 GlobalSceneTransition 載入章節場景
        if (GlobalSceneTransition.Inst != null)
        {
            TravelData.SetNextSpawn("Default");
            GlobalSceneTransition.Go(_targetScene, "Default");
        }
        else
        {
            // Fallback：直接 Additive 載入
            Debug.LogWarning("[GameResetBootstrap] GlobalSceneTransition 未就緒，使用 Fallback 載入");
            yield return SceneManager.LoadSceneAsync(_targetScene, LoadSceneMode.Additive);
        }

        // 完成，自我銷毀
        Destroy(gameObject);
    }
}
