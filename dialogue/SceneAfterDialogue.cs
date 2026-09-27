using UnityEngine;
using System.Collections;

/// <summary>
/// 掛在有對話的 NPC 或觸發物件上。
/// 搭配同物件的 Dialogue System Trigger（OnConversationEnd）呼叫 Trigger()。
/// </summary>
public class SceneAfterDialogue : MonoBehaviour
{
    [Header("目標場景")]
    public string targetScene;
    public string spawnId = "Default";

    [Header("淡黑時長（秒）")]
    public float fadeDuration = 3f;

    /// <summary>由 Dialogue System Trigger 的 OnConversationEnd UnityEvent 呼叫。</summary>
    public void Trigger() => StartCoroutine(Do());

    IEnumerator Do()
    {
        GlobalSceneTransition.FadeToColor(Color.black, fadeDuration);

        yield return new WaitForSeconds(fadeDuration);
        GlobalSceneTransition.GoStayBlack(targetScene, spawnId);
    }
}
