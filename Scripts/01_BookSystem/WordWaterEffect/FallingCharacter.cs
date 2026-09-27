using UnityEngine;
using TMPro;

public class FallingCharacter : MonoBehaviour
{
    private float currentSpeed;
    private float gravity;
    private float maxFallDistance;
    private float dragCoefficient;
    private float fadeStartSpeed;
    private float fadeEndSpeed;
    private float maxLifetime;
    private float destroyYPosition;

    private float startYPosition;
    private float currentLifetime;
    private TextMeshProUGUI textComponent;
    private Color originalColor;

    public void Initialize(
        float initialSpeed,
        float gravityStrength,
        float fallDistance,
        float drag,
        float fadeStart,
        float fadeEnd,
        float lifetime,
        float destroyY)
    {
        currentSpeed = initialSpeed;
        gravity = gravityStrength;
        maxFallDistance = fallDistance;
        dragCoefficient = drag;
        fadeStartSpeed = fadeStart;
        fadeEndSpeed = fadeEnd;
        maxLifetime = lifetime;
        destroyYPosition = destroyY;

        textComponent = GetComponent<TextMeshProUGUI>();
        originalColor = textComponent.color;

        RectTransform rectTransform = GetComponent<RectTransform>();
        startYPosition = rectTransform.anchoredPosition.y;

        currentLifetime = 0f;
    }

    void Update()
    {
        currentLifetime += Time.deltaTime;

        if (currentLifetime >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        UpdateMovement();
        UpdateTransparency();
        CheckDestroy();
    }

    void UpdateMovement()
    {
        currentSpeed += gravity * Time.deltaTime;

        currentSpeed *= (1f - dragCoefficient * Time.deltaTime);

        currentSpeed = Mathf.Max(currentSpeed, 0f);

        RectTransform rectTransform = GetComponent<RectTransform>();
        Vector2 currentPos = rectTransform.anchoredPosition;
        currentPos.y -= currentSpeed * Time.deltaTime * 100f;
        rectTransform.anchoredPosition = currentPos;

        float distanceFallen = startYPosition - currentPos.y;
        if (distanceFallen >= maxFallDistance)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, 0f, Time.deltaTime * 5f);
        }
    }

    void UpdateTransparency()
    {
        float alpha = 1f;

        if (currentSpeed <= fadeStartSpeed)
        {
            if (currentSpeed <= fadeEndSpeed)
            {
                alpha = 0f;
            }
            else
            {
                float fadeRange = fadeStartSpeed - fadeEndSpeed;
                float currentFade = currentSpeed - fadeEndSpeed;
                alpha = currentFade / fadeRange;
            }
        }

        Color newColor = originalColor;
        newColor.a = alpha;
        textComponent.color = newColor;

        if (alpha <= 0.01f)
        {
            Destroy(gameObject);
        }
    }

    void CheckDestroy()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform.anchoredPosition.y < destroyYPosition)
        {
            Destroy(gameObject);
        }
    }
}
