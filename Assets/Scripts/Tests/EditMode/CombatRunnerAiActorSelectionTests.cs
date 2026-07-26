using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatRunnerAiActorSelectionTests
{
    private GameObject _runnerObject;

    [TearDown]
    public void TearDown()
    {
        if (_runnerObject != null)
            Object.DestroyImmediate(_runnerObject);
    }

    [Test]
    public void ChooseAiActor_StartingFormation_SelectsEnemyWithReachableTileInsteadOfBlockedOuterCorner()
    {
        var board = Board.CreateRectangle(8, 8);
        var pieces = CreateStartingFormation();
        var engine = new CombatEngine(board, pieces);
        var outerCornerEnemy = pieces.Find(piece => piece.Id == "E_00");
        var runner = CreateRunner(engine);

        Piece chosen = InvokeChooseAiActor(runner, Team.Enemy);

        Assert.That(outerCornerEnemy, Is.Not.Null);
        Assert.That(engine.GetMoveRange(outerCornerEnemy).ReachableTiles, Is.Empty,
            "The outer corner enemy is boxed in by the starting formation.");
        Assert.That(chosen, Is.Not.Null);
        Assert.That(chosen, Is.Not.SameAs(outerCornerEnemy));
        Assert.That(engine.GetMoveRange(chosen).ReachableTiles, Is.Not.Empty,
            "The selected enemy must have at least one reachable tile.");

        engine.Begin();
        Piece playerActor = pieces.Find(piece => piece.Team == Team.Player);
        Assert.That(engine.SelectPiece(playerActor), Is.True);
        engine.Pass();
        Assert.That(engine.SelectPiece(chosen), Is.True);

        Axial before = chosen.Coords;
        DefaultEnemyAI.TakeTurn(engine);

        Assert.That(chosen.Coords, Is.Not.EqualTo(before),
            "The selected enemy AI actor must execute a movement action when no attack is available.");
    }

    private CombatRunner CreateRunner(CombatEngine engine)
    {
        _runnerObject = new GameObject("CombatRunner AI Actor Selection Test");
        var runner = _runnerObject.AddComponent<CombatRunner>();

        typeof(CombatRunner)
            .GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(runner, engine);

        return runner;
    }

    private static Piece InvokeChooseAiActor(CombatRunner runner, Team team)
    {
        var method = typeof(CombatRunner).GetMethod(
            "ChooseAiActor", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (Piece)method.Invoke(runner, new object[] { team });
    }

    private static List<Piece> CreateStartingFormation()
    {
        var pieces = new List<Piece>(32);
        for (int index = 0; index < 16; index++)
        {
            pieces.Add(CreatePiece(
                $"P_{index:00}", Team.Player, index == 0,
                new Axial(index % 8, index / 8)));
            pieces.Add(CreatePiece(
                $"E_{index:00}", Team.Enemy, index == 0,
                new Axial(7 - (index % 8), 7 - (index / 8))));
        }

        return pieces;
    }

    private static Piece CreatePiece(string id, Team team, bool isQueen, Axial coords)
    {
        return new Piece(id, team, 10, 1, 1, 1, 1, isQueen: isQueen)
        {
            Coords = coords,
        };
    }
}
