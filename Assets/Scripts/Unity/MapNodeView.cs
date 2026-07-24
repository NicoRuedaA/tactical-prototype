using UnityEngine.EventSystems;

/// <summary>
/// Pointer feedback bridge for a map node button. The map owns navigation and
/// this component only forwards hover events so the button remains reusable.
/// </summary>
public sealed class MapNodeView : UnityEngine.MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private MapView _owner;

    public string NodeId { get; private set; }

    public void Configure(MapView owner, string nodeId)
    {
        _owner = owner;
        NodeId = nodeId;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.OnNodePointerEntered(NodeId);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.OnNodePointerExited(NodeId);
    }
}
