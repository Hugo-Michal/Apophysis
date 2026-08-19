using System.Threading.Channels;

namespace FractalFlameCurator.Pipeline;

public sealed class BoundedRenderQueue<T>
{
    private readonly Channel<T> _channel;
    private int _count;

    public BoundedRenderQueue(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait, SingleWriter = true, SingleReader = false });
    }

    public int Capacity { get; }
    public int Count => Volatile.Read(ref _count);
    public void Complete() => _channel.Writer.TryComplete();

    public async ValueTask EnqueueAsync(T item, CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(item, cancellationToken);
        Interlocked.Increment(ref _count);
    }

    public async ValueTask<T> DequeueAsync(CancellationToken cancellationToken)
    {
        var item = await _channel.Reader.ReadAsync(cancellationToken);
        Interlocked.Decrement(ref _count);
        return item;
    }
}

