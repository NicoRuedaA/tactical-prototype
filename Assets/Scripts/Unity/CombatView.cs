using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

public sealed class CombatFeedbackRecord
{
    public CombatFeedbackRecord(
        Piece piece,
        CombatFeedbackKind kind,
        int amount,
        string label,
        bool isPassive = false)
    {
        Piece = piece;
        Kind = kind;
        Amount = amount;
        Label = label ?? string.Empty;
        IsPassive = isPassive;
    }

    public Piece Piece { get; }
    public CombatFeedbackKind Kind { get; }
    public int Amount { get; }
    public string Label { get; }
    public bool IsPassive { get; }
}

public class CombatView : MonoBehaviour
{
    [Header("References")]
    public CombatRunner Runner;
    public Transform BoardRoot;
    public Transform PiecesRoot;

    [Header("Prefabs")]
    public GameObject TilePrefab;
    public GameObject PiecePrefab;
    public Material QueenMaterial;
    public Color QueenColor = new Color(1f, 0.72f, 0.12f, 1f);

    [Header("Tile Materials")]
    public Material TileNormal;
    public Material TileReachable;
    public Material TileAttackable;
    public Material TileSelected;
    public Material TileAbilityRange;

    [Header("Piece Materials")]
    public Material PiecePlayerMat;
    public Material PieceEnemyMat;

    [Header("Replaceable Piece Indicators")]
    public GameObject PieceHighlightPrefab;
    public Material PieceSelectedHighlight;
    public Material PieceAttackHighlight;
    public Material PieceAbilityHighlight;

    [Header("Floating Feedback")]
    public GameObject FloatingTextPrefab;
    public Transform FeedbackRoot;
    [Min(0f)] public float FloatingTextDuration = 0.7f;
    public bool CompleteAnimationsImmediately;

    [Tooltip("Raises piece visuals above the board surface. Piece.prefab uses a centered 2-unit body.")]
    [Min(0f)] public float PieceVerticalOffset = 1f;

    private CombatEngine _engine;
    private readonly Dictionary<Axial, TileView> _tileViews = new();
    private readonly Dictionary<Piece, PieceView> _pieceViews = new();
    private readonly Dictionary<Piece, CharacterData> _pieceDefinitions = new();
    private readonly HashSet<Piece> _dyingPieces = new();
    private readonly Queue<CombatFeedbackPopup> _popupPool = new();
    private readonly HashSet<CombatFeedbackPopup> _activePopups = new();
    private int _popupSequence;
    private bool _isShuttingDown;

    public event Action<CombatFeedbackRecord> FeedbackPresented;

    public int ActivePopupCount => _activePopups.Count;
    public int PooledPopupCount => _popupPool.Count;
    public bool HasActiveFeedback
    {
        get
        {
            if (_activePopups.Count > 0)
                return true;
            foreach (PieceView view in _pieceViews.Values)
            {
                if (view != null && view.HasActiveFeedback)
                    return true;
            }
            return false;
        }
    }

    public void OnEngineReady(CombatEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        Subscribe();
        BuildBoard();
        BuildPieces();
    }

    public void SetPieceDefinition(Piece piece, CharacterData definition)
    {
        if (piece == null)
            return;
        if (definition == null)
            _pieceDefinitions.Remove(piece);
        else
            _pieceDefinitions[piece] = definition;
    }

    private void OnDestroy()
    {
        _isShuttingDown = true;
        if (_engine != null)
        {
            _engine.PieceMoved -= OnPieceMoved;
            _engine.AttackResolved -= OnAttackResolved;
            _engine.PieceDied -= OnPieceDied;
            _engine.TurnChanged -= OnTurnChanged;
            _engine.AbilityResolved -= PresentAbilityResolution;
        }

        foreach (CombatFeedbackPopup popup in new List<CombatFeedbackPopup>(_activePopups))
            DestroyPopup(popup);
        _activePopups.Clear();

        while (_popupPool.Count > 0)
            DestroyPopup(_popupPool.Dequeue());
    }

    private void Subscribe()
    {
        _engine.PieceMoved += OnPieceMoved;
        _engine.AttackResolved += OnAttackResolved;
        _engine.PieceDied += OnPieceDied;
        _engine.TurnChanged += OnTurnChanged;
        _engine.AbilityResolved += PresentAbilityResolution;
    }

    private void BuildBoard()
    {
        if (TilePrefab == null || BoardRoot == null)
            return;

        foreach (Tile tile in _engine.Board.Tiles)
        {
            Axial coord = tile.Coords;
            GameObject instance = Instantiate(
                TilePrefab,
                HexLayout.AxialToWorld(coord),
                Quaternion.identity,
                BoardRoot);
            instance.name = $"Tile_{coord}";

            TileView view = instance.GetComponent<TileView>();
            if (view != null)
            {
                view.Coord = coord;
                view.AssignMaterials(
                    TileNormal,
                    TileReachable,
                    TileAttackable,
                    TileSelected,
                    TileAbilityRange);
                view.SetHighlight(TileHighlight.Normal);
            }
            _tileViews[coord] = view;
        }
    }

    private void BuildPieces()
    {
        if (PiecesRoot == null)
            return;

        foreach (Piece piece in _engine.Pieces)
        {
            if (piece.IsDead)
                continue;

            _pieceDefinitions.TryGetValue(piece, out CharacterData definition);
            GameObject prefab = definition != null && definition.modelPrefab != null
                ? definition.modelPrefab
                : PiecePrefab;
            if (prefab == null)
                continue;

            GameObject instance = Instantiate(
                prefab,
                PieceWorldPosition(piece.Coords),
                Quaternion.identity,
                PiecesRoot);
            instance.name = piece.Name;

            PieceView view = instance.GetComponent<PieceView>();
            if (view != null)
            {
                view.Piece = piece;
                view.SetCompleteAnimationsImmediately(CompleteAnimationsImmediately);
                Material material = definition != null && definition.modelMaterial != null
                    ? definition.modelMaterial
                    : piece.Team == Team.Player ? PiecePlayerMat : PieceEnemyMat;
                if (piece.IsQueen && QueenMaterial != null)
                    material = QueenMaterial;
                Color? tint = piece.IsQueen
                    ? QueenColor
                    : definition != null && definition.useModelTint
                        ? definition.modelTint
                        : (Color?)null;
                view.AssignMaterial(material, tint);
                view.ConfigureHighlight(
                    PieceHighlightPrefab,
                    PieceSelectedHighlight != null ? PieceSelectedHighlight : TileSelected,
                    PieceAttackHighlight != null ? PieceAttackHighlight : TileAttackable,
                    PieceAbilityHighlight != null ? PieceAbilityHighlight : TileAbilityRange);
                view.RefreshVitals();
            }
            _pieceViews[piece] = view;
        }
    }

    public TileView GetTileView(Axial coord)
    {
        _tileViews.TryGetValue(coord, out TileView view);
        return view;
    }

    public PieceView GetPieceView(Piece piece)
    {
        _pieceViews.TryGetValue(piece, out PieceView view);
        return view;
    }

    public void SetCompleteAnimationsImmediately(bool completeImmediately)
    {
        CompleteAnimationsImmediately = completeImmediately;
        foreach (PieceView view in new List<PieceView>(_pieceViews.Values))
        {
            if (view != null)
                view.SetCompleteAnimationsImmediately(completeImmediately);
        }
        if (!completeImmediately)
            return;
        CompleteActiveFeedbackImmediately();
    }

    public void CompleteActiveFeedbackImmediately()
    {
        foreach (PieceView view in new List<PieceView>(_pieceViews.Values))
        {
            if (view != null)
                view.CompleteAllFeedbackImmediately();
        }
        foreach (CombatFeedbackPopup popup in new List<CombatFeedbackPopup>(_activePopups))
        {
            if (popup != null)
                popup.CompleteImmediately();
        }
    }

    public void PresentAbilityResolution(AbilityResolution resolution)
    {
        if (resolution == null)
            return;

        if (resolution.Source != null && _pieceViews.TryGetValue(resolution.Source, out PieceView casterView))
        {
            casterView.RefreshVitals();
            int manaCost = resolution.Ability != null ? resolution.Ability.ManaCost : 0;
            if (!resolution.IsPassive && manaCost > 0)
                PresentChange(resolution.Source, casterView, CombatFeedbackKind.Mana, -manaCost, $"Mana -{manaCost}");
        }

        foreach (AbilityEffectChange change in resolution.Changes)
        {
            if (change?.Target == null || !_pieceViews.TryGetValue(change.Target, out PieceView targetView))
                continue;

            targetView.RefreshVitals();
            if (change.HpDelta < 0)
                PresentChange(change.Target, targetView, CombatFeedbackKind.Damage, -change.HpDelta, $"-{Math.Abs(change.HpDelta)}", resolution.IsPassive);
            else if (change.HpDelta > 0)
                PresentChange(change.Target, targetView, CombatFeedbackKind.Heal, change.HpDelta, $"+{change.HpDelta}", resolution.IsPassive);

            if (change.ManaDelta != 0)
            {
                string sign = change.ManaDelta > 0 ? "+" : string.Empty;
                PresentChange(change.Target, targetView, CombatFeedbackKind.Mana, change.ManaDelta, $"Mana {sign}{change.ManaDelta}", resolution.IsPassive);
            }

            if (resolution.Ability != null
                && (resolution.Ability.EffectType == EffectType.Buff
                    || resolution.Ability.EffectType == EffectType.Debuff)
                && change.BuffDelta != 0)
            {
                int amount = Math.Abs(change.BuffDelta);
                bool debuff = resolution.Ability.EffectType == EffectType.Debuff;
                string label = debuff ? "Debuff" : "Buff";
                PresentChange(change.Target, targetView,
                    debuff ? CombatFeedbackKind.Debuff : CombatFeedbackKind.Buff,
                    amount, label, resolution.IsPassive);
            }
        }
    }

    public void SetHighlightForCoord(Axial coord, TileHighlight state)
    {
        if (_tileViews.TryGetValue(coord, out TileView view) && view != null)
            view.SetHighlight(state);
    }

    public void SetHighlightForPiece(Piece piece, PieceHighlight state)
    {
        if (piece != null && _pieceViews.TryGetValue(piece, out PieceView view) && view != null)
            view.SetHighlight(state);
    }

    public void ClearHighlights()
    {
        foreach (TileView view in _tileViews.Values)
        {
            if (view != null)
                view.SetHighlight(TileHighlight.Normal);
        }
        foreach (PieceView view in _pieceViews.Values)
        {
            if (view != null && !view.IsDying)
                view.SetHighlight(PieceHighlight.Normal);
        }
    }

    private void OnPieceMoved(Piece piece, Axial from, Axial to)
    {
        if (_pieceViews.TryGetValue(piece, out PieceView view) && view != null)
            view.OnMove(PieceWorldPosition(to));
    }

    private Vector3 PieceWorldPosition(Axial coord)
    {
        return HexLayout.AxialToWorld(coord) + Vector3.up * PieceVerticalOffset;
    }

    private void OnAttackResolved(AttackResolution resolution)
    {
        if (resolution?.Target == null || resolution.AppliedDamage <= 0)
            return;
        if (!_pieceViews.TryGetValue(resolution.Target, out PieceView view) || view == null)
            return;
        PresentChange(
            resolution.Target,
            view,
            CombatFeedbackKind.Damage,
            resolution.AppliedDamage,
            $"-{resolution.AppliedDamage}");
    }

    private void OnPieceDied(Piece piece)
    {
        if (piece == null || _dyingPieces.Contains(piece))
            return;
        if (!_pieceViews.TryGetValue(piece, out PieceView view) || view == null)
            return;

        _dyingPieces.Add(piece);
        view.OnDeath(() => CompletePieceRemoval(piece, view));
    }

    private void CompletePieceRemoval(Piece piece, PieceView expectedView)
    {
        if (!_dyingPieces.Remove(piece))
            return;
        if (_pieceViews.TryGetValue(piece, out PieceView currentView)
            && currentView == expectedView)
            _pieceViews.Remove(piece);
    }

    private void OnTurnChanged(Piece current)
    {
        ClearHighlights();
    }

    private void PresentChange(
        Piece piece,
        PieceView view,
        CombatFeedbackKind kind,
        int amount,
        string label,
        bool isPassive = false)
    {
        switch (kind)
        {
            case CombatFeedbackKind.Damage:
                view.OnDamage(Math.Abs(amount));
                break;
            case CombatFeedbackKind.Heal:
                view.OnHeal(Math.Abs(amount));
                break;
            case CombatFeedbackKind.Mana:
                view.OnManaChanged(amount);
                break;
            case CombatFeedbackKind.Buff:
                view.OnBuffChanged(amount);
                break;
            case CombatFeedbackKind.Debuff:
                view.OnDebuffChanged(amount);
                break;
        }

        FeedbackPresented?.Invoke(new CombatFeedbackRecord(piece, kind, amount, label, isPassive));
        ShowPopup(view.transform.position, label, kind);
    }

    private void ShowPopup(Vector3 piecePosition, string label, CombatFeedbackKind kind)
    {
        CombatFeedbackPopup popup = AcquirePopup();
        _activePopups.Add(popup);
        float stackOffset = (_popupSequence++ % 3) * 0.22f;
        popup.Show(
            piecePosition + Vector3.up * (1.05f + stackOffset),
            label,
            kind,
            FloatingTextDuration,
            CompleteAnimationsImmediately);
    }

    private CombatFeedbackPopup AcquirePopup()
    {
        CombatFeedbackPopup popup = _popupPool.Count > 0
            ? _popupPool.Dequeue()
            : CreatePopup();
        popup.gameObject.SetActive(true);
        return popup;
    }

    private CombatFeedbackPopup CreatePopup()
    {
        Transform parent = FeedbackRoot != null ? FeedbackRoot : PiecesRoot;
        GameObject instance;
        if (FloatingTextPrefab != null)
            instance = Instantiate(FloatingTextPrefab, parent);
        else
        {
            instance = new GameObject("Combat Feedback Popup");
            instance.transform.SetParent(parent, false);
            TextMesh text = instance.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.055f;
            MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 100;
        }

        CombatFeedbackPopup popup = instance.GetComponent<CombatFeedbackPopup>();
        if (popup == null)
            popup = instance.AddComponent<CombatFeedbackPopup>();
        popup.Initialize(ReleasePopup);
        return popup;
    }

    private void ReleasePopup(CombatFeedbackPopup popup)
    {
        if (popup == null || !_activePopups.Remove(popup))
            return;
        popup.gameObject.SetActive(false);
        if (!_isShuttingDown)
            _popupPool.Enqueue(popup);
    }

    private static void DestroyPopup(CombatFeedbackPopup popup)
    {
        if (popup == null)
            return;
        popup.Shutdown();
        if (Application.isPlaying)
            Destroy(popup.gameObject);
        else
            DestroyImmediate(popup.gameObject);
    }
}

internal sealed class CombatFeedbackPopup : MonoBehaviour
{
    private TextMesh _text;
    private Action<CombatFeedbackPopup> _release;
    private Coroutine _routine;
    private Color _baseColor;

    public void Initialize(Action<CombatFeedbackPopup> release)
    {
        _release = release;
        _text = GetComponentInChildren<TextMesh>(true);
        if (_text == null)
            _text = gameObject.AddComponent<TextMesh>();
    }

    public void Show(
        Vector3 position,
        string label,
        CombatFeedbackKind kind,
        float duration,
        bool completeImmediately)
    {
        if (_routine != null)
            StopCoroutine(_routine);
        transform.position = position;
        _text.text = label ?? string.Empty;
        _baseColor = GetColor(kind);
        _text.color = _baseColor;
        if (completeImmediately || duration <= 0f)
        {
            CompleteImmediately();
            return;
        }
        _routine = StartCoroutine(Animate(duration));
    }

    public void CompleteImmediately()
    {
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = null;
        _release?.Invoke(this);
    }

    public void Shutdown()
    {
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = null;
        _release = null;
    }

    private void LateUpdate()
    {
        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }

    private IEnumerator Animate(float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = start + Vector3.up * (0.45f * t);
            _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 1f - t);
            yield return null;
        }
        _routine = null;
        _release?.Invoke(this);
    }

    private static Color GetColor(CombatFeedbackKind kind)
    {
        return kind switch
        {
            CombatFeedbackKind.Damage => new Color(1f, 0.22f, 0.16f, 1f),
            CombatFeedbackKind.Heal => new Color(0.25f, 1f, 0.4f, 1f),
            CombatFeedbackKind.Mana => new Color(0.25f, 0.75f, 1f, 1f),
            CombatFeedbackKind.Debuff => new Color(0.9f, 0.25f, 0.65f, 1f),
            _ => new Color(1f, 0.82f, 0.2f, 1f),
        };
    }
}
