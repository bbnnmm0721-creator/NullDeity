using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ProximityLayerOutline : MonoBehaviour
{
    [Header("觸發設定")]
    public float radius = 2f;
    public float zTolerance = 0.1f;
    public string outlineLayerName = "OutlineTemp";

    int defaultLayer, outlineLayer;
    Transform player;
    bool near;                     // 當前狀態
    bool playerMissingLogged;      // 避免每幀狂刷

    void Awake()
    {
        defaultLayer = gameObject.layer;
        outlineLayer = LayerMask.NameToLayer(outlineLayerName);

        if (outlineLayer == -1)
            Debug.LogError($"[{name}] 找不到 Layer \"{outlineLayerName}\" ▶ Project Settings ▸ Tags & Layers 先新增");

        player = PlayerMovement.Instance ? PlayerMovement.Instance.transform : null;
        Debug.Log($"[{name}] Awake. defaultLayer={LayerMask.LayerToName(defaultLayer)}");
    }

    void Update()
    {
        // 0. 找不到 Player 單例 → 先嘗試抓一次
        if (player == null)
        {
            if (PlayerMovement.Instance)
            {
                player = PlayerMovement.Instance.transform;
                Debug.Log($"[{name}] Player 已取得");
            }
            else
            {
                if (!playerMissingLogged)
                {
                    Debug.LogWarning($"[{name}] PlayerMovement.Instance == null (還沒生成？)");
                    playerMissingLogged = true;
                }
                return;
            }
        }

        // 1. 計算距離
        Vector2 pos2D = new Vector2(transform.position.x, transform.position.y);
        Vector2 ply2D = new Vector2(player.position.x, player.position.y);
        float xyDist = Vector2.Distance(pos2D, ply2D);
        float zDist = Mathf.Abs(transform.position.z - player.position.z);

        bool nowNear = xyDist <= radius && zDist <= zTolerance;

        // 2. 只有狀態變化才動作 & 列印
        if (nowNear != near)
        {
            near = nowNear;
            gameObject.layer = near ? outlineLayer : defaultLayer;

            Debug.Log(
                $"[{name}] near={near} | xyDist={xyDist:F2} zDist={zDist:F2} " +
                $"| Layer → {LayerMask.LayerToName(gameObject.layer)}");
        }

        // 3. 若要持續看數值，可取消註解下行
        // Debug.Log($"[{name}] tick xy={xyDist:F2} z={zDist:F2}");
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = near ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
