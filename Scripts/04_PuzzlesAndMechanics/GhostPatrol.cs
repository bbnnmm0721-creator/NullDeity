using UnityEngine;
using System.Collections;

public class GhostPatrol : MonoBehaviour
{
    [Header("巡邏範圍設定")]
    [Tooltip("巡邏的最小 X 座標")]
    public float minX = -5f;

    [Tooltip("巡邏的最大 X 座標")]
    public float maxX = 5f;

    [Tooltip("移動速度")]
    public float moveSpeed = 2f;

    [Header("鬼的初始位置")]
    [Tooltip("鬼的重生點位置（留空則使用當前位置）")]
    public Transform ghostSpawnPoint;

    [Header("玩家重生設定")]
    [Tooltip("玩家重生點的 SceneSpawnPoint")]
    public SceneSpawnPoint playerSpawnPoint;

    [Header("Fade 設定")]
    [Tooltip("拖入場景中的 fade CanvasGroup 物件")]
    public CanvasGroup fadeCanvasGroup;

    [Tooltip("Fade In 持續時間")]
    public float fadeInDuration = 0.5f;

    [Tooltip("Fade Out 持續時間")]
    public float fadeOutDuration = 0.5f;

    [Tooltip("黑屏停留時間")]
    public float blackScreenDuration = 0.3f;

    [Header("視覺設定")]
    [Tooltip("鬼的 SpriteRenderer（用於翻轉）")]
    public SpriteRenderer ghostSprite;

    [Header("檢測設定")]
    [Tooltip("檢測間隔（避免重複觸發）")]
    public float detectionCooldown = 0.1f;

    private Vector3 initialPosition;
    private int direction = 1;
    private bool isResetting = false;
    private BoxCollider boxCol;
    private float lastDetectionTime = 0f;

    void Start()
    {
        boxCol = GetComponent<BoxCollider>();

        if (boxCol == null)
        {
            Debug.LogError("[GhostPatrol] 找不到 BoxCollider！請確保鬼物件上有 BoxCollider 組件");
        }

        if (ghostSprite == null)
        {
            ghostSprite = GetComponent<SpriteRenderer>();
        }

        if (ghostSpawnPoint != null)
        {
            initialPosition = ghostSpawnPoint.position;
        }
        else
        {
            initialPosition = transform.position;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[GhostPatrol] 未設定 fadeCanvasGroup，將無法執行淡入淡出效果");
        }

        transform.position = new Vector3(initialPosition.x, transform.position.y, transform.position.z);

        if (transform.position.x < minX || transform.position.x > maxX)
        {
            transform.position = new Vector3(Mathf.Clamp(initialPosition.x, minX, maxX), transform.position.y, transform.position.z);
            Debug.LogWarning($"[GhostPatrol] 初始位置超出範圍，已調整至 X = {transform.position.x}");
        }

        direction = moveSpeed > 0 ? 1 : -1;
        UpdateSpriteDirection();
    }

    void Update()
    {
        if (isResetting) return;

        float targetX = transform.position.x + (direction * moveSpeed * Time.deltaTime);

        if (targetX >= maxX)
        {
            targetX = maxX;
            direction = -1;
            UpdateSpriteDirection();
        }
        else if (targetX <= minX)
        {
            targetX = minX;
            direction = 1;
            UpdateSpriteDirection();
        }

        transform.position = new Vector3(targetX, transform.position.y, transform.position.z);
    }

    void UpdateSpriteDirection()
    {
        if (ghostSprite == null) return;

        if (direction > 0)
        {
            ghostSprite.flipX = false;
        }
        else
        {
            ghostSprite.flipX = true;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (isResetting) return;

        if (Time.time - lastDetectionTime < detectionCooldown)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            PlayerMovement player = other.GetComponent<PlayerMovement>();

            if (player != null)
            {
                bool isCrouching = player.GetType()
                    .GetField("isCrouching", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(player) is bool crouching && crouching;

                if (isCrouching)
                {
                    return;
                }
            }

            lastDetectionTime = Time.time;
            StartCoroutine(HandlePlayerCaught(other.gameObject));
        }
    }

    IEnumerator HandlePlayerCaught(GameObject player)
    {
        isResetting = true;

        Debug.Log("[GhostPatrol] 玩家被鬼抓到！");

        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeIn());
        }
        else
        {
            yield return new WaitForSeconds(fadeInDuration);
        }

        ResetGhostPosition();

        if (playerSpawnPoint != null)
        {
            PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                player.transform.position = playerSpawnPoint.transform.position;

                SpawnSide spawnSide = playerSpawnPoint.faceRight ? SpawnSide.Right : SpawnSide.Left;
                playerMovement.FaceFromSpawn(spawnSide);
                playerMovement.SetPositionForSpawn(playerSpawnPoint.transform.position);

                Debug.Log($"[GhostPatrol] 玩家已重生至 {playerSpawnPoint.id}");
            }
            else
            {
                player.transform.position = playerSpawnPoint.transform.position;
                Debug.LogWarning("[GhostPatrol] 找不到 PlayerMovement，僅移動位置");
            }
        }
        else
        {
            Debug.LogWarning("[GhostPatrol] 未設定 playerSpawnPoint，玩家無法重生");
        }

        yield return new WaitForSeconds(blackScreenDuration);

        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeOut());
        }
        else
        {
            yield return new WaitForSeconds(fadeOutDuration);
        }

        isResetting = false;
    }

    void ResetGhostPosition()
    {
        transform.position = new Vector3(initialPosition.x, transform.position.y, transform.position.z);

        direction = 1;
        UpdateSpriteDirection();

        Debug.Log($"[GhostPatrol] 鬼已重置到初始位置 X = {initialPosition.x}");
    }

    IEnumerator FadeIn()
    {
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
    }

    IEnumerator FadeOut()
    {
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 minPos = new Vector3(minX, transform.position.y, transform.position.z);
        Vector3 maxPos = new Vector3(maxX, transform.position.y, transform.position.z);

        Gizmos.DrawLine(minPos + Vector3.up * 0.5f, minPos - Vector3.up * 0.5f);
        Gizmos.DrawLine(maxPos + Vector3.up * 0.5f, maxPos - Vector3.up * 0.5f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(minPos, maxPos);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(minPos + Vector3.up * 0.7f, $"Min X: {minX}");
        UnityEditor.Handles.Label(maxPos + Vector3.up * 0.7f, $"Max X: {maxX}");
#endif
    }
}
