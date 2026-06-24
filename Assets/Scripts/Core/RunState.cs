using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Tracks the player's roster of pieces across an entire run (multiple sequential
    /// combats). HP persists because the same Piece object references are reused.
    /// Pure C# — no Unity dependencies, fully unit-testable.
    /// </summary>
    public sealed class RunState
    {
        private readonly List<Piece> _pieces;

        /// <summary>The player's pieces — same references survive across combats.</summary>
        public IReadOnlyList<Piece> Pieces => _pieces;

        /// <summary>Current combat in the sequence (0-based). Starts at 0.</summary>
        public int CombatIndex { get; private set; }

        /// <summary>Total combats in this run.</summary>
        public int TotalCombats { get; }

        /// <param name="playerPieces">Initial player pieces. Must be non-null and non-empty.</param>
        /// <param name="totalCombats">Number of combats in this run (default 3).</param>
        /// <exception cref="ArgumentNullException">Thrown when playerPieces is null.</exception>
        /// <exception cref="ArgumentException">Thrown when playerPieces is empty.</exception>
        public RunState(IEnumerable<Piece> playerPieces, int totalCombats = 3)
        {
            _pieces = playerPieces?.ToList()
                      ?? throw new ArgumentNullException(nameof(playerPieces));

            if (_pieces.Count == 0)
                throw new ArgumentException("Player pieces cannot be empty.", nameof(playerPieces));

            TotalCombats = totalCombats > 0
                ? totalCombats
                : throw new ArgumentException("TotalCombats must be greater than 0.", nameof(totalCombats));

            CombatIndex = 0;
        }

        /// <summary>
        /// Advance to the next combat.
        /// </summary>
        /// <returns>True if more combats remain; false if the run is complete.</returns>
        public bool AdvanceCombat()
        {
            CombatIndex++;
            return CombatIndex < TotalCombats;
        }

        /// <summary>True when all player pieces are dead.</summary>
        public bool IsPlayerDead => _pieces.All(p => p.IsDead);

        /// <summary>Returns pieces with Hp > 0 (alive and can fight).</summary>
        public IEnumerable<Piece> GetAlivePlayerPieces() =>
            _pieces.Where(p => !p.IsDead);

        /// <summary>
        /// Adds an ability to the specified piece. Null abilities are silently ignored.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when pieceId is not found.</exception>
        public void AddAbility(string pieceId, IAbilityData ability)
        {
            if (ability == null) return;
            var piece = _pieces.FirstOrDefault(p => p.Id == pieceId);
            if (piece == null)
                throw new ArgumentException($"Piece '{pieceId}' not found in run state.", nameof(pieceId));
            piece.AddAbility(ability);
        }

        /// <summary>
        /// Applies a flat stat boost to the specified piece.
        /// StatType.MaxHp is NOT in the StatType enum — use ApplyMaxHpBoost for HP.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when pieceId is not found.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown stat types.</exception>
        public void ApplyStatBoost(string pieceId, StatType stat, int amount)
        {
            var piece = _pieces.FirstOrDefault(p => p.Id == pieceId);
            if (piece == null)
                throw new ArgumentException($"Piece '{pieceId}' not found in run state.", nameof(pieceId));

            switch (stat)
            {
                case StatType.Damage:
                    piece.AddBonusDamage(amount);
                    break;
                case StatType.AttackRange:
                    piece.AddBonusAttackRange(amount);
                    break;
                case StatType.MoveRange:
                    piece.AddBonusMoveRange(amount);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, $"Unknown stat type: {stat}");
            }
        }

        /// <summary>
        /// Increases a piece's max HP and heals by the same amount.
        /// Uses Piece.AddBonusMaxHp which does direct Hp manipulation.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when pieceId is not found.</exception>
        public void ApplyMaxHpBoost(string pieceId, int amount)
        {
            var piece = _pieces.FirstOrDefault(p => p.Id == pieceId);
            if (piece == null)
                throw new ArgumentException($"Piece '{pieceId}' not found in run state.", nameof(pieceId));
            piece.AddBonusMaxHp(amount);
        }

        /// <summary>Updates a piece's board coordinates for combat placement.</summary>
        /// <exception cref="ArgumentException">Thrown when pieceId is not found.</exception>
        public void PlacePiece(string pieceId, Axial coords)
        {
            var piece = _pieces.FirstOrDefault(p => p.Id == pieceId);
            if (piece == null)
                throw new ArgumentException($"Piece '{pieceId}' not found in run state.", nameof(pieceId));
            piece.Coords = coords;
        }
    }
}
