using Common;
using UnityEngine;

// グリッド中心を周回するカメラ制御.
public class CameraController : MonoBehaviour
{
    // 回転感度（基本値の1/10）.
    [SerializeField] private float sensitivity = 0.1f;

    // 仰角制限（度）.
    [SerializeField] private float minPitch = 5f;
    [SerializeField] private float maxPitch = 85f;

    // 現在の回転角度.
    private float yaw = 0f;
    private float pitch = 30f;

    // 周回対象.
    private Vector3 targetCenter;
    private float orbitDistance;
    private bool isInitialized = false;

    private InputSystem_Actions action;

    private void Start()
    {
        action = InputSystemActionsManager.Instance()?.GetInputSystem_Actions();

        // GridManagerからグリッド中心と距離を計算.
        var grid = GridManager.Instance();
        if (grid != null)
        {
            targetCenter = grid.GridCenter;
            // グリッド全体が見える距離を自動計算.
            float gridExtent = Mathf.Max(grid.XZMax - grid.XZMin, grid.YMax - grid.YMin);
            orbitDistance = gridExtent * 1.5f;
        }
        else
        {
            targetCenter = Vector3.zero;
            orbitDistance = 50f;
        }

        // 初期カメラ位置から角度を逆算.
        Vector3 offset = transform.position - targetCenter;
        if (offset.sqrMagnitude > 0.01f)
        {
            orbitDistance = offset.magnitude;
            yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(Mathf.Clamp(offset.y / orbitDistance, -1f, 1f)) * Mathf.Rad2Deg;
        }

        isInitialized = true;
        UpdateCameraPosition();
    }

    private void LateUpdate()
    {
        if (!isInitialized || action == null) return;

        // Player.Look入力を取得.
        Vector2 lookInput = action.Player.Look.ReadValue<Vector2>();

        if (lookInput.sqrMagnitude > 0.001f)
        {
            yaw += lookInput.x * sensitivity;
            pitch -= lookInput.y * sensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            UpdateCameraPosition();
        }
    }

    // カメラ位置と向きを更新.
    private void UpdateCameraPosition()
    {
        float pitchRad = pitch * Mathf.Deg2Rad;
        float yawRad = yaw * Mathf.Deg2Rad;

        // 球面座標からカメラ位置を計算.
        Vector3 offset = new Vector3(
            Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
            Mathf.Sin(pitchRad),
            Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
        ) * orbitDistance;

        transform.position = targetCenter + offset;
        transform.LookAt(targetCenter);
    }

    // カメラの正面方向（XZ平面に投影、正規化済み）.
    public Vector3 GetForwardXZ()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    // カメラの右方向（XZ平面に投影、正規化済み）.
    public Vector3 GetRightXZ()
    {
        Vector3 right = transform.right;
        right.y = 0f;
        return right.normalized;
    }
}
