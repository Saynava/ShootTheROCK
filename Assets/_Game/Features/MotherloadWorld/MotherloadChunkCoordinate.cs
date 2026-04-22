using System;
using UnityEngine;

[Serializable]
public struct MotherloadChunkCoordinate : IEquatable<MotherloadChunkCoordinate>
{
    [SerializeField] private int column;
    [SerializeField] private int row;

    public int Column => column;
    public int Row => row;

    public MotherloadChunkCoordinate(int column, int row)
    {
        this.column = column;
        this.row = row;
    }

    public bool Equals(MotherloadChunkCoordinate other)
    {
        return column == other.column && row == other.row;
    }

    public override bool Equals(object obj)
    {
        return obj is MotherloadChunkCoordinate other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (column * 397) ^ row;
        }
    }

    public override string ToString()
    {
        return "(" + column + ", " + row + ")";
    }
}
