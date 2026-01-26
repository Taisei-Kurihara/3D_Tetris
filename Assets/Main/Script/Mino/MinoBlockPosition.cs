using UnityEngine;

// blockに付与される自身の位置をGameMasterに伝えるクラス.
public class MinoBlockPosition : MonoBehaviour, IMinoBlock
{
    private Vector3Int gridPosition;
    private bool isRegistered = false;

    // グリッド位置を取得.
    public Vector3Int GridPosition => gridPosition;

    // ワールド位置を取得.
    public Vector3 WorldPosition => transform.position;

    private void OnEnable()
    {
        UpdateGridPosition();
    }

    private void OnDisable()
    {
        if (isRegistered)
        {
            UnregisterFromGameMaster();
        }
    }

    // グリッド位置を更新.
    private void UpdateGridPosition()
    {
        if (GameMaster.Instance() != null)
        {
            gridPosition = GameMaster.Instance().WorldToGridIndex(transform.position);
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
        if (GameMaster.Instance() == null) return false;

        // 移動先のワールド座標を計算.
        Vector3 targetWorldPos = transform.position + direction;

        // 移動先のグリッドインデックスを取得.
        Vector3Int targetGridIndex = GameMaster.Instance().WorldToGridIndex(targetWorldPos);

        // グリッド範囲内かチェック(X,Z軸と下方向のみ).
        if (targetGridIndex.x < 0 || targetGridIndex.x >= 10 ||
            targetGridIndex.z < 0 || targetGridIndex.z >= 10 ||
            targetGridIndex.y < 0)
        {
            return false;
        }

        // 上方向制限チェック(ゲームオーバー判定用のみ).
        if (!ignoreUpperLimit && targetGridIndex.y >= 6)
        {
            return false;
        }

        // Y軸の正の方向がグリッド範囲外の場合、ブロック衝突チェックをスキップ(落下・移動・回転用).
        if (ignoreUpperLimit && targetGridIndex.y >= 6)
        {
            return true;
        }

        // 最大高さを取得してチェック.
        int maxHeight = GameMaster.Instance().GetHeightAt(targetGridIndex.x, targetGridIndex.z);

        // 移動先が最大高さ未満の場合、ブロック衝突チェック.
        if (targetGridIndex.y < maxHeight)
        {
            // そのマスにブロックが存在するか確認.
            if (GameMaster.Instance().IsBlockExist(targetGridIndex))
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
            UnregisterFromGameMaster();
        }
        transform.position = newWorldPosition;
        UpdateGridPosition();
        RegisterToGameMaster();
    }

    // GameMasterに位置を登録.
    public void RegisterToGameMaster()
    {
        if (GameMaster.Instance() != null && !isRegistered)
        {
            UpdateGridPosition();
            GameMaster.Instance().RegisterBlock(gridPosition, gameObject);
            isRegistered = true;
        }
    }

    // GameMasterから位置を解除.
    public void UnregisterFromGameMaster()
    {
        if (GameMaster.Instance() != null && isRegistered)
        {
            GameMaster.Instance().UnregisterBlock(gridPosition);
            isRegistered = false;
        }
    }
}
