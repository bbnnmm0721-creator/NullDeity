using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CharacterRainEffect : MonoBehaviour
{
    [Header("字符與字體")]
    public string characterSet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public TMP_FontAsset font;
    [Range(1, 120)]
    public float fontSize = 50f;

    [Header("顏色設定")]
    public Color mainColor = Color.black;
    public Color blurColor = new Color(0f, 0f, 0f, 0.25f);

    [Header("多層模糊效果")]
    [Tooltip("啟用多層模糊")]
    public bool enableBlur = true;
    [Tooltip("模糊層數量（越多越模糊）")]
    [Range(1, 5)]
    public int blurLayerCount = 3;
    [Tooltip("每層模糊的偏移距離")]
    public float blurSpread = 3f;
    [Tooltip("模糊層縮放")]
    [Range(1f, 1.5f)]
    public float blurScale = 1.15f;
    [Tooltip("模糊透明度衰減")]
    [Range(0.5f, 1f)]
    public float blurAlphaDecay = 0.7f;

    [Header("水流列設定")]
    public float xMin = -400f;
    public float xMax = 400f;
    [Tooltip("固定X軸的列數")]
    [Range(1, 100)]
    public int columnCount = 20;
    [Tooltip("是否使用固定列（不勾則完全隨機X）")]
    public bool useFixedColumns = true;
    [Tooltip("列間距隨機偏移範圍（0=均勻，越大越隨機）")]
    [Range(0f, 100f)]
    public float columnSpacingVariation = 30f;

    [Header("生成速度")]
    [Tooltip("每列生成間隔最小值")]
    [Range(0.05f, 1f)]
    public float spawnIntervalMin = 0.1f;
    [Tooltip("每列生成間隔最大值")]
    [Range(0.1f, 2f)]
    public float spawnIntervalMax = 0.4f;

    [Header("下落設定（目標速度）")]
    [Tooltip("目標下落速度（類似終端速度）")]
    [Range(100f, 1000f)]
    public float baseSpeed = 350f;
    [Tooltip("目標速度隨機範圍")]
    [Range(0f, 500f)]
    public float speedVariation = 250f;
    [Tooltip("最大下落距離")]
    [Range(200f, 2000f)]
    public float maxDistance = 800f;
    [Tooltip("距離隨機範圍")]
    [Range(0f, 800f)]
    public float distanceVariation = 400f;

    [Header("水流物理模擬")]
    [Tooltip("加速度（越大，從靜止加速到目標速度越快）")]
    [Range(50f, 3000f)]
    public float acceleration = 1200f;
    [Tooltip("橫向微擺振幅（類似水柱左右抖動）")]
    [Range(0f, 50f)]
    public float horizontalWaveAmplitude = 10f;
    [Tooltip("橫向微擺頻率")]
    [Range(0.1f, 10f)]
    public float horizontalWaveFrequency = 3f;

    [Header("淡出效果")]
    [Tooltip("下落多少距離後開始淡出 (比例 0-1)")]
    [Range(0f, 1f)]
    public float fadeStartRatio = 0.7f;

    [Header("Y軸範圍")]
    public float spawnY = 600f;
    public float destroyY = -600f;

    [Header("渲染層級")]
    [Tooltip("Canvas 排序順序（數字越大顯示越上層）")]
    public int canvasSortOrder = 100;

    private class Column
    {
        public float xPosition;
        public float timer;
        public float interval;
    }

    private class FallingChar
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public List<TextMeshProUGUI> allTexts = new List<TextMeshProUGUI>();
        public TextMeshProUGUI mainText;
        public List<Color> originalColors = new List<Color>();

        public float speed;
        public float targetSpeed;
        public float maxDistance;
        public float startY;
        public float traveledDistance;

        public float baseX;
        public float noiseSeed;
    }

    private Canvas canvas;
    private List<Column> columns = new List<Column>();
    private List<FallingChar> activeChars = new List<FallingChar>();

    private int previousColumnCount;
    private bool previousUseFixedColumns;

    void Start()
    {
        SetupCanvas();
        InitializeColumns();
        previousColumnCount = columnCount;
        previousUseFixedColumns = useFixedColumns;
    }

    void SetupCanvas()
    {
        canvas = GetComponent<Canvas>();
        bool needsNewCanvas = (canvas == null);

        if (needsNewCanvas)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas != canvas)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = canvasSortOrder;
        }
        else if (needsNewCanvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    void InitializeColumns()
    {
        columns.Clear();

        if (useFixedColumns)
        {
            for (int i = 0; i < columnCount; i++)
            {
                float t = (columnCount == 1) ? 0.5f : (float)i / (columnCount - 1);
                float baseXPos = Mathf.Lerp(xMin, xMax, t);

                float randomOffset = Random.Range(-columnSpacingVariation, columnSpacingVariation);
                float xPos = Mathf.Clamp(baseXPos + randomOffset, xMin, xMax);

                Column col = new Column();
                col.xPosition = xPos;
                col.timer = Random.Range(0f, spawnIntervalMax);
                col.interval = Random.Range(spawnIntervalMin, spawnIntervalMax);
                columns.Add(col);
            }

            Debug.Log($"[CharacterRainEffect] Initialized {columnCount} columns");
        }
    }

    void Update()
    {
        if (previousColumnCount != columnCount || previousUseFixedColumns != useFixedColumns)
        {
            InitializeColumns();
            previousColumnCount = columnCount;
            previousUseFixedColumns = useFixedColumns;
            Debug.Log($"[CharacterRainEffect] Columns reinitialized. Count: {columnCount}, UseFixed: {useFixedColumns}");
        }

        UpdateSpawning();
        UpdateFallingCharacters();
    }

    void UpdateSpawning()
    {
        if (useFixedColumns)
        {
            foreach (var col in columns)
            {
                col.timer += Time.deltaTime;

                if (col.timer >= col.interval)
                {
                    SpawnCharacterAt(col.xPosition);
                    col.timer = 0f;
                    col.interval = Random.Range(spawnIntervalMin, spawnIntervalMax);
                }
            }
        }
        else
        {
            float avgInterval = (spawnIntervalMin + spawnIntervalMax) * 0.5f;
            if (Random.value < Time.deltaTime / avgInterval)
            {
                float randomX = Random.Range(xMin, xMax);
                SpawnCharacterAt(randomX);
            }
        }
    }

    void SpawnCharacterAt(float xPos)
    {
        GameObject container = new GameObject("CharDrop");
        container.transform.SetParent(transform, false);

        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(fontSize * 2f, fontSize * 2f);
        containerRect.anchoredPosition = new Vector2(xPos, spawnY);

        char randomChar = characterSet[Random.Range(0, characterSet.Length)];

        FallingChar fc = new FallingChar();
        fc.gameObject = container;
        fc.rectTransform = containerRect;
        fc.baseX = xPos;
        fc.noiseSeed = Random.value * 10f;

        float target = baseSpeed + Random.Range(-speedVariation, speedVariation);
        fc.targetSpeed = Mathf.Max(50f, target);
        fc.speed = 0f;

        if (enableBlur)
        {
            CreateMultiLayerBlur(container, randomChar, fc);
        }

        fc.mainText = CreateMainText(container, randomChar);
        fc.allTexts.Add(fc.mainText);
        fc.originalColors.Add(fc.mainText.color);

        fc.maxDistance = maxDistance + Random.Range(-distanceVariation, distanceVariation);
        fc.startY = spawnY;
        fc.traveledDistance = 0f;

        activeChars.Add(fc);
    }

    void CreateMultiLayerBlur(GameObject parent, char character, FallingChar fc)
    {
        float angleStep = 360f / blurLayerCount;

        for (int i = 0; i < blurLayerCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(
                Mathf.Cos(angle) * blurSpread,
                Mathf.Sin(angle) * blurSpread
            );

            GameObject blurObj = new GameObject($"BlurLayer_{i}");
            blurObj.transform.SetParent(parent.transform, false);

            TextMeshProUGUI blurText = blurObj.AddComponent<TextMeshProUGUI>();
            blurText.font = font;
            blurText.fontSize = fontSize * blurScale;

            Color layerColor = blurColor;
            layerColor.a = blurColor.a * Mathf.Pow(blurAlphaDecay, i);
            blurText.color = layerColor;

            blurText.alignment = TextAlignmentOptions.Center;
            blurText.text = character.ToString();

            RectTransform blurRect = blurObj.GetComponent<RectTransform>();
            blurRect.anchorMin = new Vector2(0.5f, 0.5f);
            blurRect.anchorMax = new Vector2(0.5f, 0.5f);
            blurRect.pivot = new Vector2(0.5f, 0.5f);
            blurRect.anchoredPosition = offset;
            blurRect.sizeDelta = new Vector2(fontSize * 2f, fontSize * 2f);

            fc.allTexts.Add(blurText);
            fc.originalColors.Add(layerColor);
        }
    }

    TextMeshProUGUI CreateMainText(GameObject parent, char character)
    {
        GameObject mainObj = new GameObject("MainLayer");
        mainObj.transform.SetParent(parent.transform, false);

        TextMeshProUGUI mainText = mainObj.AddComponent<TextMeshProUGUI>();
        mainText.font = font;
        mainText.fontSize = fontSize;
        mainText.color = mainColor;
        mainText.alignment = TextAlignmentOptions.Center;
        mainText.text = character.ToString();

        RectTransform mainRect = mainObj.GetComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0.5f, 0.5f);
        mainRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainRect.pivot = new Vector2(0.5f, 0.5f);
        mainRect.anchoredPosition = Vector2.zero;
        mainRect.sizeDelta = new Vector2(fontSize, fontSize);

        return mainText;
    }

    void UpdateFallingCharacters()
    {
        float dt = Time.deltaTime;

        for (int i = activeChars.Count - 1; i >= 0; i--)
        {
            FallingChar fc = activeChars[i];

            if (fc.gameObject == null || fc.rectTransform == null)
            {
                activeChars.RemoveAt(i);
                continue;
            }

            fc.speed = Mathf.MoveTowards(fc.speed, fc.targetSpeed, acceleration * dt);

            Vector2 pos = fc.rectTransform.anchoredPosition;
            pos.y -= fc.speed * dt;

            if (horizontalWaveAmplitude > 0f && horizontalWaveFrequency > 0f)
            {
                float wave = Mathf.Sin((Time.time + fc.noiseSeed) * horizontalWaveFrequency) * horizontalWaveAmplitude;
                pos.x = fc.baseX + wave;
            }

            fc.rectTransform.anchoredPosition = pos;

            fc.traveledDistance = fc.startY - pos.y;

            UpdateCharacterAlpha(fc);

            if (pos.y < destroyY || fc.traveledDistance >= fc.maxDistance)
            {
                Destroy(fc.gameObject);
                activeChars.RemoveAt(i);
            }
        }
    }

    void UpdateCharacterAlpha(FallingChar fc)
    {
        float fadeStartDistance = fc.maxDistance * fadeStartRatio;
        float alpha = 1f;

        if (fc.traveledDistance >= fadeStartDistance)
        {
            float fadeRange = fc.maxDistance - fadeStartDistance;
            float fadeProgress = (fc.traveledDistance - fadeStartDistance) / fadeRange;
            alpha = Mathf.Clamp01(1f - fadeProgress);
        }

        for (int i = 0; i < fc.allTexts.Count; i++)
        {
            if (fc.allTexts[i] != null)
            {
                Color baseColor = fc.originalColors[i];
                Color c = baseColor;
                c.a = baseColor.a * alpha;
                fc.allTexts[i].color = c;
            }
        }
    }
}
