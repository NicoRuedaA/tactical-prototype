using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

/// <summary>
/// Renders the procedural map as a scrollable grid of node buttons with
/// connection lines between them. Follows the RewardScreen pattern:
/// pre-placed UI references, singleton callback to RunManager.
///
/// Rebuilds on every OnEnable() so returning from combat/rest/reward
/// refreshes the map state.
/// </summary>
public class MapView : MonoBehaviour
{
    [Header("UI References")]
    public Button NodeButtonPrefab;
    public Transform ContentContainer;
    public LineRenderer LineRendererPrefab;

    [Header("Layout")]
    public float RowSpacing = 120f;
    public float ColSpacing = 100f;
    public float RowOffsetX = 0f; // slight horizontal offset per row for visual interest

    [Header("Node Colors")]
    public Color CombatColor = new Color(0.3f, 0.5f, 1.0f);     // blue
    public Color EliteColor = new Color(1.0f, 0.6f, 0.1f);      // orange
    public Color BossColor = new Color(1.0f, 0.2f, 0.2f);       // red
    public Color RestColor = new Color(0.2f, 0.8f, 0.2f);       // green
    public Color ShopColor = new Color(1.0f, 0.9f, 0.1f);       // yellow
    public Color DisabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f); // greyed out

    [Header("Line Renderer")]
    public float LineWidth = 2f;
    public Color LineColor = Color.white;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private readonly List<GameObject> _spawnedButtons = new List<GameObject>();
    private readonly List<LineRenderer> _spawnedLines = new List<LineRenderer>();
    private MapGraph _currentGraph;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDisable()
    {
        ClearSpawned();
    }

    /// <summary>
    /// Rebuilds the entire map UI: clears previous, reads graph from
    /// RunManager, spawns buttons and connection lines.
    /// </summary>
    public void Rebuild()
    {
        ClearSpawned();

        var mgr = RunManager.Instance;
        if (mgr == null || mgr.CurrentRun == null)
        {
            Debug.LogWarning("MapView: No active RunState found.");
            return;
        }

        _currentGraph = mgr.CurrentRun.Graph;
        if (_currentGraph == null)
        {
            Debug.LogWarning("MapView: CurrentRun has no MapGraph.");
            return;
        }

        var nodeButtons = new Dictionary<string, RectTransform>();
        var availableNodes = mgr.CurrentRun.GetAvailableNodes();

        // Spawn button for each node
        foreach (var kvp in _currentGraph.Nodes)
        {
            var node = kvp.Value;
            var button = CreateNodeButton(node, availableNodes);
            nodeButtons[node.Id] = button;
        }

        // Draw connection lines between nodes
        DrawConnectionLines(nodeButtons);
    }

    // ── Button creation ───────────────────────────────────────────────────────

    private RectTransform CreateNodeButton(MapNode node, IReadOnlyList<string> availableNodes)
    {
        if (NodeButtonPrefab == null || ContentContainer == null)
            return null;

        var button = Instantiate(NodeButtonPrefab, ContentContainer);
        _spawnedButtons.Add(button.gameObject);

        // Position by Row/Col
        var rt = button.GetComponent<RectTransform>();
        if (rt != null)
        {
            float x = node.Col * ColSpacing + node.Row * RowOffsetX;
            float y = -node.Row * RowSpacing; // negative so first row is at top
            rt.anchoredPosition = new Vector2(x, y);
        }

        // Set node label text
        var text = button.GetComponentInChildren<Text>();
        if (text != null)
            text.text = GetNodeLabel(node);

        // Color by type
        var colors = button.colors;
        bool isAvailable = availableNodes.Contains(node.Id);
        if (isAvailable)
        {
            colors.normalColor = GetNodeColor(node.Type);
            colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, 0.3f);
        }
        else
        {
            colors.normalColor = DisabledColor;
            colors.highlightedColor = DisabledColor;
        }
        button.colors = colors;

        // Interactable only if available
        button.interactable = isAvailable;

        // Click handler
        string capturedId = node.Id;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnNodeButtonClicked(capturedId));

        return rt;
    }

    // ── Connection lines ──────────────────────────────────────────────────────

    private void DrawConnectionLines(Dictionary<string, RectTransform> nodeButtons)
    {
        if (LineRendererPrefab == null)
            return;

        foreach (var kvp in _currentGraph.Nodes)
        {
            var fromNode = kvp.Value;
            if (!nodeButtons.TryGetValue(fromNode.Id, out var fromRt))
                continue;

            if (fromNode.ConnectedNodeIds.Count == 0)
                continue;

            Vector3 fromWorld = GetButtonCenterWorld(fromRt);

            foreach (var toId in fromNode.ConnectedNodeIds)
            {
                if (!nodeButtons.TryGetValue(toId, out var toRt))
                    continue;

                Vector3 toWorld = GetButtonCenterWorld(toRt);

                // Draw a line from parent to child
                var line = Instantiate(LineRendererPrefab, ContentContainer);
                _spawnedLines.Add(line);

                line.positionCount = 2;
                line.SetPosition(0, fromWorld);
                line.SetPosition(1, toWorld);
                line.startWidth = LineWidth;
                line.endWidth = LineWidth;
                line.startColor = LineColor;
                line.endColor = LineColor;
                line.useWorldSpace = true;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Vector3 GetButtonCenterWorld(RectTransform rt)
    {
        // Get the center of the button in world space
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    private void OnNodeButtonClicked(string nodeId)
    {
        var mgr = RunManager.Instance;
        if (mgr != null)
            mgr.OnNodeSelected(nodeId);
    }

    private void ClearSpawned()
    {
        foreach (var go in _spawnedButtons)
        {
            if (go != null)
                Destroy(go);
        }
        _spawnedButtons.Clear();

        foreach (var line in _spawnedLines)
        {
            if (line != null)
                Destroy(line.gameObject);
        }
        _spawnedLines.Clear();

        _currentGraph = null;
    }

    // ── Static helpers (testable pure functions) ──────────────────────────────

    /// <summary>
    /// Returns the display color for a given MapNodeType.
    /// Pure function — no side effects, trivially testable.
    /// </summary>
    public static Color GetNodeColor(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Combat => new Color(0.3f, 0.5f, 1.0f),   // blue
            MapNodeType.Elite  => new Color(1.0f, 0.6f, 0.1f),   // orange
            MapNodeType.Boss   => new Color(1.0f, 0.2f, 0.2f),   // red
            MapNodeType.Rest   => new Color(0.2f, 0.8f, 0.2f),   // green
            MapNodeType.Shop   => new Color(1.0f, 0.9f, 0.1f),   // yellow
            _                   => Color.gray,
        };
    }

    /// <summary>
    /// Returns a short label for a map node, combining type symbol and row/col.
    /// </summary>
    private static string GetNodeLabel(MapNode node)
    {
        string symbol = node.Type switch
        {
            MapNodeType.Combat => "\u2694",   // crossed swords
            MapNodeType.Elite  => "\u2726",   // star
            MapNodeType.Boss   => "\u2620",   // skull
            MapNodeType.Rest   => "\u2665",   // heart
            MapNodeType.Shop   => "\u2663",   // club
            _                   => "?",
        };
        return $"{symbol}";
    }
}
