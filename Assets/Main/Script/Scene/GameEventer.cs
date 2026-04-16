using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SceneEventer
{
    // ゲームシーンのボタンイベント管理.
    public class GameEventer : ButtonEventer
    {
        [SerializeField]
        private Button backToTitle;
        [SerializeField]
        private Button restart;

        // ボタンイベントの処理.
        protected override void ButtonEvents(Button button)
        {
            switch (button)
            {
                case var _ when button == backToTitle:
                    BackToTitle();
                    break;
                case var _ when button == restart:
                    Restart();
                    break;
            }
        }

        protected override void Init()
        {
            // UI入力を有効化.
            InputSystemActionsManager.Instance().UIEnable();

            buttons = new Button[][]
            {
                new Button[] { backToTitle },
                new Button[] { restart }
            };
        }

        // タイトルに戻る.
        public void BackToTitle()
        {
            SceneManagerSingleton.Instance().ChangeScene(UseScene.Title).Forget();
        }

        // ゲームをリスタート.
        public void Restart()
        {
            SceneManagerSingleton.Instance().ChangeScene(UseScene.InGame, true).Forget();
        }
    }
}
