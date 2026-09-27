using UnityEngine;
using PixelCrushers.DialogueSystem;

public class SheepLuaCommands : MonoBehaviour
{
    void OnEnable()
    {
        Lua.RegisterFunction("EnableSheepByVar", this, SymbolExtensions.GetMethodInfo(() => EnableSheepByVar(string.Empty)));
        Lua.RegisterFunction("DisableSheepByVar", this, SymbolExtensions.GetMethodInfo(() => DisableSheepByVar(string.Empty)));
    }

    void OnDisable()
    {
        Lua.UnregisterFunction("EnableSheepByVar");
        Lua.UnregisterFunction("DisableSheepByVar");
    }

    public void EnableSheepByVar(string variableName)
    {
        if (SheepCompanion.Instance != null)
        {
            SheepCompanion.Instance.EnableByVariable(variableName);
        }
        else
        {
            Debug.LogWarning("[SheepLuaCommands] SheepCompanion Instance 不存在！");
        }
    }

    public void DisableSheepByVar(string variableName)
    {
        if (SheepCompanion.Instance != null)
        {
            SheepCompanion.Instance.DisableByVariable(variableName);
        }
        else
        {
            Debug.LogWarning("[SheepLuaCommands] SheepCompanion Instance 不存在！");
        }
    }
}
