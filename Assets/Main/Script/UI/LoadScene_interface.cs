using Cysharp.Threading.Tasks;

// シーンロード処理のインターフェース定義.
// シーン遷移時のフェード処理を実装するためのインターフェース.
public interface LoadScene_interface
{
    // フェードイン処理を開始（画面を暗くする）.
    UniTask StartFadeIn();

    // フェードアウト処理を開始（画面を明るくする）.
    UniTask StartFadeOut();
}
