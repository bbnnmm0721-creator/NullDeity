using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// 2D/3D 橫向卷軸相機（支援 XYZ 軸跟隨與 Zoom）
public class SideScrollCamera : MonoBehaviour
{
    /*──────── 跟隨設定 ────────*/
    [Header("Target & Offset")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2f, 0f);

    [Header("X Axis Clamp")]
    public float minX = -Mathf.Infinity;
    public float maxX = Mathf.Infinity;

    [Header("Y Axis Clamp")]
    public float minY = -Mathf.Infinity;
    public float maxY = Mathf.Infinity;

    [Header("Z Axis Clamp")]
    public float minZ = -Mathf.Infinity;
    public float maxZ = Mathf.Infinity;

    [Header("Smooth (sec)")]
    public float smoothTime = 0.2f;

    /*──────── Zoom 參數 ────────*/
    [Header("Zoom Offset (relative)")]
    public float normalOffsetX = 0f;
    public float zoomOffsetX = -6f;
    public float normalOffsetZ = 0f;
    public float zoomOffsetZ = -6f;
    public float normalOffsetY = 0f;
    public float zoomOffsetY = 1.5f;
    public float zoomDuration = 1f;

    /*──────── 自動綁定 Player 相關 ────────*/
    [Header("Auto-Bind Player")]
    [Tooltip("尋找 Player 的 Tag")]
    public string playerTag = "Player";
    [Tooltip("找不到時隔多久再嘗試 (秒)")]
    public float retryDelay = 0.2f;

    /*──────── 內部狀態 ────────*/
    public static SideScrollCamera Inst { get; private set; }

    Vector3 vel;
    float baseZ;

    float currOffsetX, currOffsetY, currOffsetZ;
    float startOffsetX, targetOffsetX;
    float startOffsetY, targetOffsetY;
    float startOffsetZ, targetOffsetZ;

    float zoomT;
    bool isZooming;
    float nextRetryTime;

    /*────────────────────────*/
    void Awake()
    {
        Inst = this;
        baseZ = transform.position.z;

        currOffsetX = normalOffsetX;
        currOffsetY = normalOffsetY;
        currOffsetZ = normalOffsetZ;

        SceneManager.sceneLoaded += OnSceneLoaded;

        TryBindPlayer();
    }

    void OnDestroy()
    {
        if (Inst == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (target == null)
            TryBindPlayer();

        if (target == null)
            nextRetryTime = Time.unscaledTime + retryDelay;

        StartCoroutine(WaitForSpawnAndReposition());
    }

    IEnumerator WaitForSpawnAndReposition()
    {
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);

        if (target != null)
        {
            Vector3 newTargetPos = target.position;
            Vector3 desiredPos = new Vector3(
                newTargetPos.x + offset.x,
                newTargetPos.y + offset.y,
                newTargetPos.z + offset.z + currOffsetZ
            );

            desiredPos.x = Mathf.Clamp(desiredPos.x, minX, maxX);
            desiredPos.x += currOffsetX;
            desiredPos.y += currOffsetY;
            desiredPos.y = Mathf.Clamp(desiredPos.y, minY, maxY);
            desiredPos.z = Mathf.Clamp(desiredPos.z, minZ, maxZ);

            transform.position = desiredPos;

            Debug.Log($"[SideScrollCamera] 場景載入後重新定位到: {desiredPos}");
        }
    }

    /*────────────────────────*/
    void LateUpdate()
    {
        /* ---------- 若 target 為 null → 重試 ---------- */
        if (target == null && Time.unscaledTime >= nextRetryTime)
        {
            TryBindPlayer();
            if (target == null)
                nextRetryTime = Time.unscaledTime + retryDelay;
        }

        /* ---------- 沒 target 直接返回 ---------- */
        if (target == null) return;

        /* ---------- 正常跟隨邏輯 ---------- */
        Vector3 desired = new(
            target.position.x + offset.x,
            target.position.y + offset.y,
            target.position.z + offset.z);

        desired.x = Mathf.Clamp(desired.x, minX, maxX);

        /* Zoom 過渡 */
        if (isZooming)
        {
            zoomT += Time.deltaTime;
            float t = zoomDuration > 0 ? zoomT / zoomDuration : 1f;
            if (t >= 1f) { t = 1f; isZooming = false; }

            currOffsetX = Mathf.Lerp(startOffsetX, targetOffsetX, t);
            currOffsetY = Mathf.Lerp(startOffsetY, targetOffsetY, t);
            currOffsetZ = Mathf.Lerp(startOffsetZ, targetOffsetZ, t);
        }

        desired.x += currOffsetX;
        desired.y += currOffsetY;
        desired.z += currOffsetZ;

        desired.y = Mathf.Clamp(desired.y, minY, maxY);
        desired.z = Mathf.Clamp(desired.z, minZ, maxZ);

        /* 移動 */
        if (smoothTime <= 0f)
            transform.position = desired;
        else
            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref vel, smoothTime);
    }

    /*──────── 公開 API ────────*/
    public static void RequestZoom(bool zoomOut)
    {
        if (Inst != null) Inst.StartZoom(zoomOut);
    }

    public void StartZoom(bool zoomOut)
    {
        startOffsetX = currOffsetX;
        startOffsetY = currOffsetY;
        startOffsetZ = currOffsetZ;

        targetOffsetX = zoomOut ? zoomOffsetX : normalOffsetX;
        targetOffsetY = zoomOut ? zoomOffsetY : normalOffsetY;
        targetOffsetZ = zoomOut ? zoomOffsetZ : normalOffsetZ;

        zoomT = 0f;
        isZooming = true;

        if (zoomDuration <= 0f)
        {
            currOffsetX = targetOffsetX;
            currOffsetY = targetOffsetY;
            currOffsetZ = targetOffsetZ;
            isZooming = false;
        }
    }

    /*──────── 私有：嘗試綁定玩家 ────────*/
    void TryBindPlayer()
    {
        if (PlayerMovement.Instance)
        {
            target = PlayerMovement.Instance.transform;
            return;
        }

        var go = GameObject.FindGameObjectWithTag(playerTag);
        if (go) target = go.transform;
    }
}
