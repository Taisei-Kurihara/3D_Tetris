using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

// 使用するシーンの定義.
public enum UseScene
{
    Title,
    InGame
}

// シーン管理シングルトン.
public class SceneManagerSingleton : Singleton_MonoBehaviourBase<SceneManagerSingleton>
{
    public UseScene CurrentScene { get; private set; } = UseScene.Title;
    private bool isLoading = false;

    // 起動時の現在シーンをCurrentSceneにセット.
    public void InitCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (System.Enum.TryParse<UseScene>(sceneName, out UseScene scene))
        {
            CurrentScene = scene;
        }
        else
        {
            Debug.LogWarning($"[SceneManager] CurrentScene 初期化失敗: シーン名 '{sceneName}' は UseScene に存在しません. デフォルト Title を使用.");
            CurrentScene = UseScene.Title;
        }
    }

    // シーン切り替え.
    public async UniTask ChangeScene(UseScene newScene, bool forceReload = false)
    {
        Debug.Log($"[SceneManager] ChangeScene: {newScene} (forceReload={forceReload})");

        if (isLoading) return;
        isLoading = true;

        // Addressables初期化完了を待機.
        await UniTask.WaitUntil(() => IsInitialized);

        try
        {
            await LoadSceneAsync(newScene);
            CurrentScene = newScene;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SceneManager] ChangeScene failed: {e}");
        }

        isLoading = false;
    }

    private async UniTask LoadSceneAsync(UseScene scene)
    {
        // フェードプレハブをロード（Addressablesに"Load"キーで登録されている場合）.
        GameObject fadeObj = null;
        LoadScene_interface loadScene = null;

        try
        {
            // キーの存在チェック.
            var locHandle = Addressables.LoadResourceLocationsAsync("Load");
            await locHandle.Task;
            if (locHandle.Result != null && locHandle.Result.Count > 0)
            {
                var fadeHandle = Addressables.InstantiateAsync("Load");
                await fadeHandle.Task;
                fadeObj = fadeHandle.Result;
                fadeObj.transform.SetParent(transform);
                fadeObj.transform.localPosition = Vector3.zero;
                loadScene = fadeObj.GetComponent<LoadScene_interface>();
            }
            Addressables.Release(locHandle);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SceneManager] フェードプレハブの読み込み失敗（スキップ）: {e.Message}");
        }

        // フェードイン開始.
        if (loadScene != null) await loadScene.StartFadeIn();

        // シーンロード.
        string sceneName = scene.ToString();
        try
        {
            // キーの存在チェック.
            var locHandle = Addressables.LoadResourceLocationsAsync(sceneName);
            await locHandle.Task;
            if (locHandle.Result == null || locHandle.Result.Count == 0)
            {
                Addressables.Release(locHandle);

                // Build Settingsにシーンが登録されているか確認.
                int sceneIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + sceneName + ".unity");
                if (sceneIndex < 0)
                {
                    Debug.LogError($"[SceneManager] シーン '{sceneName}' はAddressablesにもBuild Settingsにも登録されていません.");
                    throw new System.InvalidOperationException($"シーン '{sceneName}' が見つかりません.");
                }

                // 通常のSceneManagerで読み込み.
                Debug.Log($"[SceneManager] 通常ロードを使用: {sceneName}");
                var op = SceneManager.LoadSceneAsync(sceneName);
                if (op != null)
                {
                    await op;
                }
                else
                {
                    throw new System.InvalidOperationException($"シーン '{sceneName}' のロードに失敗しました.");
                }
            }
            else
            {
                Addressables.Release(locHandle);
                var sceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                await sceneHandle.Task;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SceneManager] シーンロード失敗: {e.Message}");
            throw;
        }

        // フェードアウト開始.
        if (loadScene != null)
        {
            await loadScene.StartFadeOut();
            GameObject.Destroy(fadeObj);
        }
    }
}
