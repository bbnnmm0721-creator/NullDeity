using UnityEngine;

/// <summary>
/// Syncs the character's current sprite texture to the Dither override material
/// each frame so the dither silhouette matches the animated sprite shape.
/// </summary>
public class DitherSpriteSync : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Material ditherMaterial;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private Sprite _lastSprite;

    void LateUpdate()
    {
        if (spriteRenderer == null || ditherMaterial == null) return;

        Sprite current = spriteRenderer.sprite;
        if (current == _lastSprite) return;         // 只在 sprite 切換時更新，避免每幀 SetTexture

        _lastSprite = current;
        ditherMaterial.SetTexture(MainTexId, current != null ? current.texture : null);
    }
}
