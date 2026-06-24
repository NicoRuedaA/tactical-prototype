using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// A single node on the procedural StS-style map.
    /// ConnectedNodeIds form directed edges to nodes in the next row.
    /// Pure C# — no Unity dependencies, fully unit-testable.
    /// </summary>
    public sealed class MapNode
    {
        public string Id { get; }
        public MapNodeType Type { get; }
        public int Row { get; }
        public int Col { get; }
        public List<string> ConnectedNodeIds { get; }
        public bool IsVisited { get; set; }

        public MapNode(string id, MapNodeType type, int row, int col)
        {
            Id = id;
            Type = type;
            Row = row;
            Col = col;
            ConnectedNodeIds = new List<string>();
            IsVisited = false;
        }
    }
}
