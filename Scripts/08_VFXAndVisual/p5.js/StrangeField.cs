using UnityEngine;
using UnityEngine.UI;

public class StrangeField : MonoBehaviour
{
    [Header("Point settings")]
    public int pointCount = 30000;
    public float speed = 0.4f;
    public float brightness = 0.6f;
    public float warmShift = 0.35f;
    public float spriteWorldSize = 0.06f;
    public GameObject dotPrefab;

    [Header("Rendering")]
    public float zOffset = 10f;

    Transform[] dots;
    float t = 0;

    void Start()
    {
        if (dotPrefab == null)
        {
            Debug.LogError("StrangeField: dotPrefab 未赋值！请在 Inspector 中设置 Dot Prefab。");
            enabled = false;
            return;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 200f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.backgroundColor = Color.black;
        }

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
        t += Mathf.PI / 60f * speed;

        for (int i = 0; i < pointCount; i++)
        {
            float y = i / 799f;
            float k = 5f * Mathf.Cos(i / 48f);
            float e = 5f * Mathf.Cos(y / 9f);
            float d = Mathf.Pow((Mathf.Sqrt(k * k + e * e) / (6f + i % 4)), 4) + 4f;

            float q = k * (3f + e * 0.5f * Mathf.Sin(d * 8f + k / 9f - t))
                    - 3f * Mathf.Sin(k * d / 3f)
                    - ((i & 1) == 0 ? 80f : -80f);

            float c = d - t / 9f + (i % 5);

            float px = (q * Mathf.Sin(c) + 200f);
            float py = (q * Mathf.Cos(c - (i % 2) + (i % 5) * 3 + 7) + 200f);

            dots[i].localPosition = new Vector3(px - 200f, 200f - py, zOffset);

            float shift = 1 - warmShift + Random.value * warmShift;
            float alpha = brightness * Random.Range(0.05f, 0.30f);

            SpriteRenderer sr = dots[i].GetComponent<SpriteRenderer>();
            sr.color = new Color(1f, shift, 1f, alpha);
        }
    }
}
