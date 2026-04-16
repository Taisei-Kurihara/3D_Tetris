using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMaster : Singleton_DestroyAvailableMonoSingleton<GameMaster>
{

    private float fallSec = 1;
    public float FallSec { get { return fallSec; } }

    // 前フレームのXrey入力値.
    private float prevXreyInput = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        // シーンロード毎に呼ばれるコールバックを登録.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // シーンロード時にInGameならGameMasterを生成.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (System.Enum.TryParse<UseScene>(scene.name, out UseScene useScene) && useScene == UseScene.InGame)
        {
            GameMaster.Instance();
        }
    }

    private void Start()
    {
        MainGameLoop().Forget();
    }

    private void Update()
    {
        // X-ray vision 入力処理（InputSystem 1D Axis）.
        var inputActions = InputSystemActionsManager.Instance()?.GetInputSystem_Actions();
        if (inputActions != null)
        {
            float xreyInput = inputActions.Player.Xrey.ReadValue<float>();

            // 入力が0から変化した瞬間のみ処理.
            if (xreyInput != 0 && prevXreyInput == 0)
            {
                if (xreyInput > 0)
                {
                    // 正の入力で深度増加（透明化範囲を縮小）.
                    GridManager.Instance()?.AdjustXrayDepth(1);
                }
                else
                {
                    // 負の入力で深度減少（より下層まで透明化）.
                    GridManager.Instance()?.AdjustXrayDepth(-1);
                }
            }

            prevXreyInput = xreyInput;
        }
    }

    // ゲームオーバー処理.
    public void OnGameOver()
    {
        Debug.Log("ゲームオーバー");
        // ゲームオーバー時の処理(後で実装).
    }

    private async UniTask MainGameLoop()
    {
        // GridManagerのプレハブ読み込み完了を待機.
        await GridManager.Instance().WaitForPrefabLoaded();

        while (true)
        {
            GameObject minoObject = new GameObject("Mino");
            MinoManager minoManager = minoObject.AddComponent<MinoManager>();
            // ランダムな形状を設定.
            minoManager.ShapeData = MinoShapeData.GetRandomShape();

            // GridManagerからスポーン位置を取得.
            Vector3 spawnPos = GridManager.Instance().GetMinoSpawnPosition();
            minoManager.CreateMino(spawnPos);
            await minoManager.StartFall();

            Debug.Log("MainGameLoop: StartFall終了、消去処理開始");
            // 落下終了後に消去処理と詰め処理を実行(演出と待機を含む).
            await GridManager.Instance().CheckAndClearLines();
            Debug.Log("MainGameLoop: 消去処理終了、次のミノ生成へ");
        }
    }

}
