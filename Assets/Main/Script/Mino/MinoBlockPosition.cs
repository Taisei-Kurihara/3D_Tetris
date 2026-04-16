using UnityEngine;

// blockに付与される自身の位置をGridManagerに伝えるクラス.
public class MinoBlockPosition : MonoBehaviour, IMinoBlock
{
    private Vector3Int gridPosition;
    private bool isRegistered = false;

    // グリッド位置を取得.
    public Vector3Int GridPosition => gridPosition;

    // ワールド位置を取得.
    public Vector3 WorldPosition => transform.position;

    // GridManagerのキャッシュ参照（シーン破棄時に新規生成を防ぐためプロパティではなくフィールドで保持）.
    private GridManager _gridCache;
    private GridManager grid
    {
        get
        {
            if (_gridCache == null)
            {
                // シーン破棄中はInstance()を呼ばない.
                if (_isQuitting) return null;
                _gridCache = GridManager.Instance();
            }
            return _gridCache;
        }
    }

    private static bool _isQuitting = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _isQuitting = false;
    }

    private void OnEnable()
    {
        UpdateGridPosition();
    }

    private void OnDisable()
    {
        if (isRegistered && _gridCache != null)
        {
            UnregisterFromGridManager();
        }
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    // グリッド位置を更新.
    private void UpdateGridPosition()
    {
        if (grid != null)
        {
            gridPosition = grid.WorldToGridIndex(transform.position);
        }
    }

    // 指定方向に移動可能か判定(ワールド座標で処理).
    public bool CanMove(Vector3 direction)
    {
        // デフォルトは上方向制限を無視する(通常の移動・回転・落下用).
        return CanMove(direction, true);
    }

    // 指定方向に移動可能か判定(上方向制限を無視するオプション付き).
    public bool CanMove(Vector3 direction, bool ignoreUpperLimit)
    {
        if (grid == null) return false;

        int gridXZSize = grid.GridXZSize;
        int gridYSize = grid.GridYSize;

        // 移動先のワールド座標を計算.
        Vector3 targetWorldPos = transform.position + direction;

        // 移動先のグリッドインデックスを取得.
        Vector3Int targetGridIndex = grid.WorldToGridIndex(targetWorldPos);

        // グリッド範囲内かチェック(X,Z軸と下方向のみ).
        if (targetGridIndex.x < 0 || targetGridIndex.x >= gridXZSize ||
            targetGridIndex.z < 0 || targetGridIndex.z >= gridXZSize ||
            targetGridIndex.y < 0)
        {
            return false;
        }

        // 上方向制限チェック(ゲームオーバー判定用のみ).
        if (!ignoreUpperLimit && targetGridIndex.y >= gridYSize)
        {
            return false;
        }

        // Y軸の正の方向がグリッド範囲外の場合、ブロック衝突チェックをスキップ(落下・移動・回転用).
        if (ignoreUpperLimit && targetGridIndex.y >= gridYSize)
        {
            return true;
        }

        // 最大高さを取得してチェック.
        int maxHeight = grid.GetHeightAt(targetGridIndex.x, targetGridIndex.z);

        // 移動先が最大高さ未満の場合、ブロック衝突チェック.
        if (targetGridIndex.y < maxHeight)
        {
            // そのマスにブロックが存在するか確認.
            if (grid.IsBlockExist(targetGridIndex))
            {
                return false;
            }
        }

        return true;
    }

    // 位置を更新.
    public void UpdatePosition(Vector3 newWorldPosition)
    {
        if (isRegistered)
        {
            UnregisterFromGridManager();
        }
        transform.position = newWorldPosition;
        UpdateGridPosition();
        RegisterToGridManager();
    }

    // GridManagerに位置を登録.
    public void RegisterToGridManager()
    {
        if (grid != null && !isRegistered)
        {
            UpdateGridPosition();
            grid.RegisterBlock(gridPosition, gameObject);
            isRegistered = true;
        }
    }

    // GridManagerから位置を解除.
    public void UnregisterFromGridManager()
    {
        if (grid != null && isRegistered)
        {
            grid.UnregisterBlock(gridPosition);
            isRegistered = false;
        }
    }
}
