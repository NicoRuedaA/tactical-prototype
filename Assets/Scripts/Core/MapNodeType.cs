namespace Game.Core
{
    /// <summary>
    /// Types of nodes on the procedural StS-style map.
    /// Pure enum — no behavior, no Unity dependencies.
    /// </summary>
    public enum MapNodeType
    {
        Combat,
        Elite,
        Boss,
        Rest,
        Shop
    }
}
