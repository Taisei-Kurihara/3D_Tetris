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
        Debug.Log("[SceneManager] LoadSceneAsync 開始");

        // CanvasManager初期化待機.
        var canvasManager = CanvasManager.Instance();
        Debug.Log($"[SceneManager] CanvasManager取得: {canvasManager != null}");

        // フェードプレハブをロード（Addressablesに"Load"キーで登録されている場合）.
        GameObject fadeObj = null;
        LoadScene_interface loadScene = null;

        try
        {
            // キーの存在チェック.
            var locHandle = Addressables.LoadResourceLocationsAsync("Load");
            await locHandle.Task;
            Debug.Log($"[SceneManager] 'Load'キー検索結果: count={locHandle.Result?.Count ?? 0}");
            if (locHandle.Result != null && locHandle.Result.Count > 0)
            {
                var fadeHandle = Addressables.InstantiateAsync("Load");
                await fadeHandle.Task;
                fadeObj = fadeHandle.Result;
                Debug.Log($"[SceneManager] fadeObj生成: {fadeObj.name}, components={string.Join(", ", System.Array.ConvertAll(fadeObj.GetComponents<Component>(), c => c.GetType().Name))}");

                // Canvas配下に親子付け（UIとして正しく表示するため）.
                await canvasManager.AttachToCanvas(fadeObj, 100);
                Debug.Log($"[SceneManager] Canvas親子付け完了: parent={fadeObj.transform.parent?.name}");

                // RectTransformを画面全体に拡張.
                RectTransform rt = fadeObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    Debug.Log($"[SceneManager] RectTransform設定完了: size={rt.rect.size}");
                }
                else
                {
                    Debug.LogWarning("[SceneManager] fadeObjにRectTransformがありません.");
                }

                loadScene = fadeObj.GetComponent<LoadScene_interface>();
                Debug.Log($"[SceneManager] LoadScene_interface取得: {loadScene != null}, 型={loadScene?.GetType().Name}");
            }
            Addressables.Release(locHandle);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SceneManager] フェードプレハブの読み込み失敗（スキップ）: {e.Message}");
        }

        // フェードイン開始.
        Debug.Log($"[SceneManager] フェードイン開始前: loadScene={loadScene != null}");
        if (loadScene != null) await loadScene.StartFadeIn();
        Debug.Log("[SceneManager] フェードイン完了");

        // シーンロード（AssetBundle重複回避のため標準SceneManagerを優先）.
        string sceneName = scene.ToString();
        try
        {
            // Build Settingsに登録されていれば標準ロードを使用.
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op != null)
            {
                Debug.Log($"[SceneManager] 標準ロードを使用: {sceneName}");
                await op;
            }
            else
            {
                // Build Settingsに未登録の場合、Addressablesにフォールバック.
                Debug.Log($"[SceneManager] Addressablesフォールバック: {sceneName}");
                var sceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                await sceneHandle.Task;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SceneManager] シーンロード失敗: {e.Message}");
            throw;
        }

        Debug.Log($"[SceneManager] シーンロード完了: activeScene={SceneManager.GetActiveScene().name}");

        // フェードアウト開始.
        Debug.Log($"[SceneManager] フェードアウト開始前: loadScene={loadScene != null}, fadeObj={fadeObj != null}, fadeObj.destroyed={fadeObj == null}");
        if (loadScene != null)
        {
            Debug.Log($"[SceneManager] フェードアウト開始: fadeObj.active={fadeObj?.activeSelf}, fadeObj.parent={fadeObj?.transform.parent?.name}");
            await loadScene.StartFadeOut();
            Debug.Log("[SceneManager] フェードアウト完了、fadeObj破棄");
            GameObject.Destroy(fadeObj);
        }

        Debug.Log("[SceneManager] LoadSceneAsync 完了");
    }
}
