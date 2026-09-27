using UnityEngine;
using UnityEditor;

public class SparkleItemCreator : MonoBehaviour
{
    [Header("快速建立設定")]
    public int itemID = 101;
    public float activationDistance = 3f;
    public GameObject sparklePrefab;

    [ContextMenu("建立閃光點物品")]
    void CreateSparkleItem()
    {
        // 創建主物件
        GameObject sparkleItem = new GameObject($"SparkleItem_{itemID}");
        sparkleItem.transform.position = transform.position;

        // 添加腳本
        SparkleInteractableItem script = sparkleItem.AddComponent<SparkleInteractableItem>();
        script.itemID = itemID;
        script.activationDistance = activationDistance;

        // 添加碰撞器
        SphereCollider collider = sparkleItem.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 1f;

        // 創建閃光點子物件
        GameObject sparkle = CreateSparkleEffect(sparkleItem.transform);
        script.sparkleObject = sparkle;

        // 創建互動提示
        GameObject hint = CreateInteractionHint(sparkleItem.transform);
        script.interactionHint = hint;

        Debug.Log($"已建立閃光點物品：{sparkleItem.name}");
    }

    GameObject CreateSparkleEffect(Transform parent)
    {
        GameObject sparkle = new GameObject("Sparkle");
        sparkle.transform.SetParent(parent);
        sparkle.transform.localPosition = Vector3.zero;

        // 添加 Sprite Renderer 顯示閃光
        SpriteRenderer sr = sparkle.AddComponent<SpriteRenderer>();

        // 使用簡單的白色圓形作為閃光效果
        sr.sprite = CreateSparkleSprite();
        sr.color = Color.yellow;
        sr.sortingOrder = 10;

        sparkle.SetActive(false);
        return sparkle;
    }

    GameObject CreateInteractionHint(Transform parent)
    {
        GameObject hint = new GameObject("InteractionHint");
        hint.transform.SetParent(parent);
        hint.transform.localPosition = Vector3.up * 0.5f;

        SpriteRenderer sr = hint.AddComponent<SpriteRenderer>();
        sr.sprite = CreateExclamationSprite();
        sr.color = Color.white;
        sr.sortingOrder = 11;

        hint.SetActive(false);
        return hint;
    }

    Sprite CreateSparkleSprite()
    {
        // 創建簡單的圓形紋理
        Texture2D texture = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];

        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                float alpha = Mathf.Clamp01(1f - (distance / 32f));
                colors[y * 64 + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
    }

    Sprite CreateExclamationSprite()
    {
        // 這裡簡化處理，實際使用時你可以載入真正的驚嘆號圖片
        return CreateSparkleSprite();
    }
}
