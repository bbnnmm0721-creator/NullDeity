using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class StarTrailsPureMesh : MonoBehaviour
{
    [Header("軌跡設定")]
    [Range(100, 5000)]
    public int starCount = 3600;

    [Range(0.1f, 2f)]
    public float baseSpeed = 0.55f;

    [Range(60f, 360f)]
    public float maxArcDegrees = 160f;

    [Header("視覺設定")]
    [Range(1f, 50f)]
    public float worldScale = 10f;

    [Range(0.45f, 0.65f)]
    public float centerX = 0.52f;

    [Range(0.9f, 1.3f)]
    public float centerY = 1.10f;

    [Range(0.8f, 1.5f)]
    public float maxRadius = 1.25f;

    [Range(0.01f, 0.5f)]
    public float lineWidth = 0.05f;

    [Header("軌跡累積")]
    [Range(1, 10)]
    public int segmentsPerFrame = 2;

    [Range(0.01f, 1f)]
    public float minAlpha = 0.1f;

    [Range(0.5f, 1f)]
    public float maxAlpha = 0.8f;

    [Header("顏色設定")]
    public Color starColor = Color.white;
    [Range(0.1f, 5f)]
    public float brightness = 1f;

    [Header("材質設定")]
    public Material starMaterial;

    [Header("控制")]
    public bool autoStart = true;
    public bool enableDashEffect = true;

    // 私有變數
    private List<Star> stars = new List<Star>();
    private Mesh starMesh;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

    private Vector2 rotationCenter;
    private float progressedRadians = 0f;
    private bool isPlaying = true;

    // Mesh 數據緩存
    private List<Vector3> allVertices = new List<Vector3>();
    private List<Color> allColors = new List<Color>();
    private List<int> allTriangles = new List<int>();
    private List<Vector2> allUVs = new List<Vector2>();

    [System.Serializable]
    private class Star
    {
        public float radius;
        public float angle;
        public float baseAlpha;
        public float dashLength;
        public float gapLength;
        public float jitter;

        // 累積軌跡點
        public List<Vector3> trailPoints = new List<Vector3>();
        public List<float> trailAlphas = new List<float>();

        public Star(float r, float a, float al, float dash, float gap, float j)
        {
            radius = r;
            angle = a;
            baseAlpha = al;
            dashLength = dash;
            gapLength = gap;
            jitter = j;
        }
    }

    void Start()
    {
        InitializeSystem();
        GenerateStars();

        if (autoStart)
            isPlaying = true;
    }

    void InitializeSystem()
    {
        // 獲取組件
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();

        // 移除Camera組件（如果存在）
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            DestroyImmediate(cam);
            Debug.Log("已移除Camera組件，使用純Mesh渲染");
        }

        // 創建 Mesh
        starMesh = new Mesh();
        starMesh.name = "StarTrailsPureMesh";
        starMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        meshFilter.mesh = starMesh;

        // 設置材質
        if (starMaterial == null)
        {
            CreateDefaultMaterial();
        }
        meshRenderer.material = starMaterial;

        // 計算旋轉中心（本地空間）
        rotationCenter = new Vector2(
            (centerX - 0.5f) * worldScale,
            (centerY - 0.5f) * worldScale
        );

        Debug.Log("StarTrails Pure Mesh系統初始化完成");
    }

    void CreateDefaultMaterial()
    {
        // 使用URP兼容的Shader
        starMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (starMaterial.shader == null)
            starMaterial = new Material(Shader.Find("Sprites/Default"));

        starMaterial.name = "StarTrailsPureMaterial";
        starMaterial.color = starColor;

        // 設置透明混合
        starMaterial.SetFloat("_Surface", 1);
        starMaterial.SetFloat("_Blend", 1);
        starMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        starMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        starMaterial.SetFloat("_ZWrite", 0);
        starMaterial.SetFloat("_AlphaClip", 0);

        // 雙面渲染
        starMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        starMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    void GenerateStars()
    {
        stars.Clear();
        float maxR = worldScale * maxRadius * 0.5f;

        for (int i = 0; i < starCount; i++)
        {
            float r = Mathf.Pow(Random.Range(0f, 1f), 0.9f) * maxR;
            float a = Random.Range(0f, Mathf.PI * 2f);
            float alpha = Random.Range(minAlpha, maxAlpha);
            float dash = Random.Range(7f, 14f);
            float gap = Random.Range(6f, 14f);
            float jitter = Random.Range(0f, 1000f);

            stars.Add(new Star(r, a, alpha, dash, gap, jitter));
        }

        Debug.Log($"生成了 {stars.Count} 個星點");
    }

    void Update()
    {
        if (!isPlaying || progressedRadians >= Mathf.Deg2Rad * maxArcDegrees)
            return;

        UpdateStarTrails();
        UpdateMesh();
    }

    void UpdateStarTrails()
    {
        float deltaAngle = Mathf.Deg2Rad * baseSpeed * Time.deltaTime;
        deltaAngle = Mathf.Min(deltaAngle, Mathf.Deg2Rad * maxArcDegrees - progressedRadians);
        progressedRadians += deltaAngle;

        // 為每顆星添加軌跡點
        foreach (Star star in stars)
        {
            // 計算多個中間點以獲得平滑軌跡
            for (int seg = 0; seg < segmentsPerFrame; seg++)
            {
                float segmentAngle = star.angle + deltaAngle * (seg + 1) / segmentsPerFrame;

                Vector3 newPoint = new Vector3(
                    rotationCenter.x + Mathf.Cos(segmentAngle) * star.radius,
                    rotationCenter.y + Mathf.Sin(segmentAngle) * star.radius,
                    0
                );

                // 檢查虛線效果
                if (enableDashEffect)
                {
                    float period = star.dashLength + star.gapLength;
                    float phase = (segmentAngle * star.radius * 0.02f + star.jitter) % period;
                    bool drawThis = phase < star.dashLength;

                    if (!drawThis) continue;
                }

                // 添加軌跡點
                star.trailPoints.Add(newPoint);
                star.trailAlphas.Add(star.baseAlpha * brightness);
            }

            // 更新角度
            star.angle += deltaAngle;
        }
    }

    void UpdateMesh()
    {
        allVertices.Clear();
        allColors.Clear();
        allTriangles.Clear();
        allUVs.Clear();

        int vertexIndex = 0;

        foreach (Star star in stars)
        {
            for (int i = 0; i < star.trailPoints.Count - 1; i++)
            {
                if (star.trailAlphas[i] < 0.01f) continue;

                Vector3 p1 = star.trailPoints[i];
                Vector3 p2 = star.trailPoints[i + 1];

                // 計算線段方向和垂直向量
                Vector3 direction = (p2 - p1).normalized;
                Vector3 perpendicular = Vector3.Cross(direction, Vector3.forward) * lineWidth * 0.5f;

                // 創建四邊形的四個頂點
                allVertices.Add(p1 - perpendicular);
                allVertices.Add(p1 + perpendicular);
                allVertices.Add(p2 + perpendicular);
                allVertices.Add(p2 - perpendicular);

                // 顏色（漸變透明度）
                Color lineColor = starColor;
                lineColor.a = star.trailAlphas[i];

                allColors.Add(lineColor);
                allColors.Add(lineColor);
                allColors.Add(lineColor);
                allColors.Add(lineColor);

                // UV 座標
                allUVs.Add(new Vector2(0, 0));
                allUVs.Add(new Vector2(0, 1));
                allUVs.Add(new Vector2(1, 1));
                allUVs.Add(new Vector2(1, 0));

                // 三角形索引（兩個三角形組成四邊形）
                allTriangles.Add(vertexIndex);
                allTriangles.Add(vertexIndex + 1);
                allTriangles.Add(vertexIndex + 2);

                allTriangles.Add(vertexIndex);
                allTriangles.Add(vertexIndex + 2);
                allTriangles.Add(vertexIndex + 3);

                vertexIndex += 4;
            }
        }

        // 更新 Mesh（批量更新以提升性能）
        if (allVertices.Count > 0)
        {
            starMesh.Clear();
            starMesh.vertices = allVertices.ToArray();
            starMesh.colors = allColors.ToArray();
            starMesh.uv = allUVs.ToArray();
            starMesh.triangles = allTriangles.ToArray();
            starMesh.RecalculateBounds();
        }
    }

    // 公開方法供外部調用
    public void Play()
    {
        isPlaying = true;
    }

    public void Pause()
    {
        isPlaying = false;
    }

    public void TogglePlayback()
    {
        isPlaying = !isPlaying;
    }

    public void ClearTrails()
    {
        foreach (Star star in stars)
        {
            star.trailPoints.Clear();
            star.trailAlphas.Clear();
        }

        starMesh.Clear();
    }

    public void ResetAnimation()
    {
        ClearTrails();
        progressedRadians = 0f;
        GenerateStars();
    }

    public void SetAdditiveBlending(bool additive)
    {
        if (starMaterial != null)
        {
            if (additive)
            {
                starMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                starMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }
            else
            {
                starMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                starMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
        }
    }

    // 取得當前進度（0-1）
    public float GetProgress()
    {
        return progressedRadians / (Mathf.Deg2Rad * maxArcDegrees);
    }

    // 檢查是否完成
    public bool IsCompleted()
    {
        return progressedRadians >= Mathf.Deg2Rad * maxArcDegrees;
    }

    // 檢查是否正在播放
    public bool IsPlaying()
    {
        return isPlaying;
    }

    void OnDestroy()
    {
        if (starMesh != null)
            DestroyImmediate(starMesh);
    }
}
