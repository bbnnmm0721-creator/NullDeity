public static class TravelData
{
    // 下一個場景想要的出生點 Id；null 代表用該場景的 default
    public static string nextSpawnId = null;

    // 方便呼叫：設定 Id
    public static void SetNextSpawn(string id)
    {
        nextSpawnId = id;
        UnityEngine.Debug.Log($"[TravelData] nextSpawnId = \"{id ?? "null"}\"");
    }
}
