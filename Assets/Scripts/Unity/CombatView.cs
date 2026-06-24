using System.Collections.Generic;
using UnityEngine;
using Game.Core;

public class CombatView : MonoBehaviour
{
    [Header("References")]
    public CombatRunner Runner;
    public Transform BoardRoot;
    public Transform PiecesRoot;

    [Header("Prefabs")]
    public GameObject TilePrefab;
    public GameObject PiecePrefab;

    [Header("Tile Materials")]
    public Material TileNormal;
    public Material TileReachable;
    public Material TileAttackable;
    public Material TileSelected;
    public Material TileAbilityRange;

    [Header("Piece Materials")]
    public Material PiecePlayerMat;
    public Material PieceEnemyMat;

    private CombatEngine _engine;
    private readonly Dictionary<Axial, TileView> _tileViews = new();
    private readonly Dictionary<Piece, PieceView> _pieceViews = new();

    public void OnEngineReady(CombatEngine engine)
    {
        _engine = engine;
        Subscribe();
        BuildBoard();
        BuildPieces();
    }

    private void OnDestroy()
    {
        if (_engine != null)
        {
            _engine.PieceMoved -= OnPieceMoved;
            _engine.PieceAttacked -= OnPieceAttacked;
            _engine.PieceDied -= OnPieceDied;
            _engine.TurnChanged -= OnTurnChanged;
            _engine.CombatEnded -= OnCombatEnded;
            _engine.AbilityUsed -= OnAbilityUsed;
        }
    }

    private void Subscribe()
    {
        _engine.PieceMoved += OnPieceMoved;
        _engine.PieceAttacked += OnPieceAttacked;
        _engine.PieceDied += OnPieceDied;
        _engine.TurnChanged += OnTurnChanged;
        _engine.CombatEnded += OnCombatEnded;
        _engine.AbilityUsed += OnAbilityUsed;
    }

    private void BuildBoard()
    {
        if (TilePrefab == null || BoardRoot == null) return;

        foreach (Tile tile in _engine.Board.Tiles)
        {
            Axial coord = tile.Coords;
            Vector3 worldPos = HexLayout.AxialToWorld(coord);

            GameObject go = Instantiate(TilePrefab, worldPos, Quaternion.identity, BoardRoot);
            go.name = $"Tile_{coord}";

            TileView tv = go.GetComponent<TileView>();
            if (tv != null)
            {
                tv.Coord = coord;
                    tv.AssignMaterials(TileNormal, TileReachable, TileAttackable, TileSelected, TileAbilityRange);
                tv.SetHighlight(TileHighlight.Normal);
            }

            _tileViews[coord] = tv;
        }
    }

    private void BuildPieces()
    {
        if (PiecePrefab == null || PiecesRoot == null) return;

        foreach (Piece piece in _engine.Pieces)
        {
            if (piece.IsDead) continue;

            Vector3 worldPos = HexLayout.AxialToWorld(piece.Coords);
            GameObject go = Instantiate(PiecePrefab, worldPos, Quaternion.identity, PiecesRoot);
            go.name = piece.Name;

            PieceView pv = go.GetComponent<PieceView>();
            if (pv != null)
            {
                pv.Piece = piece;
                pv.AssignMaterial(piece.Team == Team.Player ? PiecePlayerMat : PieceEnemyMat);
            }

            _pieceViews[piece] = pv;
        }
    }

    public TileView GetTileView(Axial coord)
    {
        _tileViews.TryGetValue(coord, out var tv);
        return tv;
    }

    public PieceView GetPieceView(Piece piece)
    {
        _pieceViews.TryGetValue(piece, out var pv);
        return pv;
    }

    private void OnPieceMoved(Piece piece, Axial from, Axial to)
    {
        if (_pieceViews.TryGetValue(piece, out var pv))
            pv.OnMove(HexLayout.AxialToWorld(to));
    }

    private void OnPieceAttacked(Piece attacker, Piece target, int damage)
    {
        if (_pieceViews.TryGetValue(target, out var pv))
            pv.OnHit();
    }

    private void OnPieceDied(Piece piece)
    {
        if (_pieceViews.TryGetValue(piece, out var pv))
        {
            _pieceViews.Remove(piece);
            pv.OnDeath();
        }
    }

    private void OnTurnChanged(Piece current)
    {
        ClearHighlights();
    }

    private void OnAbilityUsed(Piece caster, IAbilityData ability, System.Collections.Generic.IReadOnlyList<Piece> targets)
    {
        // Refresh mana bar on caster
        if (_pieceViews.TryGetValue(caster, out var pv))
            pv.RefreshMana();

        // Refresh HP bars on all targets (heal/damage)
        foreach (var t in targets)
        {
            if (_pieceViews.TryGetValue(t, out var tv))
                tv.OnHit();
        }
    }

    private void OnCombatEnded(Team winner)
    {
        Debug.Log($"<color=lime>Combat over — {winner} wins</color>");
        ShowBanner(winner);
    }

    public void TestShowBanner()
    {
        ShowBanner(Team.Player);
    }

    private void ShowBanner(Team winner)
    {
        // Canvas
        var canvasGO = new GameObject("BannerCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();

        // Dark overlay
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(canvasGO.transform, false);
        var overlayImg = overlay.AddComponent<UnityEngine.UI.Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.6f);
        var overlayRT = overlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.sizeDelta = Vector2.zero;

        // Title text
        var title = new GameObject("Title");
        title.transform.SetParent(canvasGO.transform, false);
        var titleText = title.AddComponent<UnityEngine.UI.Text>();
        bool playerWins = winner == Team.Player;
        titleText.text = playerWins ? "VICTORY" : "DEFEAT";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 72;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = playerWins ? new Color(1f, 0.84f, 0f) : new Color(1f, 0.2f, 0.2f);
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.5f);
        titleRT.anchorMax = new Vector2(1, 0.5f);
        titleRT.pivot = new Vector2(0.5f, 0.5f);
        titleRT.sizeDelta = new Vector2(0, 120);
        titleRT.anchoredPosition = Vector2.zero;

        // Subtitle
        var sub = new GameObject("Subtitle");
        sub.transform.SetParent(canvasGO.transform, false);
        var subText = sub.AddComponent<UnityEngine.UI.Text>();
        subText.text = playerWins ? "Player team wins!" : "Enemy team wins...";
        subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subText.fontSize = 32;
        subText.alignment = TextAnchor.MiddleCenter;
        subText.color = Color.white;
        var subRT = sub.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0, 0.5f);
        subRT.anchorMax = new Vector2(1, 0.5f);
        subRT.pivot = new Vector2(0.5f, 0.5f);
        subRT.sizeDelta = new Vector2(0, 60);
        subRT.anchoredPosition = new Vector2(0, -80);

        // NOTE: Intentionally NOT calling DontDestroyOnLoad — the banner is
        // per-combat and must be destroyed when the scene unloads.
    }

    public void SetHighlightForCoord(Axial coord, TileHighlight state)
    {
        if (_tileViews.TryGetValue(coord, out var tv))
            tv.SetHighlight(state);
    }

    public void ClearHighlights()
    {
        foreach (var tv in _tileViews.Values)
            tv.SetHighlight(TileHighlight.Normal);
    }
}
