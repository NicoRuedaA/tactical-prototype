using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using Game.Core;

public enum MapNodeState { Current, Available, Visited, Blocked }
public enum MapConnectionState { Available, Visited, Blocked }

public sealed class MapView : MonoBehaviour
{
    public UIDocument Document;
    public float RowSpacing = 120f, ColSpacing = 100f, RowOffsetX;
    private MapGraph _graph;
    private VisualElement _content;
    private Label _status;
    private void OnEnable() { Rebuild(); }
    public void Rebuild()
    {
        Document = Document != null ? Document : GetComponent<UIDocument>();
        if (Document == null) Document = gameObject.AddComponent<UIDocument>();
        Document.panelSettings ??= ScriptableObject.CreateInstance<PanelSettings>();
        Document.sortingOrder = 100;
        var root = Document.rootVisualElement; root.Clear(); root.style.flexGrow = 1;
        root.style.backgroundColor = new Color(0.025f, 0.035f, 0.065f, 1f);
        root.style.paddingLeft = 32; root.style.paddingRight = 32; root.style.paddingTop = 24; root.style.paddingBottom = 24;
        var run = RunManager.Instance?.CurrentRun;
        if (run?.Graph == null) { root.Add(CreateLabel("No active run.", 22)); return; }
        _graph = run.Graph;

        var header = new VisualElement { name = "MapHeader" };
        header.style.backgroundColor = new Color(0.06f, 0.09f, 0.16f, 1f);
        header.style.paddingLeft = 18; header.style.paddingRight = 18; header.style.paddingTop = 14; header.style.paddingBottom = 14;
        header.style.borderBottomWidth = 2; header.style.borderBottomColor = new Color(0.25f, 0.65f, 0.9f, 1f);
        root.Add(header);
        var title = CreateLabel("EXPEDITION MAP", 28); title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.color = new Color(0.45f, 0.85f, 1f, 1f); header.Add(title);
        var subtitle = CreateLabel("Choose your next encounter", 14); subtitle.style.color = new Color(0.65f, 0.72f, 0.84f, 1f); header.Add(subtitle);

        var roster = CreateLabel(FormatRosterSummary(run.Pieces), 13); roster.style.marginTop = 14; roster.style.color = new Color(0.78f, 0.84f, 0.92f, 1f); root.Add(roster);
        _status = CreateLabel(string.Empty, 14); _status.style.color = new Color(1f, 0.78f, 0.35f, 1f); _status.style.marginTop = 8; root.Add(_status);
        _content = new VisualElement { name = "MapNodes" }; _content.style.height = 600; _content.style.marginTop = 16; _content.style.backgroundColor = new Color(0.045f, 0.06f, 0.1f, 1f); _content.style.borderTopWidth = 1; _content.style.borderTopColor = new Color(0.16f, 0.24f, 0.36f, 1f); root.Add(_content);
        var states = BuildNodeStates(_graph, run.GetAvailableNodes());
        foreach (var node in _graph.Nodes.Values)
        {
            var id = node.Id; var button = new Button(() => RunManager.Instance?.OnNodeSelected(id)) { name = "Node-" + id, text = GetNodeLabel(node) };
            button.style.color = Color.white; button.style.backgroundColor = GetNodeColor(node.Type); button.style.fontSize = 14; button.style.unityFontStyleAndWeight = FontStyle.Bold; button.style.width = 120; button.style.height = 58;
            button.style.borderTopLeftRadius = 8; button.style.borderTopRightRadius = 8; button.style.borderBottomLeftRadius = 8; button.style.borderBottomRightRadius = 8;
            button.style.borderTopWidth = 2; button.style.borderRightWidth = 2; button.style.borderBottomWidth = 2; button.style.borderLeftWidth = 2;
            button.style.borderTopColor = new Color(1f, 1f, 1f, 0.22f); button.style.borderRightColor = button.style.borderTopColor; button.style.borderBottomColor = button.style.borderTopColor; button.style.borderLeftColor = button.style.borderTopColor;
            button.SetEnabled(states[id] == MapNodeState.Available); button.style.position = Position.Absolute;
            if (states[id] == MapNodeState.Current)
            {
                button.style.borderTopColor = Color.white; button.style.borderRightColor = Color.white; button.style.borderBottomColor = Color.white; button.style.borderLeftColor = Color.white;
            }
            if (states[id] == MapNodeState.Visited) button.style.opacity = 0.45f;
            if (states[id] == MapNodeState.Blocked) button.style.opacity = 0.2f;
            button.style.left = node.Col * ColSpacing + node.Row * RowOffsetX; button.style.top = node.Row * RowSpacing; _content.Add(button);
        }
        _status.text = _graph.LastVisitedNodeId == null ? "Choose an AVAILABLE node to start the route." : "Choose an AVAILABLE node connected to your CURRENT position.";
    }
    private static Label CreateLabel(string text, int size) { var label = new Label(text); label.style.color = Color.white; label.style.fontSize = size; label.style.marginBottom = 4; return label; }
    public static Color GetNodeColor(MapNodeType type) => type switch { MapNodeType.Combat => new Color(.3f,.5f,1f), MapNodeType.Elite => new Color(1f,.6f,.1f), MapNodeType.Boss => new Color(1f,.2f,.2f), MapNodeType.Rest => new Color(.2f,.8f,.2f), MapNodeType.Shop => new Color(1f,.9f,.1f), _ => Color.gray };
    public static IReadOnlyDictionary<string, MapNodeState> BuildNodeStates(MapGraph graph, IReadOnlyList<string> ids)
    { var result = new Dictionary<string, MapNodeState>(); if (graph == null) return result; foreach (var n in graph.Nodes.Values) result[n.Id] = GetNodeState(n, graph.LastVisitedNodeId, ids, graph.StartNodeId); return result; }
    public static MapNodeState GetNodeState(MapNode node, string current, IReadOnlyCollection<string> available, string start = null)
    { if (node == null) throw new System.ArgumentNullException(nameof(node)); if (node.Id == current) return MapNodeState.Current; if (node.IsVisited) return MapNodeState.Visited; if (start != null && node.Id == start) return current == null ? MapNodeState.Current : MapNodeState.Visited; if (start == null && current == null && node.Row == 0) return MapNodeState.Current; return available != null && available.Contains(node.Id) ? MapNodeState.Available : MapNodeState.Blocked; }
    public static MapConnectionState GetConnectionState(MapNode from, MapNode to, IReadOnlyDictionary<string, MapNodeState> states)
    { if (states[from.Id] == MapNodeState.Current && states[to.Id] == MapNodeState.Available) return MapConnectionState.Available; if (states[to.Id] == MapNodeState.Current && from.Row == 0 || states[to.Id] == MapNodeState.Visited || states[from.Id] == MapNodeState.Visited && states[to.Id] == MapNodeState.Current) return MapConnectionState.Visited; return MapConnectionState.Blocked; }
    public static string FormatRestHealResult(RestHealResult result) { if (result == null) return string.Empty; var b = new StringBuilder($"REST — healed {result.TotalDelta} HP ({result.ConfiguredPercent}% of EffectiveMaxHp)"); if (result.Pieces.Count > 0) { b.Append(": "); for (var i=0;i<result.Pieces.Count;i++) { if (i>0)b.Append(", "); var p=result.Pieces[i]; b.Append(string.IsNullOrEmpty(p.PieceName)?p.PieceId:p.PieceName).Append(' ').Append(p.BeforeHp).Append('→').Append(p.AfterHp).Append(" (+").Append(p.Delta).Append(')'); } } return b.ToString(); }
    public static string FormatRosterSummary(IReadOnlyList<Piece> pieces) { if (pieces == null || pieces.Count == 0) return "ROSTER — no player pieces."; return "ROSTER — " + string.Join(" | ", pieces.Select(p => p == null ? "unknown piece" : $"{(string.IsNullOrEmpty(p.Name) ? p.Id : p.Name)} HP {p.Hp}/{p.EffectiveMaxHp}{(p.IsDead ? " (DEFEATED)" : "")}")); }
    private static string GetNodeLabel(MapNode n) => $"{n.Type}\nRow {n.Row}, Col {n.Col}";
}
