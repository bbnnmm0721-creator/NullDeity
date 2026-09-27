using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GlobalSpawnManager : MonoBehaviour
{
    static GlobalSpawnManager _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 其餘程式碼不動


    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(PlacePlayer(scene));
    }

    IEnumerator PlacePlayer(Scene scene)
    {
        // 等一幀，確保 PlayerMovement 單例與 SpawnPoints 都初始化完
        yield return null;

        var player = PlayerMovement.Instance;
        if (!player) yield break;

        // 收集本場景所有 SpawnPoint
        var points = Object.FindObjectsOfType<SceneSpawnPoint>();
        if (points.Length == 0) yield break;

        // 決定要用哪個 Id
        string wantId = TravelData.nextSpawnId;
        if (string.IsNullOrEmpty(wantId)) wantId = "Default";
        wantId = wantId.ToLowerInvariant();

        SceneSpawnPoint chosen = null;

        // 先找精準 id
        foreach (var p in points)
            if (p.id.ToLowerInvariant() == wantId) { chosen = p; break; }

        // 沒找到→退回 Default
        if (!chosen)
            foreach (var p in points)
                if (p.id.ToLowerInvariant() == "default") { chosen = p; break; }

        if (!chosen)         // 仍舊沒 Default → 用第一個
            chosen = points[0];

        // 移動玩家
        var cc = player.GetComponent<CharacterController>();
        bool ccOn = cc && cc.enabled;
        if (ccOn) cc.enabled = false;

        player.transform.position = chosen.transform.position;

        if (ccOn) cc.enabled = true;

        // 面向
        if (chosen.faceRight)
            player.FaceFromSpawn(SpawnSide.Left);   // 左出生→面右
        else
            player.FaceFromSpawn(SpawnSide.Right);  // 右出生→面左

        // 用完就清掉，避免回到上一場景又重放
        TravelData.nextSpawnId = null;

        Debug.Log($"[GlobalSpawn] Placed at \"{chosen.id}\" in scene \"{scene.name}\"");
    }
}
