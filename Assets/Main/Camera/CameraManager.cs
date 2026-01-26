using Common;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraManager : MonoBehaviour
{
    // 注視点(原点).
    private Vector3 targetPoint = new Vector3(0,10,0);

    // カメラと注視点の距離.
    [SerializeField]
    private float distance = 70f;

    // 水平回転角度(Y軸周り).
    private float horizontalAngle = 0f;

    // 垂直回転角度(X軸周り).
    private float verticalAngle = 35f;

    // 垂直角度の上限(45度).
    [SerializeField]
    private float verticalAngleMax = 45f;

    // 垂直角度の下限(25度).
    [SerializeField]
    private float verticalAngleMin = 25f;

    // 回転速度.
    [SerializeField]
    private float rotationSpeed = 50f;

    // 入力システム.
    private InputSystem_Actions action;

    private void Start()
    {
        action = InputSystemActionsManager.Instance().GetInputSystem_Actions();

        // 初期位置を設定.
        UpdateCameraPosition();
    }

    private void Update()
    {
        HandleCameraRotation();
        UpdateCameraPosition();
    }

    // カメラ回転入力を処理.
    private void HandleCameraRotation()
    {
        Vector2 rotationInput = Vector2.zero;

        // InputSystemのLookアクションから入力を取得.
        if (action != null)
        {
            rotationInput = action.Player.Look.ReadValue<Vector2>();
        }

        // 水平回転(左右).
        horizontalAngle += rotationInput.x * rotationSpeed * Time.deltaTime;

        // 垂直回転(上下、反転).
        verticalAngle -= rotationInput.y * rotationSpeed * Time.deltaTime;

        // 垂直角度を制限(25度〜45度).
        verticalAngle = Mathf.Clamp(verticalAngle, verticalAngleMin, verticalAngleMax);
    }

    // カメラ位置と向きを更新.
    private void UpdateCameraPosition()
    {
        // 球面座標からカメラ位置を計算.
        float horizontalRad = horizontalAngle * Mathf.Deg2Rad;
        float verticalRad = verticalAngle * Mathf.Deg2Rad;

        // 球面座標から直交座標への変換.
        float x = distance * Mathf.Cos(verticalRad) * Mathf.Sin(horizontalRad);
        float y = distance * Mathf.Sin(verticalRad);
        float z = distance * Mathf.Cos(verticalRad) * Mathf.Cos(horizontalRad);

        // カメラ位置を設定.
        transform.position = targetPoint + new Vector3(x, y, z);

        // カメラを注視点に向ける.
        transform.LookAt(targetPoint);
    }

    // 水平角度を設定.
    public void SetHorizontalAngle(float angle)
    {
        horizontalAngle = angle;
        UpdateCameraPosition();
    }

    // 垂直角度を設定.
    public void SetVerticalAngle(float angle)
    {
        verticalAngle = Mathf.Clamp(angle, verticalAngleMin, verticalAngleMax);
        UpdateCameraPosition();
    }

    // 距離を設定.
    public void SetDistance(float newDistance)
    {
        distance = Mathf.Max(10f, newDistance);
        UpdateCameraPosition();
    }
}
