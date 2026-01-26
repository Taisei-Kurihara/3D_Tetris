using UnityEngine;

// ミノブロックのinterface.
public interface IMinoBlock
{
    // ブロックのグリッド位置を取得.
    Vector3Int GridPosition { get; }

    // ブロックのワールド位置を取得.
    Vector3 WorldPosition { get; }

    // 位置を更新.
    void UpdatePosition(Vector3 newWorldPosition);

    // GameMasterに位置を登録.
    void RegisterToGameMaster();

    // GameMasterから位置を解除.
    void UnregisterFromGameMaster();

    // 指定方向に移動可能か判定.
    bool CanMove(Vector3 direction);

    // 指定方向に移動可能か判定(上方向制限を無視するオプション付き).
    bool CanMove(Vector3 direction, bool ignoreUpperLimit);
}
