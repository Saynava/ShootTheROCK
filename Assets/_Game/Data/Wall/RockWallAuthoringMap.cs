using UnityEngine;

[CreateAssetMenu(fileName = "RockWallAuthoringMap", menuName = "Shoot the ROCK/Wall Authoring Map")]
public class RockWallAuthoringMap : ScriptableObject
{
    public const byte EmptyMaterialId = 0;
    public const byte RockMaterialId = 1;
    public const byte CopperMaterialId = 2;
    public const byte SilverMaterialId = 3;
    public const byte GoldMaterialId = 4;
    public const byte BedrockMaterialId = 5;

    [SerializeField, HideInInspector] private int width;
    [SerializeField, HideInInspector] private int height;
    [SerializeField, HideInInspector] private byte[] materialIds;

    public int Width => width;
    public int Height => height;

    public bool IsCompatible(int expectedWidth, int expectedHeight)
    {
        return width == expectedWidth
            && height == expectedHeight
            && materialIds != null
            && materialIds.Length == expectedWidth * expectedHeight;
    }

    public void Resize(int targetWidth, int targetHeight, byte fillMaterialId)
    {
        width = Mathf.Max(1, targetWidth);
        height = Mathf.Max(1, targetHeight);
        materialIds = new byte[width * height];
        Fill(fillMaterialId);
    }

    public void Fill(byte materialId)
    {
        EnsureBuffer();
        for (int i = 0; i < materialIds.Length; i++)
            materialIds[i] = materialId;
    }

    public void CaptureFromSolidCells(bool[,] solidCells, int rowCount, int columnCount, byte solidMaterialId = RockMaterialId)
    {
        if (solidCells == null || rowCount <= 0 || columnCount <= 0)
        {
            Resize(Mathf.Max(1, columnCount), Mathf.Max(1, rowCount), EmptyMaterialId);
            return;
        }

        width = Mathf.Max(1, columnCount);
        height = Mathf.Max(1, rowCount);
        materialIds = new byte[width * height];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
                materialIds[GetIndex(row, column)] = solidCells[row, column] ? solidMaterialId : EmptyMaterialId;
        }
    }

    public byte GetMaterialId(int row, int column)
    {
        if (row < 0 || row >= height || column < 0 || column >= width || materialIds == null)
            return EmptyMaterialId;

        return materialIds[GetIndex(row, column)];
    }

    public void SetMaterialId(int row, int column, byte materialId)
    {
        if (row < 0 || row >= height || column < 0 || column >= width)
            return;

        EnsureBuffer();
        materialIds[GetIndex(row, column)] = materialId;
    }

    private int GetIndex(int row, int column)
    {
        return (row * width) + column;
    }

    private void EnsureBuffer()
    {
        int expectedLength = Mathf.Max(1, width) * Mathf.Max(1, height);
        if (materialIds != null && materialIds.Length == expectedLength)
            return;

        materialIds = new byte[expectedLength];
    }
}
