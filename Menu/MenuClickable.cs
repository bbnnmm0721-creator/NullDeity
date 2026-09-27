using TMPro;
using UnityEngine;
using UnityEngine.Events;

public enum MenuAction
{
    StartGame,
    ContinueGame, // 新增
    QuitGame,
    Custom
}


[RequireComponent(typeof(Collider))]
public class MenuClickable : MonoBehaviour
{
    [Header("菜单动作")]
    public MenuAction menuAction = MenuAction.StartGame;

    [Header("悬停描述")]
    [TextArea(2, 4)]
    public string hoverDescription = "点击开始游戏";

    [Header("专属文字组件")]
    [Tooltip("这个物件专用的 TextMeshPro 组件")]
    public TMP_Text dedicatedText;

    [Header("悬停效果")]
    public Color hoverColor = Color.yellow;
    public float hoverScale = 1.1f;
    public float hoverAnimSpeed = 5f;

    [Header("Collider 设置")]
    [Tooltip("自动调整 BoxCollider 大小以匹配 Sprite")]
    public bool autoFitColliderToSprite = true;

    [Tooltip("Collider 的深度（Z轴厚度）")]
    public float colliderDepth = 0.1f;

    [Header("自定义事件")]
    public UnityEvent onClickEvent;
    public UnityEvent onHoverEnterEvent;
    public UnityEvent onHoverExitEvent;

    private Color originalColor;
    private Vector3 originalScale;
    private SpriteRenderer spriteRenderer;
    private Renderer objectRenderer;
    private bool isHovering = false;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        objectRenderer = GetComponent<Renderer>();

        if (objectRenderer != null)
        {
            originalColor = objectRenderer.material.color;
        }
        else if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        originalScale = transform.localScale;

        if (autoFitColliderToSprite)
        {
            SetupCollider();
        }
    }

    void SetupCollider()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            Debug.LogWarning($"[MenuClickable] {gameObject.name} 没有 SpriteRenderer 或 Sprite！");
            return;
        }

        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
            Debug.Log($"[MenuClickable] 为 {gameObject.name} 自动添加了 BoxCollider");
        }

        Bounds spriteBounds = spriteRenderer.sprite.bounds;
        boxCollider.center = spriteBounds.center;
        boxCollider.size = new Vector3(
            spriteBounds.size.x,
            spriteBounds.size.y,
            colliderDepth
        );

        Debug.Log($"[MenuClickable] {gameObject.name} BoxCollider 已设置: size={boxCollider.size}");
    }

    void Update()
    {
        if (isHovering)
        {
            AnimateHover();
        }
        else
        {
            AnimateNormal();
        }
    }

    public void OnHoverEnter()
    {
        isHovering = true;
        onHoverEnterEvent?.Invoke();
    }

    public void OnHoverExit()
    {
        isHovering = false;
        onHoverExitEvent?.Invoke();
    }

    public void OnClick()
    {
        onClickEvent?.Invoke();
    }

    void AnimateHover()
    {
        Color targetColor = hoverColor;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(
                spriteRenderer.color,
                targetColor,
                Time.deltaTime * hoverAnimSpeed
            );
        }
        else if (objectRenderer != null)
        {
            objectRenderer.material.color = Color.Lerp(
                objectRenderer.material.color,
                targetColor,
                Time.deltaTime * hoverAnimSpeed
            );
        }

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            originalScale * hoverScale,
            Time.deltaTime * hoverAnimSpeed
        );
    }

    void AnimateNormal()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(
                spriteRenderer.color,
                originalColor,
                Time.deltaTime * hoverAnimSpeed
            );
        }
        else if (objectRenderer != null)
        {
            objectRenderer.material.color = Color.Lerp(
                objectRenderer.material.color,
                originalColor,
                Time.deltaTime * hoverAnimSpeed
            );
        }

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            originalScale,
            Time.deltaTime * hoverAnimSpeed
        );
    }

    void OnDestroy()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        else if (objectRenderer != null && objectRenderer.material != null)
        {
            objectRenderer.material.color = originalColor;
        }
    }

    void OnDrawGizmosSelected()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
    }
}
