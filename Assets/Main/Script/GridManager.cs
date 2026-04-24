using System.Collections.Generic;
using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

// 盤面のサイズ・生成・ブロック状態管理を担当するクラス.
public class GridManager : Singleton_DestroyAvailableMonoSingleton<GridManager>
{
    // グリッドサイズ定義（ブロック数で指定）.
    [SerializeField]
    private Vector3Int gridBlockCount = new Vector3Int(5, 6, 5);

    // 1ブロックのサイズ.
    private const float GRID_SIZE = 5f;
    private const float POOL_Y_POSITION = -100f;

    // 動的に計算される範囲.
    private float yMin;
    private float yMax;
    private float xzMin;
    private float xzMax;

    // グリッドのサイズ.
    private int gridYSize;
    private int gridXZSize;

    // 公開プロパティ.
    public int GridYSize => gridYSize;
    public int GridXZSize => gridXZSize;
    public float CellSize => GRID_SIZE;
    public float YMin => yMin;
    public float YMax => yMax;
    public float XZMin => xzMin;
    public float XZMax => xzMax;

    // グリッド中心座標（カメラのターゲット用）.
    public Vector3 GridCenter => new Vector3(
        (xzMin + xzMax) / 2f,
        (yMin + yMax) / 2f,
        (xzMin + xzMax) / 2f
    );

    // 境界線ブロックリスト.
    private List<GameObject> borderBlocks = new List<GameObject>();

    // 境界線ブロックの色.
    private readonly Color borderColor1 = Color.white;
    private readonly Color borderColor2 = new Color(0.5f, 0.5f, 0.5f, 1f);

    // 床の内端ブロックの色（配置可能範囲の境界を示す濃いグレー）.
    private readonly Color floorInnerEdgeColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    // 壁面の法線方向（+X, -X, +Z, -Z）.
    private static readonly Vector3[] wallNormals = {
        Vector3.right,   // index 0: +X壁.
        Vector3.left,    // index 1: -X壁.
        Vector3.forward, // index 2: +Z壁.
        Vector3.back     // index 3: -Z壁.
    };

    // 壁面ごとのRendererリスト（4面）.
    private List<Renderer>[] wallRenderers;

    // カメラ正面の壁の最小α値.
    [SerializeField]
    private float wallNearTransparentAlpha = 0.05f;

    // 角ブロックとその隣接ブロック（斜め方向からの透明度補正用）.
    private HashSet<Renderer> cornerAdjacentRenderers = new HashSet<Renderer>();

    // レイキャスト遮蔽透明化用.
    private Dictionary<Collider, Vector3Int> wallColliderToGrid = new Dictionary<Collider, Vector3Int>();
    private Dictionary<Vector3Int, Renderer> wallBlockRenderers = new Dictionary<Vector3Int, Renderer>();
    private List<(Vector3 position, Collider collider)>[] floorEdgeBlocks; // 4辺: +X, -X, +Z, -Z.
    private RaycastHit[] raycastHitBuffer = new RaycastHit[16];
    private HashSet<Vector3Int> raycastTransparentSet = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> faceCandidateSet = new HashSet<Vector3Int>();

    // レイキャスト発射位置: ブロックの8角.
    private const float CORNER_HALF = GRID_SIZE * 0.5f - 0.1f;
    private static readonly Vector3[] blockCornerOffsets = {
        new Vector3(-CORNER_HALF, -CORNER_HALF, -CORNER_HALF),
        new Vector3(-CORNER_HALF, -CORNER_HALF,  CORNER_HALF),
        new Vector3(-CORNER_HALF,  CORNER_HALF, -CORNER_HALF),
        new Vector3(-CORNER_HALF,  CORNER_HALF,  CORNER_HALF),
        new Vector3( CORNER_HALF, -CORNER_HALF, -CORNER_HALF),
        new Vector3( CORNER_HALF, -CORNER_HALF,  CORNER_HALF),
        new Vector3( CORNER_HALF,  CORNER_HALF, -CORNER_HALF),
        new Vector3( CORNER_HALF,  CORNER_HALF,  CORNER_HALF),
    };

    // ブロック存在管理用3D配列 [y][x][z] (0: 非存在, 1: 存在, 2: 削除位置).
    private int[,,] blockState;

    // ブロックGameObject参照用3D配列 [y][x][z].
    private GameObject[,,] blockObjects;

    // ブロック状態の定数.
    private const int BLOCK_EMPTY = 0;
    private const int BLOCK_EXIST = 1;
    private const int BLOCK_DELETED = 2;

    // 各マスの最大高さ記録用配列 [x][z] ミノ停止判定の簡略化用.
    private int[,] heightMap;

    // オブジェクトプール.
    private Queue<GameObject> blockPool = new Queue<GameObject>();
    [SerializeField]
    private int poolInitialSize = 50;
    [SerializeField]
    private string blockAddressableKey = "Block";

    // ブロックプレハブ参照.
    private GameObject blockPrefab;
    // プレハブ読み込み完了フラグ.
    private bool isPrefabLoaded = false;

    // 盤面上のアクティブなブロックをすべて管理するList.
    private List<GameObject> activeBlocks = new List<GameObject>();

    // X-ray vision 深度（この値を超えるYのブロックを透明化）.
    private int xrayDepth;

    // X-ray vision のα値.
    private const float XRAY_ALPHA = 0.1f;
    private const float NORMAL_ALPHA = 1.0f;

    // スコア表示View.
    private Score_View scoreView;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        // シーンロード毎に呼ばれるコールバックを登録.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // シーンロード時にInGameならGridManagerを生成.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (System.Enum.TryParse<UseScene>(scene.name, out UseScene useScene) && useScene == UseScene.InGame)
        {
            GridManager.Instance();
        }
    }

    private void Start()
    {
        InitializeGrid();
        InitializePool().Forget();
        CreateBorderBlocks().Forget();
    }

    private void LateUpdate()
    {
        UpdateWallTransparency();
    }

    // カメラ方向に応じて壁面の透明度を更新.
    private void UpdateWallTransparency()
    {
        if (wallRenderers == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // カメラからGridCenterへの方向（XZ平面）.
        Vector3 dirToCamera = cam.transform.position - GridCenter;
        dirToCamera.y = 0f;
        if (dirToCamera.sqrMagnitude < 0.001f) return;
        dirToCamera.Normalize();

        // 各壁面のα値を計算（facingFactor: 内積 -1〜1）.
        // 0°=1.0, 15°=0.1, 45°=wallNearTransparentAlpha の区間補間.
        const float FACTOR_15DEG = 0.2588f; // sin(15°).
        const float FACTOR_45DEG = 0.7071f; // sin(45°).
        const float ALPHA_AT_15DEG = 0.1f;

        float[] faceAlphas = new float[4];
        for (int i = 0; i < 4; i++)
        {
            float f = Vector3.Dot(wallNormals[i], dirToCamera);

            if (f <= 0f)
            {
                faceAlphas[i] = 1f;
            }
            else if (f <= FACTOR_15DEG)
            {
                // 0° → 15°: α 1.0 → 0.1.
                faceAlphas[i] = Mathf.Lerp(1f, ALPHA_AT_15DEG, f / FACTOR_15DEG);
            }
            else if (f <= FACTOR_45DEG)
            {
                // 15° → 45°: α 0.1 → wallNearTransparentAlpha.
                faceAlphas[i] = Mathf.Lerp(ALPHA_AT_15DEG, wallNearTransparentAlpha,
                    (f - FACTOR_15DEG) / (FACTOR_45DEG - FACTOR_15DEG));
            }
            else
            {
                // 45° → 90°: 最小値で固定.
                faceAlphas[i] = wallNearTransparentAlpha;
            }
        }

        // まず全壁ブロックを不透明に設定.
        for (int i = 0; i < 4; i++)
        {
            foreach (Renderer renderer in wallRenderers[i])
            {
                if (renderer == null) continue;
                SetBlockAlpha(renderer, 1f);
            }
        }

        // 面ベース透明化の候補セットを構築（面α < 1のブロック）.
        faceCandidateSet.Clear();
        foreach (var kvp in wallBlockRenderers)
        {
            float alpha = GetBlockFaceAlpha(kvp.Key, faceAlphas);
            if (alpha < 1f)
            {
                faceCandidateSet.Add(kvp.Key);
            }
        }

        // レイキャストで遮蔽確認された候補のみ透明化.
        UpdateRaycastTransparency(cam.transform.position, faceAlphas);

        // 斜め方向から見ているとき、確認済み透明ブロックのうち角付近のものを補正.
        float[] factors = new float[4];
        for (int i = 0; i < 4; i++)
        {
            factors[i] = Vector3.Dot(wallNormals[i], dirToCamera);
        }
        System.Array.Sort(factors);
        float secondHighest = factors[2];

        if (secondHighest > 0.1f)
        {
            foreach (Renderer renderer in cornerAdjacentRenderers)
            {
                if (renderer == null) continue;
                Color color = renderer.material.color;
                if (color.a >= 1f) continue; // レイキャスト未確認のブロックはスキップ.
                color.a = Mathf.Max(wallNearTransparentAlpha, color.a - 0.1f);
                renderer.material.color = color;
            }
        }
    }

    // レイキャストで遮蔽確認された面ベース候補のみ透明化.
    private void UpdateRaycastTransparency(Vector3 camPos, float[] faceAlphas)
    {
        raycastTransparentSet.Clear();

        // カメラから最も遠い角を特定.
        Vector3 dirFromCenter = camPos - GridCenter;
        int farCornerX = dirFromCenter.x > 0 ? -1 : gridXZSize;
        int farCornerZ = dirFromCenter.z > 0 ? -1 : gridXZSize;

        // 非候補の壁ブロック（不透明）からカメラへレイキャスト（1列おきにスキップ）.
        foreach (var kvp in wallColliderToGrid)
        {
            Collider col = kvp.Key;
            Vector3Int gp = kvp.Value;
            if (col == null) continue;

            // 最も遠い角の列はスキップ.
            if (gp.x == farCornerX && gp.z == farCornerZ) continue;

            // 候補ブロックはレイキャスト元にしない（不透明ブロックからのみ発射）.
            if (faceCandidateSet.Contains(gp)) continue;

            // 1列判定したら次の列をスキップ（列の偶奇で交互）.
            if (((gp.x + gp.z) & 1) != 0) continue;

            CastAndCollect(col.transform.position, col, gp, camPos);
        }

        // カメラ方向の上位2面を特定.
        int[] faceOrder = { 0, 1, 2, 3 };
        System.Array.Sort(faceOrder, (a, b) =>
            Vector3.Dot(wallNormals[b], dirFromCenter.normalized).CompareTo(
            Vector3.Dot(wallNormals[a], dirFromCenter.normalized)));
        int topFace1 = faceOrder[0];
        int topFace2 = faceOrder[1];

        // 内縁の床ブロックからカメラへレイキャスト（カメラ方向の面+次の面のみ）.
        Vector3Int dummyFloorPos = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        foreach (var fb in floorEdgeBlocks[topFace1])
        {
            CastAndCollect(fb.position, fb.collider, dummyFloorPos, camPos);
        }
        foreach (var fb in floorEdgeBlocks[topFace2])
        {
            CastAndCollect(fb.position, fb.collider, dummyFloorPos, camPos);
        }

        // 確認された候補ブロックに面ベースα値を適用.
        foreach (Vector3Int pos in raycastTransparentSet)
        {
            if (wallBlockRenderers.TryGetValue(pos, out Renderer rend) && rend != null)
            {
                float alpha = GetBlockFaceAlpha(pos, faceAlphas);
                SetBlockAlpha(rend, alpha);
            }
        }
    }

    // ブロックの8角からレイキャストし遮蔽壁ブロックを収集（候補かつ同列以外のみ）.
    private void CastAndCollect(Vector3 blockCenter, Collider sourceCollider, Vector3Int sourceGridPos, Vector3 camPos)
    {
        for (int c = 0; c < blockCornerOffsets.Length; c++)
        {
            Vector3 origin = blockCenter + blockCornerOffsets[c];
            Vector3 dir = camPos - origin;
            float dist = dir.magnitude;
            if (dist < 0.1f) continue;
            dir /= dist;

            int hitCount = Physics.RaycastNonAlloc(origin, dir, raycastHitBuffer, dist);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCol = raycastHitBuffer[i].collider;
                if (hitCol == sourceCollider) continue; // 自身をスキップ.

                if (wallColliderToGrid.TryGetValue(hitCol, out Vector3Int gridPos))
                {
                    // 同列は無視（自身の上にいるブロックは透明化しない）.
                    if (gridPos.x == sourceGridPos.x && gridPos.z == sourceGridPos.z) continue;

                    // 面ベース候補のみ透明化対象.
                    if (faceCandidateSet.Contains(gridPos))
                    {
                        raycastTransparentSet.Add(gridPos);
                    }
                    // 同じ列のそれより上のブロックも候補であれば透明化.
                    for (int y = gridPos.y + 1; y < gridYSize; y++)
                    {
                        Vector3Int above = new Vector3Int(gridPos.x, y, gridPos.z);
                        if (faceCandidateSet.Contains(above))
                        {
                            raycastTransparentSet.Add(above);
                        }
                    }
                }
            }
        }
    }

    // 壁ブロックの面ベースα値を取得（複数面に所属する場合は最小値）.
    private float GetBlockFaceAlpha(Vector3Int gridPos, float[] faceAlphas)
    {
        float alpha = 1f;
        if (gridPos.x == gridXZSize) alpha = Mathf.Min(alpha, faceAlphas[0]); // +X壁.
        if (gridPos.x == -1) alpha = Mathf.Min(alpha, faceAlphas[1]);         // -X壁.
        if (gridPos.z == gridXZSize) alpha = Mathf.Min(alpha, faceAlphas[2]); // +Z壁.
        if (gridPos.z == -1) alpha = Mathf.Min(alpha, faceAlphas[3]);         // -Z壁.
        return alpha;
    }

    // グリッド初期化.
    public void InitializeGrid()
    {
        // Vector3Intからグリッドサイズを設定.
        gridXZSize = gridBlockCount.x;
        gridYSize = gridBlockCount.y;
        int gridZSize = gridBlockCount.z;

        // XZは同じサイズを使用（大きい方を採用）.
        gridXZSize = Mathf.Max(gridXZSize, gridZSize);

        // 範囲を動的に計算（中心を0とする）.
        float halfXZ = (gridXZSize - 1) * GRID_SIZE / 2f;
        float halfY = (gridYSize - 1) * GRID_SIZE / 2f;

        xzMin = -halfXZ;
        xzMax = halfXZ;
        yMin = -halfY;
        yMax = halfY;

        blockState = new int[gridYSize, gridXZSize, gridXZSize];
        blockObjects = new GameObject[gridYSize, gridXZSize, gridXZSize];
        heightMap = new int[gridXZSize, gridXZSize];

        // X-ray vision 深度を上限に初期化.
        xrayDepth = gridYSize;
    }

    // ミノのスポーン位置を取得（グリッドパリティを考慮）.
    public Vector3 GetMinoSpawnPosition()
    {
        // グリッドの中央インデックスのワールド座標を計算.
        int centerIndexXZ = (gridXZSize - 1) / 2;
        float spawnX = xzMin + centerIndexXZ * GRID_SIZE;
        float spawnZ = xzMin + centerIndexXZ * GRID_SIZE;
        float spawnY = yMax + GRID_SIZE;
        return new Vector3(spawnX, spawnY, spawnZ);
    }

    // グリッドのXZが偶数かどうかを返す（ミノのオフセット計算用）.
    public bool IsGridXZEven()
    {
        return gridXZSize % 2 == 0;
    }

    // 指定サイズのミノがグリッドXZ範囲内に収まるか判定.
    public bool CanMinoFit(int minoSizeX, int minoSizeZ)
    {
        return minoSizeX <= gridXZSize && minoSizeZ <= gridXZSize;
    }

    // 境界線ブロックを生成.
    private async UniTask CreateBorderBlocks()
    {
        // プレハブ読み込み完了を待機.
        await WaitForPrefabLoaded();

        // 壁面Rendererリストを初期化.
        wallRenderers = new List<Renderer>[4];
        floorEdgeBlocks = new List<(Vector3, Collider)>[4];
        for (int i = 0; i < 4; i++)
        {
            wallRenderers[i] = new List<Renderer>();
            floorEdgeBlocks[i] = new List<(Vector3, Collider)>();
        }

        // 境界線の位置は指定範囲の一個分外側.
        int borderXZMin = -1;
        int borderXZMax = gridXZSize;
        int borderYMin = -1;

        // 底面の境界線ブロックを生成（Y=-1の平面）.
        for (int x = borderXZMin; x <= borderXZMax; x++)
        {
            for (int z = borderXZMin; z <= borderXZMax; z++)
            {
                CreateBorderBlock(x, borderYMin, z);
            }
        }

        // 四方の壁の境界線ブロックを生成.
        for (int y = 0; y < gridYSize; y++)
        {
            // X方向の壁（Z=-1とZ=gridXZSize）.
            for (int x = borderXZMin; x <= borderXZMax; x++)
            {
                CreateBorderBlock(x, y, borderXZMin);
                CreateBorderBlock(x, y, borderXZMax);
            }
            // Z方向の壁（X=-1とX=gridXZSize、角は除く）.
            for (int z = 0; z < gridXZSize; z++)
            {
                CreateBorderBlock(borderXZMin, y, z);
                CreateBorderBlock(borderXZMax, y, z);
            }
        }

        Debug.Log($"境界線ブロック生成完了: {borderBlocks.Count}個");
    }

    // 境界線ブロックを1つ生成.
    private void CreateBorderBlock(int gridX, int gridY, int gridZ)
    {
        GameObject block = GetBlockFromPool();
        if (block == null) return;

        // 位置を計算（グリッド外なので直接計算）.
        float worldX = xzMin + gridX * GRID_SIZE;
        float worldY = yMin + gridY * GRID_SIZE;
        float worldZ = xzMin + gridZ * GRID_SIZE;
        block.transform.position = new Vector3(worldX, worldY, worldZ);

        // ブロックの色を設定.
        Renderer renderer = block.GetComponent<Renderer>();
        if (renderer != null)
        {
            // 床の壁直下（壁の真下の1列）を濃いグレーで強調.
            bool isFloorWallBase = (gridY == -1)
                && (gridX == -1 || gridX == gridXZSize || gridZ == -1 || gridZ == gridXZSize);

            if (isFloorWallBase)
            {
                renderer.material.color = floorInnerEdgeColor;
            }
            else
            {
                // 白と灰色のチェッカーパターン.
                bool isWhite = (gridX + gridY + gridZ) % 2 == 0;
                renderer.material.color = isWhite ? borderColor1 : borderColor2;
            }

            // 壁ブロック（床以外）を壁面ごとのリストに分類.
            int borderXZMinVal = -1;
            int borderXZMaxVal = gridXZSize;
            if (gridY >= 0)
            {
                if (gridX == borderXZMaxVal) wallRenderers[0].Add(renderer); // +X壁.
                if (gridX == borderXZMinVal) wallRenderers[1].Add(renderer); // -X壁.
                if (gridZ == borderXZMaxVal) wallRenderers[2].Add(renderer); // +Z壁.
                if (gridZ == borderXZMinVal) wallRenderers[3].Add(renderer); // -Z壁.

                // 角ブロックまたは角に隣接するブロックを記録.
                bool xAtEdge = (gridX == borderXZMinVal || gridX == borderXZMaxVal);
                bool zAtEdge = (gridZ == borderXZMinVal || gridZ == borderXZMaxVal);
                bool xNearEdge = (gridX == borderXZMinVal + 1 || gridX == borderXZMaxVal - 1);
                bool zNearEdge = (gridZ == borderXZMinVal + 1 || gridZ == borderXZMaxVal - 1);

                if ((xAtEdge && zAtEdge) || (xAtEdge && zNearEdge) || (zAtEdge && xNearEdge))
                {
                    cornerAdjacentRenderers.Add(renderer);
                }

                // レイキャスト用にColliderとグリッド位置を記録.
                Collider col = block.GetComponent<Collider>();
                if (col != null)
                {
                    Vector3Int gridPos = new Vector3Int(gridX, gridY, gridZ);
                    wallColliderToGrid[col] = gridPos;
                    wallBlockRenderers[gridPos] = renderer;
                }
            }
            else if (gridY == -1 && gridX >= 0 && gridX < gridXZSize && gridZ >= 0 && gridZ < gridXZSize)
            {
                // 内縁の床ブロックを辺ごとに分類（レイキャスト用）.
                bool isEdge = (gridX == 0 || gridX == gridXZSize - 1 || gridZ == 0 || gridZ == gridXZSize - 1);
                if (isEdge)
                {
                    Collider floorCol = block.GetComponent<Collider>();
                    if (floorCol != null)
                    {
                        var entry = (block.transform.position, floorCol);
                        if (gridX == gridXZSize - 1) floorEdgeBlocks[0].Add(entry); // +X辺.
                        if (gridX == 0)              floorEdgeBlocks[1].Add(entry); // -X辺.
                        if (gridZ == gridXZSize - 1) floorEdgeBlocks[2].Add(entry); // +Z辺.
                        if (gridZ == 0)              floorEdgeBlocks[3].Add(entry); // -Z辺.
                    }
                }
            }
        }

        borderBlocks.Add(block);
    }

    // オブジェクトプール初期化.
    private async UniTask InitializePool()
    {
        const int INITIAL_LOAD_COUNT = 16;
        const int LOAD_PER_SECOND = 16;

        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(blockAddressableKey);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            blockPrefab = handle.Result;
            isPrefabLoaded = true;
            int loadedCount = 0;

            // 最初に16個読み込み.
            int initialCount = Mathf.Min(INITIAL_LOAD_COUNT, poolInitialSize);
            for (int i = 0; i < initialCount; i++)
            {
                GameObject block = Instantiate(blockPrefab, new Vector3(0, POOL_Y_POSITION, 0), Quaternion.identity);
                block.SetActive(false);
                blockPool.Enqueue(block);
                loadedCount++;
            }

            // 残りを1秒ごとに16個ずつ読み込み.
            while (loadedCount < poolInitialSize)
            {
                await UniTask.Delay(1000);
                int batchCount = Mathf.Min(LOAD_PER_SECOND, poolInitialSize - loadedCount);
                for (int i = 0; i < batchCount; i++)
                {
                    GameObject block = Instantiate(blockPrefab, new Vector3(0, POOL_Y_POSITION, 0), Quaternion.identity);
                    block.SetActive(false);
                    blockPool.Enqueue(block);
                    loadedCount++;
                }
            }
        }
    }

    // プレハブ読み込み完了まで待機.
    public async UniTask WaitForPrefabLoaded()
    {
        while (!isPrefabLoaded)
        {
            await UniTask.Yield();
        }
    }

    // プールからブロック取得.
    public GameObject GetBlockFromPool()
    {
        if (blockPool.Count > 0)
        {
            GameObject block = blockPool.Dequeue();
            block.SetActive(true);
            return block;
        }
        // プールが空でプレハブが読み込み済みなら新規作成.
        if (isPrefabLoaded && blockPrefab != null)
        {
            GameObject block = Instantiate(blockPrefab, new Vector3(0, POOL_Y_POSITION, 0), Quaternion.identity);
            block.SetActive(true);
            return block;
        }
        return null;
    }

    // ブロックをプールに返却.
    public void ReturnBlockToPool(GameObject block)
    {
        // activeBlocksリストから削除.
        activeBlocks.Remove(block);

        block.SetActive(false);
        block.transform.position = new Vector3(0, POOL_Y_POSITION, 0);
        blockPool.Enqueue(block);
    }

    // ワールド座標からグリッドインデックスに変換.
    public Vector3Int WorldToGridIndex(Vector3 worldPos)
    {
        int y = Mathf.RoundToInt((worldPos.y - yMin) / GRID_SIZE);
        int x = Mathf.RoundToInt((worldPos.x - xzMin) / GRID_SIZE);
        int z = Mathf.RoundToInt((worldPos.z - xzMin) / GRID_SIZE);
        return new Vector3Int(x, y, z);
    }

    // グリッドインデックスからワールド座標に変換.
    public Vector3 GridIndexToWorld(Vector3Int gridIndex)
    {
        float y = yMin + gridIndex.y * GRID_SIZE;
        float x = xzMin + gridIndex.x * GRID_SIZE;
        float z = xzMin + gridIndex.z * GRID_SIZE;
        return new Vector3(x, y, z);
    }

    // Score_Viewを設定.
    public void SetScoreView(Score_View view)
    {
        scoreView = view;
    }

    // 指定位置にブロックが存在するか確認.
    public bool IsBlockExist(Vector3Int gridIndex)
    {
        if (!IsValidGridIndex(gridIndex)) return true;
        return blockState[gridIndex.y, gridIndex.x, gridIndex.z] == BLOCK_EXIST;
    }

    // ブロックの存在を登録.
    public void RegisterBlock(Vector3Int gridIndex)
    {
        RegisterBlock(gridIndex, null);
    }

    // ブロックの存在を登録(GameObject参照付き).
    public void RegisterBlock(Vector3Int gridIndex, GameObject blockObject)
    {
        if (IsValidGridIndex(gridIndex))
        {
            blockState[gridIndex.y, gridIndex.x, gridIndex.z] = BLOCK_EXIST;
            blockObjects[gridIndex.y, gridIndex.x, gridIndex.z] = blockObject;
            // 高さマップ更新(より高い位置なら更新).
            if (gridIndex.y + 1 > heightMap[gridIndex.x, gridIndex.z])
            {
                heightMap[gridIndex.x, gridIndex.z] = gridIndex.y + 1;
            }

            // activeBlocksリストに追加.
            if (blockObject != null && !activeBlocks.Contains(blockObject))
            {
                activeBlocks.Add(blockObject);
            }
        }
    }

    // ブロックの存在を解除.
    public void UnregisterBlock(Vector3Int gridIndex)
    {
        if (IsValidGridIndex(gridIndex))
        {
            // activeBlocksリストから削除.
            GameObject blockObj = blockObjects[gridIndex.y, gridIndex.x, gridIndex.z];
            if (blockObj != null)
            {
                activeBlocks.Remove(blockObj);
            }

            blockState[gridIndex.y, gridIndex.x, gridIndex.z] = BLOCK_EMPTY;
            blockObjects[gridIndex.y, gridIndex.x, gridIndex.z] = null;
            // 高さマップ再計算(解除位置が最大高さだった場合).
            if (gridIndex.y + 1 == heightMap[gridIndex.x, gridIndex.z])
            {
                int newHeight = 0;
                for (int y = gridIndex.y; y >= 0; y--)
                {
                    if (blockState[y, gridIndex.x, gridIndex.z] == BLOCK_EXIST)
                    {
                        newHeight = y + 1;
                        break;
                    }
                }
                heightMap[gridIndex.x, gridIndex.z] = newHeight;
            }
        }
    }

    // 指定XZ位置の最大高さを取得.
    public int GetHeightAt(int x, int z)
    {
        if (x >= 0 && x < gridXZSize && z >= 0 && z < gridXZSize)
        {
            return heightMap[x, z];
        }
        return gridYSize;
    }

    // グリッドインデックスが有効範囲内か確認.
    public bool IsValidGridIndex(Vector3Int gridIndex)
    {
        return gridIndex.y >= 0 && gridIndex.y < gridYSize &&
               gridIndex.x >= 0 && gridIndex.x < gridXZSize &&
               gridIndex.z >= 0 && gridIndex.z < gridXZSize;
    }

    // X-ray vision 深度を取得.
    public int XrayDepth => xrayDepth;

    // X-ray vision 深度を変更.
    public void AdjustXrayDepth(int delta)
    {
        xrayDepth = Mathf.Clamp(xrayDepth + delta, 0, gridYSize);
        UpdateXrayVisibility();
        Debug.Log($"X-ray depth: {xrayDepth}/{gridYSize}");
    }

    // X-ray vision による透明度更新.
    private void UpdateXrayVisibility()
    {
        foreach (GameObject block in activeBlocks)
        {
            if (block == null) continue;

            Vector3Int gridIndex = WorldToGridIndex(block.transform.position);
            Renderer renderer = block.GetComponent<Renderer>();
            if (renderer == null || renderer.material == null) continue;

            // xrayDepth を超えるYのブロックを透明化.
            if (gridIndex.y >= xrayDepth)
            {
                SetBlockAlpha(renderer, XRAY_ALPHA);
            }
            else
            {
                SetBlockAlpha(renderer, NORMAL_ALPHA);
            }
        }
    }

    // ブロックのα値を設定.
    private void SetBlockAlpha(Renderer renderer, float alpha)
    {
        Material mat = renderer.material;
        Color color = mat.color;
        color.a = alpha;

        if (alpha < 1.0f)
        {
            // 透明モードに設定.
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        else
        {
            // 不透明モードに設定.
            mat.SetFloat("_Mode", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }

        mat.color = color;
    }

    // 消去処理と詰め処理を実行.
    public async UniTask CheckAndClearLines()
    {
        // 削除位置リスト.
        List<Vector3Int> deletedPositions = new List<Vector3Int>();

        // 消去処理: 全高さに対して実行.
        for (int y = 0; y < gridYSize; y++)
        {
            // XZ方向に一列揃っているかチェック.
            CheckAndMarkXZLines(y, deletedPositions);
            // 斜めに一列揃っているかチェック.
            CheckAndMarkDiagonalLines(y, deletedPositions);
        }

        // 削除位置がなければ終了.
        if (deletedPositions.Count == 0)
        {
            return;
        }

        Debug.Log($"CheckAndClearLines: 削除対象ブロック数={deletedPositions.Count}");

        // ライン消去SE再生.
        AudioManager.Instance()?.PlaySE(SEType.LineClear);

        // 削除対象のブロックGameObjectを収集.
        List<GameObject> blocksToDelete = new List<GameObject>();
        foreach (Vector3Int pos in deletedPositions)
        {
            GameObject blockObj = blockObjects[pos.y, pos.x, pos.z];
            if (blockObj != null)
            {
                // 親から切り離す.
                blockObj.transform.SetParent(null);
                blocksToDelete.Add(blockObj);
            }
        }

        // 点滅演出を実行.
        await PlayDeleteAnimation(blocksToDelete);

        // スコア加算: ((削除block * 10) + ((削除line * 2) * 100)) * 削除line.
        int deletedBlocks = deletedPositions.Count;
        int lineCount = Mathf.CeilToInt((float)deletedBlocks / gridXZSize);
        int earnedPoints = ((deletedBlocks * 10) + ((lineCount * 2) * 100)) * lineCount;
        if (scoreView != null)
        {
            string formula = $"(({deletedBlocks}×10)+(({lineCount}×2)×100))×{lineCount}";
            string historyEntry = $"+{earnedPoints}pt {formula}";
            scoreView.AddScore(earnedPoints, historyEntry);
        }

        // 削除対象のブロックGameObjectをプールに返却.
        foreach (Vector3Int pos in deletedPositions)
        {
            GameObject blockObj = blockObjects[pos.y, pos.x, pos.z];
            if (blockObj != null)
            {
                ReturnBlockToPool(blockObj);
                blockObjects[pos.y, pos.x, pos.z] = null;
            }
            blockState[pos.y, pos.x, pos.z] = BLOCK_EMPTY;
        }

        // 詰め処理: 影響を受けるXZ列を特定.
        HashSet<Vector2Int> affectedColumns = new HashSet<Vector2Int>();
        foreach (Vector3Int pos in deletedPositions)
        {
            affectedColumns.Add(new Vector2Int(pos.x, pos.z));
        }

        // 各列でブロックを詰める処理.
        foreach (Vector2Int column in affectedColumns)
        {
            CompactColumn(column.x, column.y);
        }

        // 高さマップを再計算.
        RecalculateHeightMap();

        // 全ブロックの位置を再確認.
        RevalidateAllBlocks();

        // 削除後0.5秒待機.
        await UniTask.Delay(500);
    }

    // 削除演出: アルファ値をsin波で点滅させる.
    private async UniTask PlayDeleteAnimation(List<GameObject> blocks)
    {
        const float ANIMATION_DURATION = 0.8f;
        const float WAVE_FREQUENCY = 3f;

        // 各ブロックのRendererとオリジナルカラーを取得、透明モードに設定.
        List<(Renderer renderer, Color originalColor)> rendererData = new List<(Renderer, Color)>();
        foreach (GameObject block in blocks)
        {
            if (block != null)
            {
                Renderer renderer = block.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                {
                    // マテリアルを透明モードに設定.
                    SetMaterialTransparent(renderer.material);
                    rendererData.Add((renderer, renderer.material.color));
                }
            }
        }

        // sin波による点滅処理.
        float elapsedTime = 0f;
        while (elapsedTime < ANIMATION_DURATION)
        {
            // sin波でアルファ値を計算(0〜1の範囲).
            float alpha = (Mathf.Sin(elapsedTime * WAVE_FREQUENCY * Mathf.PI * 2f) + 1f) * 0.5f;

            foreach (var data in rendererData)
            {
                if (data.renderer != null)
                {
                    Color color = data.originalColor;
                    color.a = alpha;
                    data.renderer.material.color = color;
                }
            }

            await UniTask.Yield();
            elapsedTime += Time.deltaTime;
        }
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

    // 指定XZ列のブロックを詰める.
    private void CompactColumn(int x, int z)
    {
        int writeY = 0;

        for (int readY = 0; readY < gridYSize; readY++)
        {
            // blockState と blockObjects の両方をチェックし、どちらかが存在すれば処理する.
            bool hasState = blockState[readY, x, z] == BLOCK_EXIST;
            bool hasObject = blockObjects[readY, x, z] != null;

            // 不整合を検出してログ出力.
            if (hasState != hasObject)
            {
                Debug.LogWarning($"CompactColumn: 不整合検出 ({x}, {readY}, {z}) - state={hasState}, object={hasObject}");
            }

            // どちらかが存在すれば、ブロックとして処理する.
            if (hasState || hasObject)
            {
                if (readY != writeY)
                {
                    // 状態を移動.
                    blockState[writeY, x, z] = BLOCK_EXIST;
                    blockState[readY, x, z] = BLOCK_EMPTY;

                    // GameObjectを移動.
                    GameObject blockObj = blockObjects[readY, x, z];
                    if (blockObj != null)
                    {
                        // ワールド座標を更新.
                        Vector3 newWorldPos = GridIndexToWorld(new Vector3Int(x, writeY, z));
                        blockObj.transform.position = newWorldPos;

                        // 親から切り離す.
                        blockObj.transform.SetParent(null);
                    }

                    // 配列の参照を移動.
                    blockObjects[writeY, x, z] = blockObj;
                    blockObjects[readY, x, z] = null;
                }
                writeY++;
            }
        }
    }

    // XZ方向に一列揃っているかチェックして削除マーク.
    private void CheckAndMarkXZLines(int y, List<Vector3Int> deletedPositions)
    {
        // X方向の列チェック(各Zラインごと).
        for (int z = 0; z < gridXZSize; z++)
        {
            bool lineComplete = true;
            for (int x = 0; x < gridXZSize; x++)
            {
                if (blockState[y, x, z] != BLOCK_EXIST)
                {
                    lineComplete = false;
                    break;
                }
            }
            if (lineComplete)
            {
                for (int x = 0; x < gridXZSize; x++)
                {
                    MarkAsDeleted(y, x, z, deletedPositions);
                }
            }
        }

        // Z方向の列チェック(各Xラインごと).
        for (int x = 0; x < gridXZSize; x++)
        {
            bool lineComplete = true;
            for (int z = 0; z < gridXZSize; z++)
            {
                if (blockState[y, x, z] != BLOCK_EXIST)
                {
                    lineComplete = false;
                    break;
                }
            }
            if (lineComplete)
            {
                for (int z = 0; z < gridXZSize; z++)
                {
                    MarkAsDeleted(y, x, z, deletedPositions);
                }
            }
        }
    }

    // 斜めに一列揃っているかチェックして削除マーク.
    private void CheckAndMarkDiagonalLines(int y, List<Vector3Int> deletedPositions)
    {
        // 斜め1: (0,0) -> (n,n) 方向.
        bool diag1Complete = true;
        for (int i = 0; i < gridXZSize; i++)
        {
            if (blockState[y, i, i] != BLOCK_EXIST)
            {
                diag1Complete = false;
                break;
            }
        }
        if (diag1Complete)
        {
            for (int i = 0; i < gridXZSize; i++)
            {
                MarkAsDeleted(y, i, i, deletedPositions);
            }
        }

        // 斜め2: (0,n-1) -> (n,0) 方向.
        bool diag2Complete = true;
        for (int i = 0; i < gridXZSize; i++)
        {
            if (blockState[y, i, gridXZSize - 1 - i] != BLOCK_EXIST)
            {
                diag2Complete = false;
                break;
            }
        }
        if (diag2Complete)
        {
            for (int i = 0; i < gridXZSize; i++)
            {
                MarkAsDeleted(y, i, gridXZSize - 1 - i, deletedPositions);
            }
        }
    }

    // ブロックを削除済みとしてマーク.
    private void MarkAsDeleted(int y, int x, int z, List<Vector3Int> deletedPositions)
    {
        if (blockState[y, x, z] == BLOCK_EXIST)
        {
            blockState[y, x, z] = BLOCK_DELETED;
            deletedPositions.Add(new Vector3Int(x, y, z));
        }
    }

    // 高さマップを再計算.
    private void RecalculateHeightMap()
    {
        for (int x = 0; x < gridXZSize; x++)
        {
            for (int z = 0; z < gridXZSize; z++)
            {
                heightMap[x, z] = 0;
                for (int y = gridYSize - 1; y >= 0; y--)
                {
                    if (blockState[y, x, z] == BLOCK_EXIST)
                    {
                        heightMap[x, z] = y + 1;
                        break;
                    }
                }
            }
        }
    }

    // 全ブロックの位置を再確認し、blockState/blockObjects/activeBlocksを同期.
    public void RevalidateAllBlocks()
    {
        // 現在のactiveBlocksをコピーして処理.
        List<GameObject> blocksToCheck = new List<GameObject>(activeBlocks);
        int reregistered = 0;
        int removed = 0;

        foreach (GameObject block in blocksToCheck)
        {
            if (block == null)
            {
                activeBlocks.Remove(block);
                removed++;
                continue;
            }

            Vector3Int gridIndex = WorldToGridIndex(block.transform.position);

            // グリッド外のブロックは削除.
            if (!IsValidGridIndex(gridIndex))
            {
                activeBlocks.Remove(block);
                ReturnBlockToPool(block);
                removed++;
                continue;
            }

            // blockObjects に登録されていない場合は再登録.
            if (blockObjects[gridIndex.y, gridIndex.x, gridIndex.z] != block)
            {
                // 既に別のブロックがある場合はスキップ.
                if (blockObjects[gridIndex.y, gridIndex.x, gridIndex.z] != null)
                {
                    Debug.LogWarning($"RevalidateAllBlocks: 位置競合 ({gridIndex.x}, {gridIndex.y}, {gridIndex.z})");
                    continue;
                }

                blockState[gridIndex.y, gridIndex.x, gridIndex.z] = BLOCK_EXIST;
                blockObjects[gridIndex.y, gridIndex.x, gridIndex.z] = block;
                reregistered++;
            }
        }

        // 高さマップを再計算.
        RecalculateHeightMap();

        if (reregistered > 0 || removed > 0)
        {
            Debug.Log($"RevalidateAllBlocks: 再登録={reregistered}, 削除={removed}, 総数={activeBlocks.Count}");
        }
    }

    // activeBlocksのリストを取得（読み取り専用）.
    public IReadOnlyList<GameObject> GetActiveBlocks()
    {
        return activeBlocks.AsReadOnly();
    }
}
