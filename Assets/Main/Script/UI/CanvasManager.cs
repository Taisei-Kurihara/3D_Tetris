using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

// UI用Canvasを管理するシングルトン（DontDestroyOnLoad）.
public class CanvasManager : Singleton_MonoBehaviourBase<CanvasManager>
{
    private Canvas canvas;
    private bool isCanvasReady = false;

    private void Start()
    {
        CreateCanvas();
    }

    // Screen Space - Overlay Canvasを生成.
    private void CreateCanvas()
    {
        GameObject canvasObj = new GameObject("ManagedCanvas");
        canvasObj.transform.SetParent(transform);

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        isCanvasReady = true;
        Debug.Log("[CanvasManager] Canvas生成完了.");
    }

    // オブジェクトをCanvas配下に親子付け.
    public async UniTask AttachToCanvas(GameObject childObject)
    {
        await UniTask.WaitUntil(() => isCanvasReady);

        if (canvas != null && childObject != null)
        {
            childObject.transform.SetParent(canvas.transform, false);
        }
    }

    // オブジェクトをCanvas配下に親子付け（sortingOrder指定）.
    public async UniTask AttachToCanvas(GameObject childObject, int sortingOrder)
    {
        await UniTask.WaitUntil(() => isCanvasReady);

        if (canvas != null && childObject != null)
        {
            childObject.transform.SetParent(canvas.transform, false);

            // 子にCanvasがあればsortingOrderを設定.
            Canvas childCanvas = childObject.GetComponent<Canvas>();
            if (childCanvas != null)
            {
                childCanvas.sortingOrder = sortingOrder;
            }
        }
    }
}
