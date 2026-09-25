namespace GhostUserRunner.Core.Planning;

public sealed class BoundedHistory<T>(int capacity)
{
    private readonly Queue<T> _items = new();
    public int Count => _items.Count;
    public IReadOnlyCollection<T> Items => _items;
    public void Add(T item)
    {
        _items.Enqueue(item);
        while (_items.Count > capacity) _items.Dequeue();
    }
}
