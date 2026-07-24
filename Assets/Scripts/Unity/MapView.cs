using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

public enum MapNodeState
{
    Current,
    Available,
    Visited,
    Blocked
}

public enum MapConnectionState
{
    Available,
    Visited,
    Blocked
}

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

    [Header("State Colors")]
    public Color CurrentColor = new Color(1.0f, 0.85f, 0.25f);       // gold
    public Color VisitedColor = new Color(0.45f, 0.45f, 0.55f);      // muted slate

    [Header("Line Renderer")]
    public float LineWidth = 2f;
    public Color LineColor = Color.white;
    public Color AvailableLineColor = new Color(1.0f, 0.85f, 0.25f);
    public Color VisitedLineColor = new Color(0.55f, 0.65f, 0.85f);
    public Color BlockedLineColor = new Color(0.25f, 0.28f, 0.35f, 0.45f);
    public float BlockedLineWidth = 1f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private readonly List<GameObject> _spawnedButtons = new List<GameObject>();
    private readonly List<LineRenderer> _spawnedLines = new List<LineRenderer>();
    private MapGraph _currentGraph;
    private Text _statusText;
    private string _hoveredNodeId;
    private Material _runtimeLineMaterial;

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
        var nodeStates = BuildNodeStates(_currentGraph, availableNodes);

        // Spawn button for each node
        foreach (var kvp in _currentGraph.Nodes)
        {
            var node = kvp.Value;
            var button = CreateNodeButton(node, nodeStates[node.Id]);
            nodeButtons[node.Id] = button;
        }

        // Draw connection lines between nodes
        DrawConnectionLines(nodeButtons, nodeStates);
        SetStatusText(GetDefaultStatus(mgr.CurrentRun.Graph));
    }

    // ── Button creation ───────────────────────────────────────────────────────

    private RectTransform CreateNodeButton(MapNode node, MapNodeState state)
    {
        var content = EnsureContentContainer();
        if (content == null)
            return null;

        var button = NodeButtonPrefab != null
            ? Instantiate(NodeButtonPrefab, content)
            : CreateRuntimeNodeButton(content);

        if (button == null)
            return null;

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
        {
            text.text = GetNodeLabel(node);
            text.fontSize = 16;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 22;
        }

        // Color and interactivity by route state. Type colors remain visible for
        // available nodes; state colors make visited/current/blocked explicit.
        var stateColor = GetNodeStateColor(node, state);
        var colors = button.colors;
        colors.normalColor = stateColor;
        colors.highlightedColor = state == MapNodeState.Available
            ? Color.Lerp(stateColor, Color.white, 0.35f)
            : stateColor;
        colors.pressedColor = Color.Lerp(stateColor, Color.white, 0.5f);
        colors.selectedColor = colors.highlightedColor;
        // The target Image is kept white below, so the state tint is applied
        // exactly once by Button's ColorBlock (including disabled nodes).
        colors.disabledColor = stateColor;
        button.colors = colors;

        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = Color.white;

        // Only the next legal route choices are clickable.
        button.interactable = state == MapNodeState.Available;

        var nodeView = button.GetComponent<MapNodeView>() ?? button.gameObject.AddComponent<MapNodeView>();
        nodeView.Configure(this, node.Id);

        // Click handler
        string capturedId = node.Id;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnNodeButtonClicked(capturedId));

        return rt;
    }

    // ── Connection lines ──────────────────────────────────────────────────────

    private void DrawConnectionLines(
        Dictionary<string, RectTransform> nodeButtons,
        IReadOnlyDictionary<string, MapNodeState> nodeStates)
    {
        foreach (var kvp in _currentGraph.Nodes)
        {
            var fromNode = kvp.Value;
            if (!nodeButtons.TryGetValue(fromNode.Id, out var fromRt))
                continue;

            if (fromRt == null)
                continue;

            if (fromNode.ConnectedNodeIds.Count == 0)
                continue;

            Vector3 fromWorld = GetButtonCenterWorld(fromRt);

            foreach (var toId in fromNode.ConnectedNodeIds)
            {
                if (!nodeButtons.TryGetValue(toId, out var toRt))
                    continue;

                if (toRt == null)
                    continue;

                Vector3 toWorld = GetButtonCenterWorld(toRt);

                // Draw a line from parent to child
                var line = LineRendererPrefab != null
                    ? Instantiate(LineRendererPrefab, ContentContainer)
                    : CreateRuntimeLine(ContentContainer);
                _spawnedLines.Add(line);

                line.positionCount = 2;
                line.SetPosition(0, fromWorld);
                line.SetPosition(1, toWorld);
                var connectionState = GetConnectionState(fromNode, _currentGraph.Nodes[toId], nodeStates);
                var connectionColor = GetConnectionColor(connectionState);
                var connectionWidth = connectionState == MapConnectionState.Blocked
                    ? BlockedLineWidth
                    : LineWidth;
                line.startWidth = connectionWidth;
                line.endWidth = connectionWidth;
                line.startColor = connectionColor;
                line.endColor = connectionColor;
                line.useWorldSpace = true;
                line.sortingOrder = -1;
                line.transform.SetSiblingIndex(0);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Transform EnsureContentContainer()
    {
        if (ContentContainer != null)
            return ContentContainer;

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("Runtime Map Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
        }
        canvas.transform.localScale = Vector3.one;

        var contentGo = new GameObject("Runtime Map Content", typeof(RectTransform));
        contentGo.transform.SetParent(canvas.transform, false);

        var rt = contentGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-120f, 160f);
        rt.sizeDelta = new Vector2(900f, 520f);

        ContentContainer = contentGo.transform;
        _spawnedButtons.Add(contentGo);
        CreateMapLabels(ContentContainer);
        return ContentContainer;
    }

    private void CreateMapLabels(Transform parent)
    {
        var title = CreateRuntimeText(parent, "MAP ROUTE\nChoose your next encounter", 22, TextAnchor.UpperLeft);
        title.rectTransform.anchoredPosition = new Vector2(120f, -80f);
        title.rectTransform.sizeDelta = new Vector2(520f, 70f);

        var legend = CreateRuntimeText(parent,
            "Legend:  CURRENT  ◆   AVAILABLE  ●   VISITED  ✓   BLOCKED  ○",
            14, TextAnchor.UpperLeft);
        legend.rectTransform.anchoredPosition = new Vector2(120f, -125f);
        legend.rectTransform.sizeDelta = new Vector2(800f, 32f);

        _statusText = CreateRuntimeText(parent, string.Empty, 15, TextAnchor.UpperLeft);
        _statusText.rectTransform.anchoredPosition = new Vector2(120f, -280f);
        _statusText.rectTransform.sizeDelta = new Vector2(800f, 42f);
    }

    private static Text CreateRuntimeText(Transform parent, string value, int fontSize, TextAnchor alignment)
    {
        var textGo = new GameObject("Map Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(parent, false);
        var text = textGo.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private LineRenderer CreateRuntimeLine(Transform parent)
    {
        var lineGo = new GameObject("Runtime Map Connection", typeof(LineRenderer));
        lineGo.transform.SetParent(parent, false);
        var line = lineGo.GetComponent<LineRenderer>();
        line.material = GetRuntimeLineMaterial();
        line.numCapVertices = 4;
        line.alignment = LineAlignment.TransformZ;
        return line;
    }

    private Material GetRuntimeLineMaterial()
    {
        if (_runtimeLineMaterial != null)
            return _runtimeLineMaterial;

        var shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
        _runtimeLineMaterial = shader != null ? new Material(shader) : null;
        return _runtimeLineMaterial;
    }

    private static Button CreateRuntimeNodeButton(Transform parent)
    {
        var buttonGo = new GameObject("Runtime Map Node", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(parent, false);

        var rt = buttonGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(72f, 72f);

        var image = buttonGo.GetComponent<Image>();
        image.color = Color.white;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(buttonGo.transform, false);

        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var text = labelGo.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.fontSize = 32;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return buttonGo.GetComponent<Button>();
    }

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

    internal void OnNodePointerEntered(string nodeId)
    {
        _hoveredNodeId = nodeId;
        if (_currentGraph == null || !_currentGraph.Nodes.TryGetValue(nodeId, out var node))
            return;

        var state = GetNodeState(node, _currentGraph.LastVisitedNodeId,
            RunManager.Instance != null && RunManager.Instance.CurrentRun != null
                ? RunManager.Instance.CurrentRun.GetAvailableNodes()
                : new string[0],
            _currentGraph.StartNodeId);
        SetStatusText(GetNodeStatus(node, state));
    }

    internal void OnNodePointerExited(string nodeId)
    {
        if (_hoveredNodeId != nodeId)
            return;
        _hoveredNodeId = null;
        if (_currentGraph != null)
            SetStatusText(GetDefaultStatus(_currentGraph));
    }

    private void SetStatusText(string value)
    {
        if (_statusText != null)
            _statusText.text = value;
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

        _statusText = null;
        _hoveredNodeId = null;

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

    public static IReadOnlyDictionary<string, MapNodeState> BuildNodeStates(
        MapGraph graph, IReadOnlyList<string> availableNodeIds)
    {
        var states = new Dictionary<string, MapNodeState>();
        if (graph == null)
            return states;

        foreach (var node in graph.Nodes.Values)
            states[node.Id] = GetNodeState(
                node, graph.LastVisitedNodeId, availableNodeIds, graph.StartNodeId);
        return states;
    }

    public static MapNodeState GetNodeState(
        MapNode node,
        string currentNodeId,
        IReadOnlyCollection<string> availableNodeIds,
        string startNodeId = null)
    {
        if (node == null)
            throw new System.ArgumentNullException(nameof(node));
        if (node.Id == currentNodeId)
            return MapNodeState.Current;
        if (node.IsVisited)
            return MapNodeState.Visited;
        // MapGraph keeps the origin implicit and therefore never marks it
        // visited. Derive its presentation state from route progress instead.
        if (startNodeId != null && node.Id == startNodeId)
            return currentNodeId == null ? MapNodeState.Current : MapNodeState.Visited;
        // Safe fallback for callers that only have node row data.
        if (startNodeId == null && currentNodeId == null && node.Row == 0)
            return MapNodeState.Current;
        if (availableNodeIds != null && availableNodeIds.Contains(node.Id))
            return MapNodeState.Available;
        return MapNodeState.Blocked;
    }

    public static MapConnectionState GetConnectionState(
        MapNode from, MapNode to, IReadOnlyDictionary<string, MapNodeState> nodeStates)
    {
        if (from == null) throw new System.ArgumentNullException(nameof(from));
        if (to == null) throw new System.ArgumentNullException(nameof(to));
        if (nodeStates == null) throw new System.ArgumentNullException(nameof(nodeStates));

        if (nodeStates.TryGetValue(from.Id, out var fromState) &&
            nodeStates.TryGetValue(to.Id, out var toState))
        {
            if (fromState == MapNodeState.Current && toState == MapNodeState.Available)
                return MapConnectionState.Available;
            // The origin is implicit (it is not visited by RunState), so keep
            // its edge to the current node highlighted as part of the route.
            if (toState == MapNodeState.Current && from.Row == 0)
                return MapConnectionState.Visited;
            if (toState == MapNodeState.Visited ||
                (fromState == MapNodeState.Visited && toState == MapNodeState.Current))
                return MapConnectionState.Visited;
        }
        return MapConnectionState.Blocked;
    }

    private Color GetNodeStateColor(MapNode node, MapNodeState state)
    {
        switch (state)
        {
            case MapNodeState.Current:
                return CurrentColor;
            case MapNodeState.Visited:
                return Color.Lerp(GetNodeColor(node.Type), VisitedColor, 0.65f);
            case MapNodeState.Blocked:
                return DisabledColor;
            default:
                return GetNodeColor(node.Type);
        }
    }

    private Color GetConnectionColor(MapConnectionState state)
    {
        switch (state)
        {
            case MapConnectionState.Available: return AvailableLineColor;
            case MapConnectionState.Visited: return VisitedLineColor;
            default: return BlockedLineColor;
        }
    }

    private static string GetDefaultStatus(MapGraph graph)
    {
        if (graph == null)
            return string.Empty;
        if (graph.LastVisitedNodeId == null)
            return "Choose an AVAILABLE node to start the route.";
        return "Choose an AVAILABLE node connected to your CURRENT position.";
    }

    private static string GetNodeStatus(MapNode node, MapNodeState state)
    {
        switch (state)
        {
            case MapNodeState.Current: return $"CURRENT — {GetNodeTypeName(node.Type)}";
            case MapNodeState.Available: return $"AVAILABLE — travel to {GetNodeTypeName(node.Type)}";
            case MapNodeState.Visited: return $"VISITED — {GetNodeTypeName(node.Type)} already resolved";
            default: return $"BLOCKED — {GetNodeTypeName(node.Type)} is not on your current route";
        }
    }

    private static string GetNodeTypeName(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Combat => "Combat",
            MapNodeType.Elite => "Elite",
            MapNodeType.Boss => "Boss",
            MapNodeType.Rest => "Rest",
            MapNodeType.Shop => "Shop",
            _ => "Unknown"
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
        return $"{symbol}\n{GetNodeTypeName(node.Type)}";
    }
}
