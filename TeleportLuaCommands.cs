using UnityEngine;
using PixelCrushers.DialogueSystem;

public class TeleportLuaCommands : MonoBehaviour
{
    void OnEnable()
    {
        Lua.RegisterFunction("TeleportToScene", this, SymbolExtensions.GetMethodInfo(() => TeleportToScene(string.Empty, string.Empty, (double)0)));
        Lua.RegisterFunction("ResetTeleportRule", this, SymbolExtensions.GetMethodInfo(() => ResetTeleportRule(string.Empty)));
        Lua.RegisterFunction("ResetAllTeleportRules", this, SymbolExtensions.GetMethodInfo(() => ResetAllTeleportRules()));
    }

    void OnDisable()
    {
        Lua.UnregisterFunction("TeleportToScene");
        Lua.UnregisterFunction("ResetTeleportRule");
        Lua.UnregisterFunction("ResetAllTeleportRules");
    }

    public void TeleportToScene(string sceneName, string spawnId, double delay)
    {
        if (QuestSceneTeleporter.Instance != null)
        {
            QuestSceneTeleporter.Instance.TeleportToScene(sceneName, spawnId, (float)delay);
        }
        else
        {
            Debug.LogError("[TeleportLuaCommands] QuestSceneTeleporter.Instance ¤£¦s¦b¡I");
        }
    }

    public void ResetTeleportRule(string questName)
    {
        if (QuestSceneTeleporter.Instance != null)
        {
            QuestSceneTeleporter.Instance.ResetTeleportRule(questName);
        }
    }

    public void ResetAllTeleportRules()
    {
        if (QuestSceneTeleporter.Instance != null)
        {
            QuestSceneTeleporter.Instance.ResetAllTeleportRules();
        }
    }
}
