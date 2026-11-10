namespace Mw5SaveEditor.Core.Game;

/// <summary>Outcome of a multi-item operation, reported once for the whole batch.</summary>
public sealed class BatchResult<T>
{
    private readonly List<T> _succeeded = [];
    private readonly List<(T Item, string Reason)> _skipped = [];

    public IReadOnlyList<T> Succeeded => _succeeded;

    public IReadOnlyList<(T Item, string Reason)> Skipped => _skipped;

    internal void Succeed(T item) => _succeeded.Add(item);

    internal void Skip(T item, string reason) => _skipped.Add((item, reason));
}
