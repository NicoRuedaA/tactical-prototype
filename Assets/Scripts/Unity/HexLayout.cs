using UnityEngine;
using Game.Core;

/// <summary>
/// Maps axial (q, r) coordinates to a square grid in world space.
/// q → X axis (columns), r → Z axis (rows).
/// </summary>
public static class HexLayout
{
    /// <summary>Distance between tile centers.</summary>
    public static float Size { get; set; } = 1f;

    public static Vector3 AxialToWorld(Axial qr)
    {
        return new Vector3(qr.Q * Size, 0f, qr.R * Size);
    }

    public static Axial WorldToAxial(Vector3 pos)
    {
        int q = Mathf.RoundToInt(pos.x / Size);
        int r = Mathf.RoundToInt(pos.z / Size);
        return new Axial(q, r);
    }
}
