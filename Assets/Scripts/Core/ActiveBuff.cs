namespace Game.Core
{
    /// <summary>
    /// Tracks a single active buff or debuff on a Piece.
    /// Aura buffs (WhileInArea) are re-evaluated every turn start and carry
    /// RemainingTurns = -1 as a sentinel.
    /// </summary>
    public sealed class ActiveBuff
    {
        public IAbilityData Source     { get; }
        public Piece        SourcePiece { get; }
        public int          RemainingTurns { get; set; }

        public bool IsAura => Source.DurationType == DurationType.WhileInArea;

        public ActiveBuff(IAbilityData source, Piece sourcePiece, int remainingTurns)
        {
            Source         = source;
            SourcePiece    = sourcePiece;
            RemainingTurns = remainingTurns;
        }
    }
}
