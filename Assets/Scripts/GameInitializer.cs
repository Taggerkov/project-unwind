using UnityEngine;

public static class GameInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameObject prefab = Resources.Load<GameObject>("GlobalSystems");

        if (prefab == null)
        {
            Debug.LogError("GameInitializer: Could not find 'GlobalSystems' prefab in Resources!");
            return;
        }

        GameObject instance = Object.Instantiate(prefab);
        instance.name = "[Global Systems]";

        Object.DontDestroyOnLoad(instance);
    }
}