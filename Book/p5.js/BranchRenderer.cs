/*
 * 掛到空物件 BranchRenderer
 * dotPrefab：8×8 或 16×16 白色圓形 Sprite
 */
using UnityEngine;

public class BranchRenderer : MonoBehaviour
{
    [Header("Point settings")]
    public int pointCount = 10000;
    public float speed = Mathf.PI / 240f;   // 與 p5.js 相同
    public GameObject dotPrefab;
    public float spriteWorldSize = 0.06f;

    Transform[] dots;
    float t;

    void Awake()
    {
        // 建議把相機改成 1px = 1worldUnit（方便直接貼座標）
        var cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = 200f;          // 垂直 400u ≈ 400px
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = Color.black;

        // 生點
        dots = new Transform[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            var d = Instantiate(dotPrefab, transform).transform;
            d.localScale = Vector3.one * spriteWorldSize;
            dots[i] = d;
        }
    }

    void Update()
    {
        t += speed;

        for (int i = 0; i < pointCount; i++)
        {
            /* === 完全照你貼的 p5.js 公式 === */
            float x = i;
            float y = i / 235f;                                    // ← 新除數 235
            float e = y / 8f - 13f;
            float k = (4f + Mathf.Sin(y * 2f - t) * 3f) * Mathf.Cos(x / 29f);
            float d = Mathf.Sqrt(k * k + e * e);                   // dist(0,0,k,e)

            // 加上 sin(y/25)… 那一大坨
            float q = 3f * Mathf.Sin(k * 2f) + 0.3f /
                      (Mathf.Abs(k) < 1e-4f ? 1e-4f : k) +          // 避免除 0
                      Mathf.Sin(y / 25f) * k *
                      (9f + 4f * Mathf.Sin(e * 9f - d * 3f + t * 2f));

            float c = d - t;

            // 最終像素座標
            float px = q + 30f * Mathf.Cos(c) + 200f;
            float py = q * Mathf.Sin(c) + d * 39f - 200f;

            /* p5 +y 向下 → Unity +y 向上，所以 y 要反過來 */
            dots[i].localPosition = new Vector3(
                px - 200f,     // x 平移
                200f - py,     // y 反轉
                0f);
        }
    }
}
