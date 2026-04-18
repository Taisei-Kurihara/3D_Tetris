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
        Debug.Log($"[Fade] === StartFadeIn 開始 === fadeCanvas={fadeCanvas}, gameObject.active={gameObject.activeSelf}, parent={transform.parent?.name}");
        if (fadeCanvas == null)
        {
            Debug.LogWarning("[Fade] StartFadeIn: fadeCanvas が null のため中断.");
            return;
        }

        fadeCanvas.alpha = 0f;
        float time = 0f;
        int frameCount = 0;
        Debug.Log($"[Fade] FadeIn ループ開始: changetime={changetime}");
        while (fadeCanvas.alpha < 1f)
        {
            time += Time.unscaledDeltaTime;
            fadeCanvas.alpha = time / changetime;
            frameCount++;
            if (frameCount <= 3 || frameCount % 10 == 0)
            {
                Debug.Log($"[Fade] FadeIn frame={frameCount}, time={time:F3}, alpha={fadeCanvas.alpha:F3}, unscaledDT={Time.unscaledDeltaTime:F4}");
            }
            await UniTask.Yield();
        }
        fadeCanvas.alpha = 1f;
        Debug.Log($"[Fade] === StartFadeIn 完了 === frames={frameCount}, alpha={fadeCanvas.alpha}");
    }

    // フェードアウト（画面を明るくする、α: 1→0）.
    public async UniTask StartFadeOut()
    {
        Debug.Log($"[Fade] === StartFadeOut 開始 === fadeCanvas={fadeCanvas}, gameObject.active={gameObject.activeSelf}, parent={transform.parent?.name}");
        if (fadeCanvas == null)
        {
            Debug.LogWarning("[Fade] StartFadeOut: fadeCanvas が null のため中断.");
            return;
        }

        fadeCanvas.alpha = 1f;
        // シーンロード直後のdeltaTimeスパイクが収まるまで待機.
        while (Time.unscaledDeltaTime > 0.1f)
        {
            await UniTask.Yield();
        }

        float time = 0f;
        int frameCount = 0;
        while (time < changetime)
        {
            // deltaTimeをキャップ（シーンロード直後のスパイク防止）.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            time += dt;
            float newAlpha = Mathf.Clamp01(1f - (time / changetime));
            fadeCanvas.alpha = newAlpha;
            frameCount++;
            if (frameCount <= 3 || frameCount % 10 == 0)
            {
                Debug.Log($"[Fade] FadeOut frame={frameCount}, time={time:F3}, alpha={newAlpha:F3}, unscaledDT={Time.unscaledDeltaTime:F4}, cappedDT={dt:F4}");
            }
            await UniTask.Yield();
        }
        fadeCanvas.alpha = 0f;
        Debug.Log($"[Fade] === StartFadeOut 完了 === frames={frameCount}, alpha={fadeCanvas.alpha}");
    }
}
