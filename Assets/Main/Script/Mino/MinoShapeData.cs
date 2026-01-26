using UnityEngine;

// ミノの形状データを管理するクラス.
public class MinoShapeData
{
    // ミノの3D形状定義 [y][x][z] 1=ブロックあり, 0=ブロックなし.
    private int[,,] shape;

    // 形状データのプロパティ.
    public int[,,] Shape
    {
        get => shape;
        set => shape = value;
    }

    // ランダム生成Shape用の定数.
    private const int RANDOM_SIZE_MIN = 4;
    private const int RANDOM_SIZE_MAX = 6;
    private const int RANDOM_SHAPE_COUNT = 24;

    // 定義済み形状の種類数（基本5種 + ランダム24種 = 29種）.
    public static int ShapeCount => 5 + RANDOM_SHAPE_COUNT;

    // I型 - 縦に3ブロック.
    private static readonly int[,,] ShapeI = new int[,,]
    {
        { {0, 0, 0}, {0, 1, 0}, {0, 0, 0} },
        { {0, 0, 0}, {0, 1, 0}, {0, 0, 0} },
        { {0, 0, 0}, {0, 1, 0}, {0, 0, 0} }
    };

    // L型 - L字形状.
    private static readonly int[,,] ShapeL = new int[,,]
    {
        { {0, 0, 0}, {0, 1, 0}, {0, 0, 0} },
        { {0, 0, 0}, {0, 1, 0}, {0, 0, 0} },
        { {0, 0, 0}, {0, 1, 1}, {0, 0, 0} }
    };

    // T型 - T字形状.
    private static readonly int[,,] ShapeT = new int[,,]
    {
        { {0, 0, 0}, {0, 0, 0}, {0, 0, 0} },
        { {0, 1, 0}, {1, 1, 1}, {0, 0, 0} },
        { {0, 0, 0}, {0, 0, 0}, {0, 0, 0} }
    };

    // O型 - 2x2x2キューブ.
    private static readonly int[,,] ShapeO = new int[,,]
    {
        { {0, 0, 0}, {0, 1, 1}, {0, 1, 1} },
        { {0, 0, 0}, {0, 1, 1}, {0, 1, 1} },
        { {0, 0, 0}, {0, 0, 0}, {0, 0, 0} }
    };

    // S型 - 3D階段形状.
    private static readonly int[,,] ShapeS = new int[,,]
    {
        { {0, 0, 0}, {1, 0, 0}, {0, 0, 0} },
        { {0, 0, 0}, {1, 1, 0}, {0, 0, 0} },
        { {0, 0, 0}, {0, 1, 0}, {0, 0, 0} }
    };

    private static readonly int[,,] ShapeAA = new int[,,]
    {
        { {0, 0, 1}, {1, 0, 0}, {0, 0, 1} },
        { {0, 1, 1}, {1, 1, 0}, {0, 1, 1} },
        { {0, 1, 0}, {0, 1, 0}, {0, 1, 0} }
    };

    private static readonly int[,,] ShapeAB = new int[,,]
    {
        { {1, 0, 0}, {0, 0, 1}, {0, 0, 0}, {0, 1, 0} },
        { {0, 1, 0}, {0, 0, 0}, {0, 1, 0}, {0, 0, 0} },
        { {0, 0, 0}, {0, 1, 0}, {0, 0, 0}, {0, 1, 0} },
        { {0, 1, 0}, {0, 0, 0}, {1, 0, 0}, {0, 0, 1} }
    };

    //(既)修:4*4 ~ 6*6 のAC ~AZまでのrandom配置の Shapeを追加.

    // ランダム配置Shapeを生成（AC〜AZ用）.
    private static int[,,] GenerateRandomShape(int seed)
    {
        // シードに基づいてサイズを決定（4〜6）.
        System.Random rng = new System.Random(seed);
        int size = rng.Next(RANDOM_SIZE_MIN, RANDOM_SIZE_MAX + 1);

        int[,,] shape = new int[size, size, size];

        // ブロック数を決定（サイズに応じて3〜8個）.
        int blockCount = rng.Next(3, Mathf.Min(9, size * 2));

        // 中心から開始して連結したブロックを配置.
        int centerY = size / 2;
        int centerX = size / 2;
        int centerZ = size / 2;

        shape[centerY, centerX, centerZ] = 1;
        int placed = 1;

        // 隣接方向（6方向）.
        int[,] directions = new int[,]
        {
            {1, 0, 0}, {-1, 0, 0},
            {0, 1, 0}, {0, -1, 0},
            {0, 0, 1}, {0, 0, -1}
        };

        // 連結を保ちながらランダムにブロックを配置.
        while (placed < blockCount)
        {
            // 既存ブロックの隣接位置を収集.
            System.Collections.Generic.List<Vector3Int> candidates = new System.Collections.Generic.List<Vector3Int>();
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        if (shape[y, x, z] == 1)
                        {
                            for (int d = 0; d < 6; d++)
                            {
                                int ny = y + directions[d, 0];
                                int nx = x + directions[d, 1];
                                int nz = z + directions[d, 2];

                                if (ny >= 0 && ny < size && nx >= 0 && nx < size && nz >= 0 && nz < size)
                                {
                                    if (shape[ny, nx, nz] == 0)
                                    {
                                        candidates.Add(new Vector3Int(nx, ny, nz));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (candidates.Count == 0) break;

            // ランダムに1つ選んで配置.
            int idx = rng.Next(candidates.Count);
            Vector3Int pos = candidates[idx];
            shape[pos.y, pos.x, pos.z] = 1;
            placed++;
        }

        return shape;
    }

    // AC〜AZのランダムShapeを取得（インデックス0〜23）.
    public static MinoShapeData GetRandomPatternShape(int index)
    {
        // 固定シードでランダム形状を生成（同じインデックスは同じ形状）.
        int seed = 1000 + index;
        return new MinoShapeData(GenerateRandomShape(seed));
    }

    // デフォルトコンストラクタ（単一ブロック）.
    public MinoShapeData()
    {
        shape = new int[,,]
        {
            { {0, 0, 0}, {0, 0, 0}, {0, 0, 0} },
            { {0, 0, 0}, {0, 1, 0}, {0, 0, 0} },
            { {0, 0, 0}, {0, 0, 0}, {0, 0, 0} }
        };
    }

    // 形状を指定するコンストラクタ.
    public MinoShapeData(int[,,] shapeData)
    {
        shape = shapeData;
    }

    // インデックスから定義済み形状を取得.
    public static MinoShapeData GetPredefinedShape(int index)
    {
        // index 5以上はランダム配置Shape（AC〜AZ）.
        if (index >= 5)
        {
            return GetRandomPatternShape(index - 5);
        }

        return index switch
        {
            0 => new MinoShapeData(ShapeI),
            1 => new MinoShapeData(ShapeL),
            2 => new MinoShapeData(ShapeT),
            3 => new MinoShapeData(ShapeO),
            4 => new MinoShapeData(ShapeS),
            _ => new MinoShapeData(ShapeI)
        };
    }

    // ランダムな定義済み形状を取得.
    public static MinoShapeData GetRandomShape()
    {
        int index = Random.Range(0, ShapeCount);
        return GetPredefinedShape(index);
    }

    // 形状のサイズを取得.
    public int GetSizeY() => shape.GetLength(0);
    public int GetSizeX() => shape.GetLength(1);
    public int GetSizeZ() => shape.GetLength(2);

    // 指定位置にブロックがあるか確認.
    public bool HasBlockAt(int y, int x, int z)
    {
        return shape[y, x, z] == 1;
    }
}
