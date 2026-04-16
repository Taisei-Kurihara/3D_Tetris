using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SceneEventer
{
    // タイトルシーンのボタンイベント管理.
    public class TitleEventer : ButtonEventer
    {
        [SerializeField]
        private Button gameStart;
        [SerializeField]
        private Button quitGame;

        // ボタンイベントの処理.
        protected override void ButtonEvents(Button button)
        {
            switch (button)
            {
                case var _ when button == gameStart:
                    GameStart();
                    break;
                case var _ when button == quitGame:
                    Quit();
                    break;
            }
        }

        protected override void Init()
        {
            // UI入力を有効化.
            InputSystemActionsManager.Instance().UIEnable();

            buttons = new Button[][]
            {
                new Button[] { gameStart },
                new Button[] { quitGame }
            };
        }

        // ゲーム開始（InGameシーンへ遷移）.
        public void GameStart()
        {
            SceneManagerSingleton.Instance().ChangeScene(UseScene.InGame).Forget();
        }

        // ゲーム終了.
        public void Quit()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}
