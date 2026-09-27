using System.Collections.Generic;
using UnityEngine;

public class GalaxyRings : MonoBehaviour
{
    [Header("Ring Settings")]
    public int ringCount = 7;
    public float fixedRadius = 120f;
    public GameObject dustPrefab;

    [Header("Position & Rendering")]
    public Vector3 centerOffset = Vector3.zero;
    public float zOffset = 0f;

    [Header("Particle Settings")]
    public float minParticleSize = 0.05f;
    public float maxParticleSize = 0.1f;
    [Range(0f, 1f)]
    public float wobbleStrength = 0.03f;

    [Header("Color & Glow")]
    [Range(0f, 2f)]
    public float glowIntensity = 1.2f;
    [Range(0f, 1f)]
    public float minBrightness = 0.7f;
    [Range(0f, 1f)]
    public float maxBrightness = 1f;

    [Header("Trail Settings")]
    public bool enableTrails = true;
    public float minTrailTime = 0.5f;
    public float maxTrailTime = 2f;
    public float trailStartWidth = 0.1f;
    public float trailEndWidth = 0.01f;

    private List<Ring> rings = new List<Ring>();
    private Transform particleContainer;

    void Start()
    {
        if (dustPrefab == null)
        {
            Debug.LogError("GalaxyRings: dustPrefab 未赋值！");
            enabled = false;
            return;
        }

        GameObject container = new GameObject("ParticleContainer");
        particleContainer = container.transform;
        particleContainer.SetParent(transform);
        particleContainer.localPosition = centerOffset;
        particleContainer.localRotation = Quaternion.identity;
        particleContainer.localScale = Vector3.one;

        for (int i = 0; i < ringCount; i++)
        {
            Ring r = new Ring(Vector3.zero, fixedRadius, Mathf.RoundToInt(fixedRadius * 3),
                Random.Range(0.8f, 1.4f), dustPrefab, zOffset, particleContainer,
                enableTrails, minTrailTime, maxTrailTime, trailStartWidth, trailEndWidth,
                minParticleSize, maxParticleSize, wobbleStrength,
                glowIntensity, minBrightness, maxBrightness);
            rings.Add(r);
        }
    }

    void Update()
    {
        if (particleContainer != null)
        {
            particleContainer.localPosition = centerOffset;
        }

        foreach (var r in rings)
        {
            r.Update();
        }
    }

    void OnDestroy()
    {
        foreach (var r in rings)
        {
            r.Cleanup();
        }

        if (particleContainer != null)
        {
            Destroy(particleContainer.gameObject);
        }
    }
}

public class Ring
{
    public Vector3 center;
    public float radius;
    public float speed;
    public List<Dust> particles = new List<Dust>();
    private float zOffset;
    private bool enableTrails;
    private float minTrailTime;
    private float maxTrailTime;
    private float trailStartWidth;
    private float trailEndWidth;
    private float minParticleSize;
    private float maxParticleSize;
    private float wobbleStrength;
    private float glowIntensity;
    private float minBrightness;
    private float maxBrightness;

    public Ring(Vector3 center, float radius, int count, float speed,
        GameObject dustPrefab, float zOffset, Transform parentTransform,
        bool enableTrails, float minTrailTime,
        float maxTrailTime, float trailStartWidth, float trailEndWidth,
        float minParticleSize, float maxParticleSize, float wobbleStrength,
        float glowIntensity, float minBrightness, float maxBrightness)
    {
        this.center = center;
        this.radius = radius;
        this.speed = speed;
        this.zOffset = zOffset;
        this.enableTrails = enableTrails;
        this.minTrailTime = minTrailTime;
        this.maxTrailTime = maxTrailTime;
        this.trailStartWidth = trailStartWidth;
        this.trailEndWidth = trailEndWidth;
        this.minParticleSize = minParticleSize;
        this.maxParticleSize = maxParticleSize;
        this.wobbleStrength = wobbleStrength;
        this.glowIntensity = glowIntensity;
        this.minBrightness = minBrightness;
        this.maxBrightness = maxBrightness;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2);
            Dust d = new Dust(this, angle, dustPrefab, zOffset, parentTransform,
                enableTrails, minTrailTime, maxTrailTime, trailStartWidth, trailEndWidth,
                minParticleSize, maxParticleSize, wobbleStrength,
                glowIntensity, minBrightness, maxBrightness);
            particles.Add(d);
        }
    }

    public void Update()
    {
        foreach (var p in particles)
        {
            p.Update();
        }
    }

    public void Cleanup()
    {
        foreach (var p in particles)
        {
            if (p.dustGO != null)
                Object.Destroy(p.dustGO);
        }
        particles.Clear();
    }
}

public class Dust
{
    private Ring parent;
    private float angle;
    private float t;
    private float speed;
    private float size;
    private float life;
    private float dist;
    private float zOffset;
    private float minSize;
    private float maxSize;
    private float minTrailTime;
    private float maxTrailTime;
    private float wobbleStrength;
    private float glowIntensity;
    private float minBrightness;
    private float maxBrightness;
    private float baseBrightness;
    public GameObject dustGO;
    private SpriteRenderer sr;
    private TrailRenderer trail;
    private bool isFirstFrame;

    public Dust(Ring parent, float angle, GameObject prefab, float zOffset, Transform parentTransform,
        bool enableTrails, float minTrailTime, float maxTrailTime,
        float trailStartWidth, float trailEndWidth,
        float minParticleSize, float maxParticleSize, float wobbleStrength,
        float glowIntensity, float minBrightness, float maxBrightness)
    {
        this.parent = parent;
        this.angle = angle;
        this.zOffset = zOffset;
        this.minSize = minParticleSize;
        this.maxSize = maxParticleSize;
        this.minTrailTime = minTrailTime;
        this.maxTrailTime = maxTrailTime;
        this.wobbleStrength = wobbleStrength;
        this.glowIntensity = glowIntensity;
        this.minBrightness = minBrightness;
        this.maxBrightness = maxBrightness;

        dustGO = GameObject.Instantiate(prefab, parentTransform);
        sr = dustGO.GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            sr.material = new Material(Shader.Find("Sprites/Default"));
        }

        if (enableTrails)
        {
            trail = dustGO.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = dustGO.AddComponent<TrailRenderer>();
            }

            trail.time = Random.Range(minTrailTime, maxTrailTime);
            trail.startWidth = trailStartWidth;
            trail.endWidth = trailEndWidth;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.minVertexDistance = 0.05f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = Color.white;
            trail.endColor = new Color(1, 1, 1, 0);
            trail.emitting = false;
        }

        Reset();
        isFirstFrame = true;
    }

    private void Reset()
    {
        speed = Random.Range(0.002f, 0.008f) * parent.speed;
        t = Random.Range(0f, 1000f);
        size = Random.Range(minSize, maxSize);
        life = Random.Range(90f, 260f);
        dist = Random.Range(parent.radius * 0.5f, parent.radius * 1.5f);
        baseBrightness = Random.Range(minBrightness, maxBrightness);
        isFirstFrame = true;

        if (trail != null)
        {
            trail.time = Random.Range(minTrailTime, maxTrailTime);
        }
    }

    public void Update()
    {
        angle += speed;
        t += 0.01f;

        float wobble = Mathf.Sin(angle * 3f + Mathf.PerlinNoise(t, t) * 3f) * dist * wobbleStrength;
        float x = Mathf.Cos(angle) * (dist + wobble);
        float y = Mathf.Sin(angle) * (dist + wobble);
        float z = zOffset;

        dustGO.transform.localPosition = new Vector3(x, y, z);
        dustGO.transform.localScale = Vector3.one * size;

        if (isFirstFrame && trail != null)
        {
            trail.Clear();
            trail.emitting = true;
            isFirstFrame = false;
        }

        life--;
        float alpha = Mathf.Lerp(0f, 0.9f, life / 260f);

        float glowPulse = Mathf.Sin(t * 2f) * 0.15f;
        float brightness = Mathf.Clamp01(baseBrightness + glowPulse);

        Color particleColor = Color.white * brightness * glowIntensity;
        particleColor.a = alpha;
        sr.color = particleColor;

        if (trail != null)
        {
            Color trailStart = Color.white * brightness * glowIntensity;
            trailStart.a = alpha * 0.8f;
            Color trailEnd = Color.white * brightness * glowIntensity;
            trailEnd.a = 0f;
            trail.startColor = trailStart;
            trail.endColor = trailEnd;
        }

        if (life <= 0) Reset();
    }
}
