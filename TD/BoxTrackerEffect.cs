using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class BoxTrackerEffect : MonoBehaviour
{
    [Header("Input")]
    public Camera sourceCamera;            // 若留空自動抓本物件上的 Camera
    public int maskWidth = 160;
    public int maskHeight = 90;

    [Header("Compute")]
    public ComputeShader frameDiff;        // 指到 FrameDiff.compute
    [Range(0.01f, 0.3f)] public float threshold = 0.08f;

    [Header("Filter")]
    public int minBlobPixels = 20;         // 小於此像素數的雜點丟棄
    public int maxBoxes = 15;              // 最多顯示方框數
    public float boxLerp = 12f;            // 位置/尺寸平滑強度（越大越跟手）

    [Header("Style")]
    public Color lineColor = new Color(0.6f, 0.9f, 1f, 1f);
    public float cornerJitter = 3f;        // 角點抖動像素
    public float dashSpeed = 3f;           // 破折動態速度
    public bool drawCornerDots = true;
    public bool drawInterLinks = true;
    public float linkDistance = 250f;      // 中心距離在此以內才連線

    [Header("Line Style")]
    public int borderWidth = 1;
    public int linkLineWidth = 1;
    public int cornerDotSize = 2;      // 改為2像素，更精細

    // 🎯 NEW: 1像素線條材質
    private Texture2D pixelTexture;
    private Material pixelMaterial;

    // 內部資源
    RenderTexture currRT, prevRT;          // 來源影格（大）
    RenderTexture maskRT;                  // 小遮罩（RInt）
    int kernel;

    // CPU 端遮罩緩衝
    int[] cpuMask;

    // 方框列表
    struct TrackedBox
    {
        public Rect r;         // 原始矩形（像素座標）
        public Rect rSmoothed; // 平滑後矩形
        public Vector2 center; // 中心
    }
    readonly List<TrackedBox> boxes = new();

    // GL 畫線材質
    Material glMat;

    void Awake()
    {
        if (!sourceCamera) sourceCamera = GetComponent<Camera>();
        if (!frameDiff)
        {
           
            enabled = false;
            return;
        }

        // 檢查 Compute Shader 是否有效
        if (!frameDiff.HasKernel("CSMain"))
        {
            
            enabled = false;
            return;
        }

        // 取得 kernel
        kernel = frameDiff.FindKernel("CSMain");
        if (kernel < 0)
        {
            
            enabled = false;
            return;
        }

       

        // 其餘初始化程式碼...
        int w = sourceCamera.pixelWidth;
        int h = sourceCamera.pixelHeight;

        currRT = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        prevRT = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        currRT.Create(); prevRT.Create();

        // 建立小遮罩，使用 RInt (R32_SInt)
        maskRT = new RenderTexture(maskWidth, maskHeight, 0, RenderTextureFormat.RInt)
        {
            enableRandomWrite = true
        };
        maskRT.Create();

        cpuMask = new int[maskWidth * maskHeight];

        // GL 繪製材質
        glMat = new Material(Shader.Find("Hidden/Internal-Colored"));
        glMat.hideFlags = HideFlags.HideAndDontSave;
        glMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        glMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        glMat.SetInt("_Cull", (int)CullMode.Off);
        glMat.SetInt("_ZWrite", 0);

        // 先渲染一幀到 prev
        sourceCamera.targetTexture = prevRT;
        sourceCamera.Render();
        sourceCamera.targetTexture = null;
        // 🎯 NEW: 使用 Camera 事件而不是 OnPostRender
        Camera.onPostRender += OnCameraPostRender;
        CreatePixelTexture();
    }

    void CreatePixelTexture()
    {
        // 創建1x1白色texture
        pixelTexture = new Texture2D(1, 1);
        pixelTexture.SetPixel(0, 0, Color.white);
        pixelTexture.Apply();

        // 創建Unlit材質來繪製線條
        pixelMaterial = new Material(Shader.Find("Unlit/Transparent"));
        pixelMaterial.mainTexture = pixelTexture;
    }


    void Update()
    {
        // 安全檢查
        if (!frameDiff || kernel < 0) return;

        // 渲染當前幀到 currRT
        sourceCamera.targetTexture = currRT;
        sourceCamera.Render();
        sourceCamera.targetTexture = null;

        // 設定 compute
        frameDiff.SetTexture(kernel, "_CurrentTex", currRT);
        frameDiff.SetTexture(kernel, "_PrevTex", prevRT);
        frameDiff.SetTexture(kernel, "_OutMask", maskRT);
        frameDiff.SetFloat("_Threshold", threshold);
        frameDiff.SetInts("_OutSize", maskWidth, maskHeight);
        frameDiff.SetFloats("_SourceSize", currRT.width, currRT.height);

        int gx = Mathf.CeilToInt(maskWidth / 8f);
        int gy = Mathf.CeilToInt(maskHeight / 8f);

        try
        {
            frameDiff.Dispatch(kernel, gx, gy, 1);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Compute Shader Dispatch 失敗: {e.Message}");
            enabled = false;
            return;
        }

        // 儲存當前 RT 尺寸
        int currentRTWidth = currRT.width;
        int currentRTHeight = currRT.height;

        AsyncGPUReadback.Request(maskRT, 0, request =>
        {
            if (!this || !gameObject || request.hasError) return;
            if (!currRT || !prevRT || !maskRT) return;

            var data = request.GetData<int>();

            // 🎯 NEW: 調試 - 計算偵測到的像素數
            int detectedPixels = 0;
            for (int i = 0; i < data.Length && i < cpuMask.Length; i++)
            {
                cpuMask[i] = data[i];
                if (data[i] > 0) detectedPixels++;
            }

            var list = ExtractBoundingBoxes(cpuMask, maskWidth, maskHeight, minBlobPixels, maxBoxes);

          

            // 對應到像素空間的方框
            boxes.Clear();
            foreach (var m in list)
            {
                Rect r = new Rect(
                    m.xMin * (currentRTWidth / (float)maskWidth),
                    m.yMin * (currentRTHeight / (float)maskHeight),
                    m.width * (currentRTWidth / (float)maskWidth),
                    m.height * (currentRTHeight / (float)maskHeight)
                );

                r.y = currentRTHeight - r.y - r.height;

                boxes.Add(new TrackedBox
                {
                    r = r,
                    rSmoothed = r,
                    center = r.center
                });

              
            }
        });

        Graphics.Blit(currRT, prevRT);
    }

    // 🎯 NEW: 替代 OnPostRender 的方法
    void OnCameraPostRender(Camera cam)
    {
        // 只在正確的攝影機上繪製
        if (cam != sourceCamera) return;

        Debug.Log($"[BoxTracker] OnCameraPostRender 被調用，準備繪製 {boxes.Count} 個追蹤框");

        if (!glMat || boxes.Count == 0) return;

        float dt = Time.deltaTime;
        float anim = Time.time * dashSpeed;

        // 🎯 修正：確保正確的 GL 狀態
        GL.PushMatrix();
        glMat.SetPass(0);

        // 使用螢幕像素座標系
        GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

        GL.Begin(GL.LINES);
        GL.Color(lineColor);

        // 🎯 NEW: 強制繪製測試框來驗證 GL 是否工作
        if (Input.GetKey(KeyCode.T))
        {
            Rect testRect = new Rect(100, 100, 200, 150);
            DrawRectDashed(testRect, anim, cornerJitter, drawCornerDots);
            Debug.Log("[BoxTracker] 繪製測試框");
        }

        // 平滑繪製追蹤框
        for (int i = 0; i < boxes.Count; i++)
        {
            var b = boxes[i];

            float k = 1 - Mathf.Exp(-boxLerp * dt);
            b.rSmoothed.x = Mathf.Lerp(b.rSmoothed.x, b.r.x, k);
            b.rSmoothed.y = Mathf.Lerp(b.rSmoothed.y, b.r.y, k);
            b.rSmoothed.width = Mathf.Lerp(b.rSmoothed.width, b.r.width, k);
            b.rSmoothed.height = Mathf.Lerp(b.rSmoothed.height, b.r.height, k);
            b.center = b.rSmoothed.center;

            // 🎯 NEW: 調試每個追蹤框的繪製
            Debug.Log($"[BoxTracker] 繪製追蹤框: {b.rSmoothed}");
            DrawRectDashed(b.rSmoothed, anim, cornerJitter, drawCornerDots);

            boxes[i] = b;
        }

        // 追蹤框間連線
        if (drawInterLinks)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    if ((boxes[i].center - boxes[j].center).sqrMagnitude < linkDistance * linkDistance)
                        DrawLineDashed(boxes[i].center, boxes[j].center, anim + 0.17f);
                }
            }
        }

        GL.End();
        GL.PopMatrix();
    }



    // ── 視覺工具 ───────────────────────────────────────────────

    void DrawRectDashed(Rect r, float anim, float jitter, bool drawDots)
    {
        Vector2 a = new Vector2(r.xMin, r.yMin);
        Vector2 b = new Vector2(r.xMax, r.yMin);
        Vector2 c = new Vector2(r.xMax, r.yMax);
        Vector2 d = new Vector2(r.xMin, r.yMax);

        a += J(a, jitter); b += J(b, jitter); c += J(c, jitter); d += J(d, jitter);

        DrawLineDashed(a, b, anim + 0.00f);
        DrawLineDashed(b, c, anim + 0.31f);
        DrawLineDashed(c, d, anim + 0.62f);
        DrawLineDashed(d, a, anim + 0.93f);

        if (drawDots)
        {
            DrawCross(a, 4);
            DrawCross(b, 4);
            DrawCross(c, 4);
            DrawCross(d, 4);
        }
    }

    void DrawLineDashed(Vector2 p0, Vector2 p1, float anim)
    {
        float len = Vector2.Distance(p0, p1);
        int segments = Mathf.Max(1, Mathf.CeilToInt(len / 12f));
        Vector2 dir = (p1 - p0) / segments;

        // 破折：畫一段、空一段，並用 anim 做相位位移
        for (int i = 0; i < segments; i++)
        {
            float phase = Mathf.Repeat(anim + i * 0.15f, 2f);
            if (phase < 1f) // 畫段
            {
                Vector2 s = p0 + dir * i;
                Vector2 e = p0 + dir * (i + 0.6f);
                GL.Vertex3(s.x, s.y, 0);
                GL.Vertex3(e.x, e.y, 0);
            }
        }
    }

    void DrawCross(Vector2 p, float half)
    {
        GL.Vertex3(p.x - half, p.y, 0); GL.Vertex3(p.x + half, p.y, 0);
        GL.Vertex3(p.x, p.y - half, 0); GL.Vertex3(p.x, p.y + half, 0);
    }

    Vector2 J(Vector2 p, float amp)
    {
        if (amp <= 0f) return Vector2.zero;
        float jx = (Mathf.PerlinNoise(p.x * 0.01f, Time.time * 0.8f) - 0.5f) * 2f * amp;
        float jy = (Mathf.PerlinNoise(p.y * 0.01f, Time.time * 1.1f) - 0.5f) * 2f * amp;
        return new Vector2(jx, jy);
    }

    // ── 連通區 → 外接矩形（簡易 4-connected BFS） ────────────────
    List<RectInt> ExtractBoundingBoxes(int[] mask, int w, int h, int minPixels, int maxCount)
    {
        List<RectInt> result = new();
        if (mask == null || mask.Length != w * h) return result;

        int[] labels = new int[w * h];
        int nextLabel = 1;
        Queue<int> q = new Queue<int>();

        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] == 0 || labels[i] != 0) continue;

            int label = nextLabel++;
            int minx = w, miny = h, maxx = 0, maxy = 0;
            int count = 0;

            q.Enqueue(i);
            labels[i] = label;

            while (q.Count > 0)
            {
                int idx = q.Dequeue();
                int x = idx % w, y = idx / w;
                count++;
                if (x < minx) minx = x; if (x > maxx) maxx = x;
                if (y < miny) miny = y; if (y > maxy) maxy = y;

                TryPush(x + 1, y);
                TryPush(x - 1, y);
                TryPush(x, y + 1);
                TryPush(x, y - 1);

                void TryPush(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;
                    int nidx = ny * w + nx;
                    if (mask[nidx] == 0 || labels[nidx] != 0) return;
                    labels[nidx] = label;
                    q.Enqueue(nidx);
                }
            }

            if (count >= minPixels)
            {
                result.Add(new RectInt(minx, miny, (maxx - minx + 1), (maxy - miny + 1)));
                if (result.Count >= maxCount) break;
            }
        }

        return result;
    }

    void OnDestroy()
    {
        // 🎯 NEW: 在銷毀前等待所有異步操作完成
        // 注意：這不是完美的解決方案，但能減少錯誤發生
        // 🎯 NEW: 清理事件
        Camera.onPostRender -= OnCameraPostRender;
        // 釋放 RenderTexture
        if (currRT)
        {
            currRT.Release();
            currRT = null;
        }
        if (prevRT)
        {
            prevRT.Release();
            prevRT = null;
        }
        if (maskRT)
        {
            maskRT.Release();
            maskRT = null;
        }

        // 釋放材質
        if (glMat)
        {
            DestroyImmediate(glMat);
            glMat = null;
        }

        // 清空陣列
        cpuMask = null;
        boxes?.Clear();
        if (pixelTexture) DestroyImmediate(pixelTexture);
        if (pixelMaterial) DestroyImmediate(pixelMaterial);
    }

    void OnDisable()
    {
        // 🎯 NEW: 當組件被禁用時也進行清理
        // 這能幫助在場景切換時避免異步回調問題
    }
    void OnGUI()
    {
        if (boxes.Count > 0)
        {
            // 🎯 NEW: 使用Graphics.DrawTexture繪製像素級線條
            foreach (var box in boxes)
            {
                var rect = box.rSmoothed;

                // 設定顏色
                GUI.color = lineColor;

                // 🎯 真正的1像素邊框
                DrawPixelLine(new Vector2(rect.x, rect.y), new Vector2(rect.x + rect.width, rect.y), borderWidth); // 上邊
                DrawPixelLine(new Vector2(rect.x, rect.y + rect.height), new Vector2(rect.x + rect.width, rect.y + rect.height), borderWidth); // 下邊
                DrawPixelLine(new Vector2(rect.x, rect.y), new Vector2(rect.x, rect.y + rect.height), borderWidth); // 左邊
                DrawPixelLine(new Vector2(rect.x + rect.width, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height), borderWidth); // 右邊

                // 🎯 角落點（真正的像素點）
                if (drawCornerDots)
                {
                    DrawPixelDot(new Vector2(rect.x, rect.y), cornerDotSize);
                    DrawPixelDot(new Vector2(rect.x + rect.width, rect.y), cornerDotSize);
                    DrawPixelDot(new Vector2(rect.x, rect.y + rect.height), cornerDotSize);
                    DrawPixelDot(new Vector2(rect.x + rect.width, rect.y + rect.height), cornerDotSize);
                }
            }

            // 🎯 框之間的連線（真正的像素級）
            if (drawInterLinks && boxes.Count > 1)
            {
                for (int i = 0; i < boxes.Count; i++)
                {
                    for (int j = i + 1; j < boxes.Count; j++)
                    {
                        Vector2 center1 = boxes[i].center;
                        Vector2 center2 = boxes[j].center;
                        float distance = Vector2.Distance(center1, center2);

                        if (distance <= linkDistance)
                        {
                            DrawPixelLine(center1, center2, linkLineWidth);
                        }
                    }
                }
            }

            GUI.color = Color.white;
        }

        /*// 調試信息...
        GUI.Box(new Rect(10, 10, 450, 130), "");
        GUI.Label(new Rect(15, 15, 440, 20), $"追蹤框數量: {boxes.Count}");
        GUI.Label(new Rect(15, 35, 440, 20), $"真正像素級線條 | 邊框: {borderWidth}px | 連線: {linkLineWidth}px");

        if (GUI.Button(new Rect(15, 115, 100, 20), "添加測試框"))
        {
            boxes.Clear();
            boxes.Add(new TrackedBox
            {
                r = new Rect(Screen.width * 0.4f, Screen.height * 0.4f, 80, 60),
                rSmoothed = new Rect(Screen.width * 0.4f, Screen.height * 0.4f, 80, 60),
                center = new Vector2(Screen.width * 0.4f + 40, Screen.height * 0.4f + 30)
            });
            boxes.Add(new TrackedBox
            {
                r = new Rect(Screen.width * 0.6f, Screen.height * 0.6f, 80, 60),
                rSmoothed = new Rect(Screen.width * 0.6f, Screen.height * 0.6f, 80, 60),
                center = new Vector2(Screen.width * 0.6f + 40, Screen.height * 0.6f + 30)
            });
        }*/
    }

    // 🎯 NEW: 真正的像素級線條繪製
    void DrawPixelLine(Vector2 start, Vector2 end, int width)
    {
        Vector2 direction = end - start;
        float length = direction.magnitude;

        if (length < 0.1f) return; // 太短的線條跳過

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 使用Graphics.DrawTexture繪製
        Matrix4x4 oldMatrix = GUI.matrix;

        // 移動到起始點並旋轉
        GUIUtility.RotateAroundPivot(angle, start);

        // 繪製線條（真正的像素寬度）
        Rect lineRect = new Rect(start.x, start.y - width * 0.5f, length, width);
        Graphics.DrawTexture(lineRect, pixelTexture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, GUI.color);

        GUI.matrix = oldMatrix;
    }

    // 🎯 NEW: 真正的像素級點繪製
    void DrawPixelDot(Vector2 position, int size)
    {
        Rect dotRect = new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size);
        Graphics.DrawTexture(dotRect, pixelTexture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, GUI.color);
    }


    // 🎯 NEW: GUI 線條繪製輔助方法（添加在 OnGUI 方法後面）
    void DrawGUILine(Vector2 pointA, Vector2 pointB, float width)
    {
        // 計算線條的角度和長度
        Vector2 direction = pointB - pointA;
        float length = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 使用 GUI 矩形來模擬線條
        Matrix4x4 matrix = GUI.matrix;

        // 移動到起始點
        GUIUtility.RotateAroundPivot(angle, pointA);

        // 繪製水平線條
        GUI.Box(new Rect(pointA.x, pointA.y - width / 2, length, width), "");

        // 恢復矩陣
        GUI.matrix = matrix;
    }

}
