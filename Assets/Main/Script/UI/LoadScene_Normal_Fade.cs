using Cysharp.Threading.Tasks;
using UnityEngine;

// 通常のフェードイン・フェードアウト処理.
public class LoadScene_Normal_Fade : MonoBehaviour, LoadScene_interface
{
    // フェード用のCanvasGroup.
    [SerializeField] CanvasGroup fadeCanvas;

    // フェード変化にかかる時間（秒）.
    float changetime = 1f;

    // フェードイン（画面を暗くする、α: 0→1）.
    public async UniTask StartFadeIn()
    {
        if (fadeCanvas == null) return;

        fadeCanvas.alpha = 0f;
        float time = 0f;
        while (fadeCanvas.alpha < 1f)
        {
            time += Time.deltaTime;
            fadeCanvas.alpha = time / changetime;
            await UniTask.Yield();
        }
        fadeCanvas.alpha = 1f;
    }

    // フェードアウト（画面を明るくする、α: 1→0）.
    public async UniTask StartFadeOut()
    {
        if (fadeCanvas == null) return;

        fadeCanvas.alpha = 1f;
        float time = 0f;
        while (fadeCanvas.alpha > 0f)
        {
            time += Time.deltaTime;
            fadeCanvas.alpha = ((time / changetime) - 1f) * -1f;
            await UniTask.Yield();
        }
        fadeCanvas.alpha = 0f;
    }
}
