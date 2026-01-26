using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;


/// <summary>
/// 破壊可能シングルトン
/// </summary>
/// <typeparam name="T"></typeparam>
public class Singleton_DestroyAvailableMonoSingleton<T> : MonoBehaviour where T : Singleton_DestroyAvailableMonoSingleton<T>
{
    /// <summary>
    /// 静的な変数
    /// </summary>
    protected static T instance;

    // 初期化完了フラグ.
    bool isInitialized = false;
    public bool IsInitialized => isInitialized;

    /// <summary>
    /// 本体の取得
    /// </summary>
    /// <returns></returns>
    public static T Instance()
    {
        // なければ、GameObjectで生成しDontdestroyに移動させる
        if (instance == null)
        {
            var gameObject = new GameObject(typeof(T).Name);
            instance = gameObject.AddComponent<T>();

            // Awake が呼ばれる前に強制初期化
            instance.Init().Forget();
        }
        return instance;
    }

    // SceneManagerSingleton 内
    private async UniTaskVoid Init()
    {
        Debug.Log("[SceneManager] Forced Init start");
        try
        {
            await Addressables.InitializeAsync();
            isInitialized = true;
            Debug.Log("[SceneManager] Forced Init complete");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SceneManager] Forced Init failed: {e}");
        }
    }
}
