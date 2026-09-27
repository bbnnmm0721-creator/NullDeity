using UnityEngine;

public class ParticleField : MonoBehaviour
{
    [Header("Layout / Motion")]
    public float masterSpeed = 1f;       // ★ (1) 全局速度旋鈕
    public float outerRadius = 12f;       // 畫面大小用父物件的 scale 控
    public float shrinkFactor = 0.75f;     // 0.6–0.8，越小層越少
    public float ringFactor = 1.35f;     // 內層比外層轉得再快
    public float baseSpin = 0.02f;     // 最外層基準轉速
    public int segments = 120;
    public float ballScale = 0.07f;     // 球相對當層半徑大小
    public int maxDepth = 12;        // 最多幾層

    [Header("Material")]
    public Material lineMat;               // Unlit/Color 白+α

    Mesh sphereMesh;

    void Awake()
    {
        sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
    }
    float phi;                          // ★ (2) 全局相位

    void Update()                       // ★ (3) 每幀累加
    {
        phi += masterSpeed * Time.deltaTime;
    }
    void OnRenderObject()
    {
        if (!lineMat || sphereMesh == null) return;
        lineMat.SetPass(0);

        /* 父物件的位移 / 旋轉 / 縮放 全套用 */
        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);

        DrawRingsIterative();

        GL.PopMatrix();
    }

    void DrawRingsIterative()
    {
        var root = transform.localToWorldMatrix;   // 父物件 → 世界

        float r = outerRadius;
        int depth = 0;

        while (r >= 0.09f && depth < maxDepth)
        {
            /* 每層自己的旋轉 */
            float selfSpin = baseSpin * Mathf.Pow(ringFactor, depth);

            float angDeg = selfSpin * phi * Mathf.Rad2Deg / 4f     // ★ (4)
                         + depth * 10f;

            Matrix4x4 m = Matrix4x4.Rotate(
                            Quaternion.AngleAxis(angDeg, Vector3.up)) *
                          Matrix4x4.Rotate(
                            Quaternion.AngleAxis(angDeg, Vector3.right));

            /* ------------- 畫線條（跟父物件一起動） ------------- */
            GL.PushMatrix();
            GL.MultMatrix(root * m);                // 父物件 × 當層旋轉

            GL.Begin(GL.LINE_STRIP);
            for (int i = 0; i <= segments; i++)
            {
                float t = i * Mathf.PI * 2f / segments;
                GL.Vertex3(Mathf.Cos(t) * r, Mathf.Sin(t) * r, 0);
            }
            GL.End();
            GL.PopMatrix();                         // 還原 GL 狀態

            /* ------------- 畫小球（用 DrawMeshNow） ------------- */
            float f = selfSpin * phi + depth * 1.5f;             // ★ (5)
            Vector3 p = new(r * Mathf.Cos(f), r * Mathf.Sin(f), 0);

            var worldBall = root * m * Matrix4x4.TRS(   // 父 × 旋轉 × 球本地
                                p, Quaternion.identity,
                                Vector3.one * (r * ballScale));

            Graphics.DrawMeshNow(sphereMesh, worldBall);

            /* 下一層 */
            r *= shrinkFactor;
            depth++;
        }
    }

}
