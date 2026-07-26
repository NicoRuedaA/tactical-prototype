public sealed class MapNodeView : UnityEngine.MonoBehaviour
{
    private MapView _owner;

    public string NodeId { get; private set; }

    public void Configure(MapView owner, string nodeId)
    {
        _owner = owner;
        NodeId = nodeId;
    }

}
