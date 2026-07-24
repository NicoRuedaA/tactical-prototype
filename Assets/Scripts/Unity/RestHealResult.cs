using System.Collections.Generic;

/// <summary>
/// HP change captured for one player piece when a Rest node resolves.
/// </summary>
public sealed class RestHealPieceResult
{
    public RestHealPieceResult(string pieceId, string pieceName, int beforeHp, int afterHp)
    {
        PieceId = pieceId;
        PieceName = pieceName;
        BeforeHp = beforeHp;
        AfterHp = afterHp;
    }

    public string PieceId { get; }
    public string PieceName { get; }
    public int BeforeHp { get; }
    public int AfterHp { get; }
    public int Delta => AfterHp - BeforeHp;
}

/// <summary>
/// The exact result of one Rest node resolution. The collection contains alive
/// pieces only and records the clamped before/after HP values.
/// </summary>
public sealed class RestHealResult
{
    public RestHealResult(int configuredPercent, IEnumerable<RestHealPieceResult> pieces)
    {
        ConfiguredPercent = configuredPercent;
        var snapshot = pieces == null
            ? new List<RestHealPieceResult>()
            : new List<RestHealPieceResult>(pieces);
        Pieces = snapshot.AsReadOnly();

        int totalDelta = 0;
        foreach (var piece in snapshot)
        {
            if (piece != null)
                totalDelta += piece.Delta;
        }
        TotalDelta = totalDelta;
    }

    public int ConfiguredPercent { get; }
    public IReadOnlyList<RestHealPieceResult> Pieces { get; }
    public int TotalDelta { get; }
}
