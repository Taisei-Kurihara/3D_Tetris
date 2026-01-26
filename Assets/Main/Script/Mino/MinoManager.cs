using System.Collections.Generic;
using System.Threading;
using Common;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// ミノの形(3D配置)3x3x3にblockを配置して親子付けして管理するクラス.
public class MinoManager : MonoBehaviour
{
    // ミノを構成するブロックのリスト.
    private List<GameObject> blocks = new List<GameObject>();

    // ゴースト(予測)ブロックのリスト.
    private List<GameObject> ghostBlocks = new List<GameObject>();

    // ゴーストブロックの親オブジェクト.
    private GameObject ghostParent;

    // ゴーストブロックの色(水色、高透明度).
    private static readonly Color GHOST_COLOR = new Color(0f, 1f, 1f, 0.2f);

    // 回転方向矢印オブジェクト(X軸:赤, Y軸:緑, Z軸:青).
    private GameObject arrowX;
    private GameObject arrowY;
    private GameObject arrowZ;

    // 移動方向矢印オブジェクト(X軸:赤, Z軸:青).
    private GameObject moveArrowX;
    private GameObject moveArrowZ;

    // 矢印の親オブジェクト.
    private GameObject arrowParent;

    // 回転矢印の色(α=0.35).
    private static readonly Color ARROW_COLOR_X = new Color(1f, 0f, 0f, 0.35f);
    private static readonly Color ARROW_COLOR_Y = new Color(0f, 1f, 0f, 0.35f);
    private static readonly Color ARROW_COLOR_Z = new Color(0f, 0f, 1f, 0.35f);

    // 移動矢印の色(α=0.35).
    private static readonly Color MOVE_ARROW_COLOR_X = new Color(1f, 0f, 0f, 0.35f);
    private static readonly Color MOVE_ARROW_COLOR_Z = new Color(0f, 0f, 1f, 0.35f);

    // 矢印のオフセット距離.
    private const float ARROW_OFFSET = 10f;

    // ミノの形状データ.
    private MinoShapeData shapeData = new MinoShapeData();

    // ミノの形状データを外部から設定・取得可能なプロパティ.
    public MinoShapeData ShapeData
    {
        get => shapeData;
        set => shapeData = value;
    }


    // グリッドサイズ.
    private const float GRID_SIZE = 5f;

    // 落下処理用の時間保存変数.
    private float lastFallTime;

    // 落下中フラグ.
    private bool isFalling = false;

    // 着地状態フラグ(下にブロックがある状態).
    private bool isGrounded = false;

    // 一度でも落下したかフラグ(ゲームオーバー判定用).
    private bool hasFallen = false;

    // ロックディレイ中フラグ(着地後もう1回落下時間を待つ).
    private bool isInLockDelay = false;

    // 回転アニメーション中フラグ.
    private bool isRotating = false;

    // 回転アニメーション時間(秒).
    private const float ROTATION_DURATION = 0.1f;

    // キャンセル用トークン.
    private CancellationTokenSource fallCancellationToken;

    InputSystem_Actions action;

    // 回転入力キャンセル用トークン.
    private CancellationTokenSource rotationCancellationToken;
    Vector3 rotvec = Vector3.zero;

    // 移動入力キャンセル用トークン.
    private CancellationTokenSource movementCancellationToken;
    Vector2 movevec = Vector2.zero;

    // 落下開始関数.
    public async UniTask StartFall()
    {
        action = InputSystemActionsManager.Instance().GetInputSystem_Actions();
        // Player入力を有効化.
        InputSystemActionsManager.Instance().PlayerEnable();

        // 回転入力処理を開始.
        rotationCancellationToken = new CancellationTokenSource();
        RotationUpdateLoop(rotationCancellationToken.Token).Forget();

        // 移動入力処理を開始.
        movementCancellationToken = new CancellationTokenSource();
        MovementUpdateLoop(movementCancellationToken.Token).Forget();

        if (isFalling) return;

        isFalling = true;
        fallCancellationToken = new CancellationTokenSource();

        // StartFall入力を待機.
        await UniTask.WaitUntil(
            () => action.Player.StartFall.WasPerformedThisFrame(),
            cancellationToken: fallCancellationToken.Token
        );


        // 落下開始時にhasFallenをリセット.
        hasFallen = false;

        Debug.Log("落下開始");

        lastFallTime = Time.time;
        while (isFalling && !fallCancellationToken.Token.IsCancellationRequested)
        {
            float fallSec = GameMaster.Instance().FallSec;
            float currentTime = Time.time;

            // SkipFallボタンが押されたら即座に落下処理.
            if (action.Player.SkipFall.WasPerformedThisFrame())
            {
                Debug.Log("SkipFall: 即時落下");
                lastFallTime = currentTime;
                OnFallStep();

                if (!isFalling)
                {
                    Debug.Log("StartFall: SkipFall後にisFalling=falseのためループ終了");
                    break;
                }
            }
            // 保存した時間との差がFallSec以上なら落下処理を行う.
            else if (currentTime - lastFallTime >= fallSec)
            {
                Debug.Log($"OnFallStep呼び出し: hasFallen={hasFallen}, isGrounded={isGrounded}");
                lastFallTime = currentTime;
                OnFallStep();

                // OnFallStep内でStopFallが呼ばれた場合、ループを抜ける.
                if (!isFalling)
                {
                    Debug.Log("StartFall: isFalling=falseのためループ終了");
                    break;
                }
            }

            await UniTask.Yield(fallCancellationToken.Token);
        }
        Debug.Log("StartFall: whileループ終了");
    }

    // 落下1ステップの処理（継承クラスでオーバーライド可能）.
    protected virtual void OnFallStep()
    {
        Vector3 fallDistance = Vector3.down * GRID_SIZE;

        // 自分自身のブロックとの干渉を防ぐため、チェック前に登録解除.
        UnregisterAllBlocks();

        // 着地状態を更新.
        UpdateGroundedState();
        Debug.Log($"OnFallStep: isGrounded={isGrounded}, hasFallen={hasFallen}, isInLockDelay={isInLockDelay}");

        // 着地状態(下方向に移動不可)の場合.
        if (isGrounded)
        {
            // ブロックを再登録.
            RegisterAllBlocks();

            // 一度も落下していない場合(開始位置で既に着地)はループ停止してゲームオーバー判定.
            if (!hasFallen)
            {
                Debug.Log("OnFallStep: 落下できないためループ停止");
                StopFall();

                // ゲームオーバー判定(開始位置で既に積み上がっている場合).
                bool isGameOver = CheckGameOver();
                Debug.Log($"OnFallStep: 落下不可 CheckGameOver={isGameOver}");
                if (isGameOver)
                {
                    GameMaster.Instance().OnGameOver();
                }
                return;
            }

            // ロックディレイ中でなければ、ロックディレイを開始.
            if (!isInLockDelay)
            {
                Debug.Log("OnFallStep: ロックディレイ開始");
                isInLockDelay = true;
                return;
            }

            // ロックディレイ中で再度着地状態なら確定.
            Debug.Log("OnFallStep: ロックディレイ終了、着地確定");

            // 着地SE再生.
            AudioManager.Instance()?.PlaySE(SEType.BlockLand);

            // ミノの操作停止.
            StopFall();

            // 消去処理はGameMasterのMainGameLoopで実行.

            // ゲームオーバー判定(上限を超えているか、着地後のみ判定).
            bool isGameOverAfterFall = CheckGameOver();
            Debug.Log($"OnFallStep: CheckGameOver={isGameOverAfterFall}");
            if (isGameOverAfterFall)
            {
                GameMaster.Instance().OnGameOver();
            }
            return;
        }

        // 着地状態でなくなったらロックディレイ解除.
        isInLockDelay = false;

        // Y軸方向に1グリッド分落下.
        Debug.Log($"OnFallStep: 落下実行 position={transform.position} -> {transform.position + fallDistance}");
        transform.position += fallDistance;
        RegisterAllBlocks();

        // 落下SE再生.
        AudioManager.Instance()?.PlaySE(SEType.BlockFall);

        // 一度でも落下したフラグを立てる.
        hasFallen = true;

        // 落下後に着地状態を更新(再度Unregisterしてチェック).
        UnregisterAllBlocks();
        UpdateGroundedState();
        RegisterAllBlocks();
        Debug.Log($"OnFallStep: 落下後 isGrounded={isGrounded}");

        // 落下後に着地状態ならロックディレイを開始(すぐには停止しない).
        if (isGrounded)
        {
            Debug.Log("OnFallStep: 落下後に着地、ロックディレイ開始");
            isInLockDelay = true;
        }

        // ゴースト位置を更新.
        UpdateGhostPosition();

        // 矢印位置を更新.
        UpdateArrowPositions();
    }

    // 回転入力のUpdate処理ループ.
    private async UniTaskVoid RotationUpdateLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // 入力取得.

            Vector3 vec = action.Player.Rot.ReadValue<Vector3>();

            //Debug.Log(vec);

            float rotX = rotvec.x;
            float rotY = rotvec.y;
            float rotZ = rotvec.z;

            vec.x = ProcessRotationAxis(vec.x, ref rotX);
            vec.y = ProcessRotationAxis(vec.y, ref rotY);
            vec.z = ProcessRotationAxis(vec.z, ref rotZ);

            rotvec = new Vector3(rotX, rotY, rotZ);

            RotateMino(vec);

            await UniTask.Yield(token);
        }
    }

    // 移動入力のUpdate処理ループ.
    private async UniTaskVoid MovementUpdateLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // 入力取得.
            Vector2 vec = action.Player.Move.ReadValue<Vector2>();

            float moveX = movevec.x;
            float moveZ = movevec.y;

            vec.x = ProcessRotationAxis(vec.x, ref moveX);
            vec.y = ProcessRotationAxis(vec.y, ref moveZ);

            movevec = new Vector2(moveX, moveZ);

            // X軸とZ軸方向に移動.
            if (vec.x != 0 || vec.y != 0)
            {
                MoveMino(new Vector3(vec.x, 0, vec.y));
            }

            await UniTask.Yield(token);
        }
    }

    // 落下停止関数.
    public void StopFall()
    {
        isFalling = false;
        fallCancellationToken?.Cancel();
        rotationCancellationToken?.Cancel();
        movementCancellationToken?.Cancel();

        // ゴーストブロックを削除.
        ClearGhostBlocks();

        // 矢印を削除.
        ClearRotationArrows();
    }

    // ミノを生成.
    public void CreateMino(Vector3 centerPosition)
    {
        ClearBlocks();

        int sizeY = shapeData.GetSizeY();
        int sizeX = shapeData.GetSizeX();
        int sizeZ = shapeData.GetSizeZ();

        // 偶数サイズの場合、グリッドにスナップするための追加オフセット（2.5）.
        float snapOffsetX = (sizeX % 2 == 0) ? GRID_SIZE / 2 : 0;
        float snapOffsetZ = (sizeZ % 2 == 0) ? GRID_SIZE / 2 : 0;
        Vector3 adjustedCenter = centerPosition + new Vector3(snapOffsetX, 0, snapOffsetZ);

        // ブロック配置用オフセット計算（X,Zは中央、Yは最下面を基準）.
        float offsetX = (sizeX - 1) / 2f * GRID_SIZE;
        float offsetZ = (sizeZ - 1) / 2f * GRID_SIZE;

        // 回転軸の中心を計算（X,Z は中央+2.5、Y も中央）.
        Vector3 rotationCenter = adjustedCenter + new Vector3(
            2.5f,
            (sizeY - 1) / 2f * GRID_SIZE,
            2.5f
        );
        transform.position = rotationCenter;

        
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (shapeData.HasBlockAt(y, x, z))
                    {
                        // ブロックのローカル座標を計算（回転中心からの相対位置）.
                        Vector3 localBlockPos = new Vector3(
                            x * GRID_SIZE - offsetX,
                            y * GRID_SIZE - (sizeY - 1) / 2f * GRID_SIZE,
                            z * GRID_SIZE - offsetZ
                        );

                        GameObject block = GameMaster.Instance().GetBlockFromPool();
                        if (block != null)
                        {
                            block.transform.SetParent(transform);
                            block.transform.localPosition = localBlockPos;
                            block.transform.localRotation = Quaternion.identity;

                            // 明るめのランダムカラーを適用（HSVで彩度・明度を高めに設定）.
                            Renderer renderer = block.GetComponent<Renderer>();
                            if (renderer != null)
                            {
                                Color brightColor = Color.HSVToRGB(
                                    Random.value,
                                    Random.Range(0.7f, 1f),
                                    Random.Range(0.8f, 1f)
                                );
                                renderer.material.color = brightColor;
                            }

                            // MinoBlockPositionコンポーネントを追加または取得.
                            MinoBlockPosition blockPosition = block.GetComponent<MinoBlockPosition>();
                            if (blockPosition == null)
                            {
                                blockPosition = block.AddComponent<MinoBlockPosition>();
                            }

                            blocks.Add(block);
                        }
                    }
                }
            }
        }

        // ゴーストブロックを生成.
        CreateGhostBlocks();

        // 回転方向矢印を生成.
        CreateRotationArrows().Forget();
    }

    // 回転方向矢印を生成.
    private async UniTaskVoid CreateRotationArrows()
    {
        ClearRotationArrows();

        // 矢印用の親オブジェクトを作成(ミノに親子付けしない、ワールド空間で固定回転).
        arrowParent = new GameObject("RotationArrows");
        arrowParent.transform.position = transform.position;

        // Addressablesから矢印プレハブをロード.
        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>("Arrow_Curve");
        AsyncOperationHandle<GameObject> handleStraight = Addressables.LoadAssetAsync<GameObject>("Arrow_Straight");
        await UniTask.WhenAll(handle.Task.AsUniTask(), handleStraight.Task.AsUniTask());

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning("矢印プレハブの読み込みに失敗しました.");
            return;
        }

        if (handleStraight.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning("移動矢印プレハブの読み込みに失敗しました.");
            return;
        }

        GameObject arrowCurvePrefab = handle.Result;
        GameObject arrowStraightPrefab = handleStraight.Result;

        // X軸回転矢印(赤).
        arrowX = Instantiate(arrowCurvePrefab, arrowParent.transform);
        arrowX.name = "ArrowX";
        SetArrowColor(arrowX, ARROW_COLOR_X);

        // Y軸回転矢印(緑).
        arrowY = Instantiate(arrowCurvePrefab, arrowParent.transform);
        arrowY.name = "ArrowY";
        SetArrowColor(arrowY, ARROW_COLOR_Y);

        // Z軸回転矢印(青).
        arrowZ = Instantiate(arrowCurvePrefab, arrowParent.transform);
        arrowZ.name = "ArrowZ";
        SetArrowColor(arrowZ, ARROW_COLOR_Z);

        // X軸移動矢印(赤).
        moveArrowX = Instantiate(arrowStraightPrefab, arrowParent.transform);
        moveArrowX.name = "MoveArrowX";
        SetArrowColor(moveArrowX, MOVE_ARROW_COLOR_X);

        // Z軸移動矢印(青).
        moveArrowZ = Instantiate(arrowStraightPrefab, arrowParent.transform);
        moveArrowZ.name = "MoveArrowZ";
        SetArrowColor(moveArrowZ, MOVE_ARROW_COLOR_Z);

        // 矢印の位置と向きを更新.
        UpdateArrowPositions();
    }

    // 矢印の色を設定(透明対応).
    private void SetArrowColor(GameObject arrow, Color color)
    {
        if (arrow == null) return;

        Renderer[] renderers = arrow.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                // 透明モードに設定.
                SetMaterialTransparent(renderer.material);
                renderer.material.color = color;
            }
        }
    }

    // 矢印の位置と向きを更新.
    private void UpdateArrowPositions()
    {
        if (arrowParent == null) return;

        // 矢印の親をミノに追従させる.
        arrowParent.transform.position = transform.position;

        // 各軸の回転方向に矢印を配置(Arrow_Curve).
        if (arrowX != null)
        {
            // X軸回転(赤).
            arrowX.transform.localPosition = new Vector3(0f, 9.6f, -6.8f);
            arrowX.transform.localRotation = Quaternion.Euler(-135f, 0f, 90f);
            arrowX.transform.localScale = new Vector3(150f, 150f, 150f);
        }

        if (arrowY != null)
        {
            // Y軸回転(緑).
            arrowY.transform.localPosition = new Vector3(10f, 0f, 10f);
            arrowY.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            arrowY.transform.localScale = new Vector3(150f, 150f, 150f);
        }

        if (arrowZ != null)
        {
            // Z軸回転(青).
            arrowZ.transform.localPosition = new Vector3(6.8f, 9.6f, 0f);
            arrowZ.transform.localRotation = Quaternion.Euler(-45f, 90f, -90f);
            arrowZ.transform.localScale = new Vector3(150f, 150f, 150f);
        }

        // 移動方向矢印を配置(Arrow_Straight).
        if (moveArrowX != null)
        {
            // X軸移動(赤).
            moveArrowX.transform.localPosition = new Vector3(10f, 0f, 0f);
            moveArrowX.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            moveArrowX.transform.localScale = new Vector3(200f, 200f, 200f);
        }

        if (moveArrowZ != null)
        {
            // Z軸移動(青).
            moveArrowZ.transform.localPosition = new Vector3(0f, 0f, 10f);
            moveArrowZ.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            moveArrowZ.transform.localScale = new Vector3(200f, 200f, 200f);
        }
    }

    // 回転方向矢印をクリア.
    private void ClearRotationArrows()
    {
        if (arrowX != null)
        {
            Destroy(arrowX);
            arrowX = null;
        }
        if (arrowY != null)
        {
            Destroy(arrowY);
            arrowY = null;
        }
        if (arrowZ != null)
        {
            Destroy(arrowZ);
            arrowZ = null;
        }
        if (moveArrowX != null)
        {
            Destroy(moveArrowX);
            moveArrowX = null;
        }
        if (moveArrowZ != null)
        {
            Destroy(moveArrowZ);
            moveArrowZ = null;
        }
        if (arrowParent != null)
        {
            Destroy(arrowParent);
            arrowParent = null;
        }
    }

    // ゴーストブロックを生成.
    private void CreateGhostBlocks()
    {
        ClearGhostBlocks();

        // ゴースト用の親オブジェクトを作成.
        ghostParent = new GameObject("GhostBlocks");

        // 各ブロックに対応するゴーストブロックを生成.
        foreach (GameObject block in blocks)
        {
            if (block != null)
            {
                GameObject ghostBlock = GameMaster.Instance().GetBlockFromPool();
                if (ghostBlock != null)
                {
                    ghostBlock.transform.SetParent(ghostParent.transform);

                    // ゴーストブロックのマテリアルを透明に設定.
                    Renderer renderer = ghostBlock.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        // 透明モードに設定.
                        SetMaterialTransparent(renderer.material);
                        renderer.material.color = GHOST_COLOR;
                    }

                    // MinoBlockPositionがあれば無効化(ゴーストはGameMasterに登録しない).
                    MinoBlockPosition blockPosition = ghostBlock.GetComponent<MinoBlockPosition>();
                    if (blockPosition != null)
                    {
                        blockPosition.enabled = false;
                    }

                    ghostBlocks.Add(ghostBlock);
                }
            }
        }

        // ゴースト位置を更新.
        UpdateGhostPosition();
    }

    // マテリアルを透明モードに設定.
    private void SetMaterialTransparent(Material material)
    {
        material.SetFloat("_Mode", 3);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    // ゴーストブロックをクリア.
    private void ClearGhostBlocks()
    {
        foreach (GameObject ghostBlock in ghostBlocks)
        {
            if (ghostBlock != null)
            {
                ghostBlock.transform.SetParent(null);
                GameMaster.Instance().ReturnBlockToPool(ghostBlock);
            }
        }
        ghostBlocks.Clear();

        if (ghostParent != null)
        {
            Destroy(ghostParent);
            ghostParent = null;
        }
    }

    // ゴーストの位置を更新.
    private void UpdateGhostPosition()
    {
        if (ghostBlocks.Count == 0 || blocks.Count == 0) return;
        if (ghostBlocks.Count != blocks.Count) return;

        // 現在のミノ位置から落下可能な距離を計算.
        int dropDistance = CalculateDropDistance();

        // ゴーストブロックの位置を更新.
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] != null && ghostBlocks[i] != null)
            {
                Vector3 ghostPos = blocks[i].transform.position;
                ghostPos.y -= dropDistance * GRID_SIZE;
                ghostBlocks[i].transform.position = ghostPos;
                ghostBlocks[i].transform.rotation = blocks[i].transform.rotation;
            }
        }
    }

    // 落下可能な距離(グリッド単位)を計算.
    private int CalculateDropDistance()
    {
        // ブロック登録を一時解除して判定(自己衝突を防ぐ).
        UnregisterAllBlocks();

        int distance = 0;
        Vector3 testOffset = Vector3.zero;

        // 下方向に移動可能な限り距離を増やす.
        while (true)
        {
            testOffset = Vector3.down * (distance + 1) * GRID_SIZE;

            bool canMove = true;
            foreach (GameObject block in blocks)
            {
                if (block != null)
                {
                    // ブロックの移動先をチェック.
                    Vector3 targetWorldPos = block.transform.position + testOffset;
                    Vector3Int targetGridIndex = GameMaster.Instance().WorldToGridIndex(targetWorldPos);

                    // 範囲外チェック(Y下限とXZ範囲のみ、Y上限は無視).
                    if (targetGridIndex.y < 0 ||
                        targetGridIndex.x < 0 || targetGridIndex.x >= 10 ||
                        targetGridIndex.z < 0 || targetGridIndex.z >= 10)
                    {
                        canMove = false;
                        break;
                    }

                    // Y上限を超えている場合は衝突判定をスキップ(上空にブロックはない).
                    if (targetGridIndex.y >= 6)
                    {
                        continue;
                    }

                    // 既存ブロックとの衝突判定.
                    if (GameMaster.Instance().IsBlockExist(targetGridIndex))
                    {
                        canMove = false;
                        break;
                    }
                }
            }

            if (!canMove) break;
            distance++;

            // 安全のため最大値を設定.
            if (distance > 20) break;
        }

        // ブロックを再登録.
        RegisterAllBlocks();

        return distance;
    }

    // 全ブロックをプールに返却.
    public void ClearBlocks()
    {
        // ゴーストブロックもクリア.
        ClearGhostBlocks();

        // 回転方向矢印もクリア.
        ClearRotationArrows();

        foreach (GameObject block in blocks)
        {
            if (block != null)
            {
                MinoBlockPosition blockPosition = block.GetComponent<MinoBlockPosition>();
                if (blockPosition != null)
                {
                    blockPosition.UnregisterFromGameMaster();
                }
                block.transform.SetParent(null);
                GameMaster.Instance().ReturnBlockToPool(block);
            }
        }
        blocks.Clear();
    }

    // 全ブロックをGameMasterに登録.
    public void RegisterAllBlocks()
    {
        foreach (GameObject block in blocks)
        {
            MinoBlockPosition blockPosition = block.GetComponent<MinoBlockPosition>();
            if (blockPosition != null)
            {
                blockPosition.RegisterToGameMaster();
            }
        }
    }

    // 全ブロックをGameMasterから解除.
    public void UnregisterAllBlocks()
    {
        foreach (GameObject block in blocks)
        {
            MinoBlockPosition blockPosition = block.GetComponent<MinoBlockPosition>();
            if (blockPosition != null)
            {
                blockPosition.UnregisterFromGameMaster();
            }
        }
    }

    // 全ブロックが指定方向に移動可能か判定.
    public bool CanAllBlocksMove(Vector3 direction)
    {
        foreach (GameObject block in blocks)
        {
            MinoBlockPosition blockPosition = block.GetComponent<MinoBlockPosition>();
            if (blockPosition != null)
            {
                if (!blockPosition.CanMove(direction))
                {
                    return false;
                }
            }
        }
        return true;
    }

    // 全ブロックが指定方向に移動可能か判定(上方向制限オプション付き).
    public bool CanAllBlocksMove(Vector3 direction, bool ignoreUpperLimit)
    {
        foreach (GameObject block in blocks)
        {
            MinoBlockPosition blockPosition = block.GetComponent<MinoBlockPosition>();
            if (blockPosition != null)
            {
                if (!blockPosition.CanMove(direction, ignoreUpperLimit))
                {
                    return false;
                }
            }
        }
        return true;
    }

    // 下方向に移動可能か判定(着地状態チェック用).
    public bool CanMoveDown()
    {
        return CanAllBlocksMove(Vector3.down * GRID_SIZE);
    }

    // 着地状態を更新.
    private void UpdateGroundedState()
    {
        isGrounded = !CanMoveDown();
    }

    // ゲームオーバー判定(上限を超えているかチェック).
    public bool CheckGameOver()
    {
        // いずれかのブロックのY座標が上限を超えているかチェック(ブロック衝突チェックは行わない).
        foreach (GameObject block in blocks)
        {
            if (block != null)
            {
                Vector3Int gridPos = GameMaster.Instance().WorldToGridIndex(block.transform.position);
                if (gridPos.y >= 6)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ミノを移動.
    public void MoveMino(Vector3 direction)
    {
        Vector3 moveDistance = direction * GRID_SIZE;

        // 自分自身のブロックとの干渉を防ぐため、チェック前に登録解除.
        UnregisterAllBlocks();

        // 全ブロックが移動可能か確認.
        if (!CanAllBlocksMove(moveDistance))
        {
            // 移動不可の場合は再登録して終了.
            RegisterAllBlocks();
            return;
        }

        transform.position += moveDistance;
        RegisterAllBlocks();

        // 移動SE再生.
        AudioManager.Instance()?.PlaySE(SEType.Move);

        // 着地状態なら落下時間をリセット.
        UnregisterAllBlocks();
        UpdateGroundedState();
        RegisterAllBlocks();
        if (isGrounded)
        {
            lastFallTime = Time.time;
        }

        // ゴースト位置を更新.
        UpdateGhostPosition();

        // 矢印位置を更新.
        UpdateArrowPositions();
    }

    // ミノを回転.
    public void RotateMino(Vector3 axis)
    {
        if (axis == Vector3.zero) return;
        if (isRotating) return;

        UnregisterAllBlocks();
        float rotationAngle = Mathf.PI / 2 * Mathf.Rad2Deg;

        // 元の位置と回転を保存.
        Vector3 originalPosition = transform.position;
        Quaternion originalRotation = transform.rotation;

        // 回転を適用(判定用).
        transform.Rotate(axis * rotationAngle, Space.World);

        // 回転成功フラグ.
        bool rotationSuccess = false;
        Vector3 finalPosition = transform.position;
        Quaternion finalRotation = transform.rotation;

        // 回転後の位置で衝突チェック.
        if (CanAllBlocksMove(Vector3.zero))
        {
            rotationSuccess = true;
            finalPosition = transform.position;
            finalRotation = transform.rotation;
        }

        // 回転軸方向に1マス移動して確認.
        if (!rotationSuccess)
        {
            transform.position = originalPosition + axis.normalized * GRID_SIZE;
            if (CanAllBlocksMove(Vector3.zero))
            {
                rotationSuccess = true;
                finalPosition = transform.position;
                finalRotation = transform.rotation;
            }
        }

        // ワールド座標の1マス上に移動して確認.
        if (!rotationSuccess)
        {
            transform.position = originalPosition + Vector3.up * GRID_SIZE;
            if (CanAllBlocksMove(Vector3.zero))
            {
                rotationSuccess = true;
                finalPosition = transform.position;
                finalRotation = transform.rotation;
            }
        }

        // ワールド座標の1マス下に移動して確認.
        if (!rotationSuccess)
        {
            transform.position = originalPosition + Vector3.down * GRID_SIZE;
            if (CanAllBlocksMove(Vector3.zero))
            {
                rotationSuccess = true;
                finalPosition = transform.position;
                finalRotation = transform.rotation;
            }
        }

        // 回転成功時はアニメーション、失敗時は元に戻す.
        if (rotationSuccess)
        {
            // 回転SE再生.
            AudioManager.Instance()?.PlaySE(SEType.Rotate);

            // 元の状態に戻してからアニメーション開始.
            transform.position = originalPosition;
            transform.rotation = originalRotation;

            // 回転アニメーション実行.
            PlayRotationAnimation(originalPosition, originalRotation, finalPosition, finalRotation).Forget();
        }
        else
        {
            // 回転失敗SE再生.
            AudioManager.Instance()?.PlaySE(SEType.RotateFail);

            // 元に戻す.
            transform.position = originalPosition;
            transform.rotation = originalRotation;
            RegisterAllBlocks();
        }
    }

    // 回転アニメーションを再生.
    private async UniTaskVoid PlayRotationAnimation(Vector3 startPos, Quaternion startRot, Vector3 endPos, Quaternion endRot)
    {
        isRotating = true;

        float elapsedTime = 0f;
        while (elapsedTime < ROTATION_DURATION)
        {
            float t = elapsedTime / ROTATION_DURATION;
            // イージング(ease out).
            float easedT = 1f - Mathf.Pow(1f - t, 2f);

            transform.position = Vector3.Lerp(startPos, endPos, easedT);
            transform.rotation = Quaternion.Slerp(startRot, endRot, easedT);

            await UniTask.Yield();
            elapsedTime += Time.deltaTime;
        }

        // 最終位置を確定.
        transform.position = endPos;
        transform.rotation = endRot;

        RegisterAllBlocks();

        // 着地状態なら落下時間をリセット.
        UnregisterAllBlocks();
        UpdateGroundedState();
        RegisterAllBlocks();
        if (isGrounded)
        {
            lastFallTime = Time.time;
        }

        // ゴースト位置を更新.
        UpdateGhostPosition();

        // 矢印位置を更新.
        UpdateArrowPositions();

        isRotating = false;
    }

    // 単軸の回転入力処理(デッドゾーン適用と正規化).
    private float ProcessRotationAxis(float input, ref float prevInput, float deadzone = 0.1f)
    {
        // デッドゾーン設定.
        input = Mathf.Abs(input) <= deadzone ? 0 : input;

        if (prevInput == 0 && input != 0)
        {
            // 前回入力無ければ正規化しておく.
            prevInput = input > 0 ? 1 : -1;
            return prevInput;
        }
        else if (prevInput != 0 && input == 0)
        {
            // 入力がなくなったならそれを記録しておく.
            prevInput = 0;
        }
        return 0;
    }

    private void OnDestroy()
    {
        StopFall();
        ClearBlocks();
    }
}
