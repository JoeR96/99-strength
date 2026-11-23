using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using A2S.Application.Sync;

namespace A2S.Infrastructure.Services;

/// <summary>
/// In-process <see cref="ISyncNotifier"/>: a bounded channel per connected client, grouped by user. Right for one
/// API instance. With several instances a bump on one would not reach clients connected to another; the upgrade
/// is to <c>NOTIFY a2s_sync, '{user}:{version}:{areas}'</c> after the bump and have one <c>LISTEN</c> connection per
/// instance feed this same fan-out (see docs/development.md).
/// </summary>
public sealed class InProcessSyncNotifier : ISyncNotifier
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<SyncChange>>> _byUser = new(StringComparer.Ordinal);

    public ISyncSubscription Subscribe(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        // A slow client only needs the latest bumps: the oldest are dropped, never the writer blocked.
        var channel = Channel.CreateBounded<SyncChange>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
        var id = Guid.NewGuid();
        _byUser.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<SyncChange>>())[id] = channel;
        return new Subscription(channel, () => Remove(userId, id));
    }

    public void Publish(string userId, SyncChange change)
    {
        if (string.IsNullOrEmpty(userId) || !_byUser.TryGetValue(userId, out var channels))
        {
            return;
        }

        foreach (var channel in channels.Values)
        {
            channel.Writer.TryWrite(change);
        }
    }

    /// <summary>Connected clients for a user (tests and diagnostics).</summary>
    public int SubscriberCount(string userId) => _byUser.TryGetValue(userId, out var channels) ? channels.Count : 0;

    private void Remove(string userId, Guid id)
    {
        if (_byUser.TryGetValue(userId, out var channels) && channels.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
            if (channels.IsEmpty)
            {
                _byUser.TryRemove(new KeyValuePair<string, ConcurrentDictionary<Guid, Channel<SyncChange>>>(userId, channels));
            }
        }
    }

    private sealed class Subscription : ISyncSubscription
    {
        private readonly Channel<SyncChange> _channel;
        private readonly Action _dispose;
        private int _disposed;

        public Subscription(Channel<SyncChange> channel, Action dispose)
        {
            _channel = channel;
            _dispose = dispose;
        }

        public async IAsyncEnumerable<SyncChange> ReadAllAsync([EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var change in _channel.Reader.ReadAllAsync(ct))
            {
                yield return change;
            }
        }

        public async Task<SyncChange?> WaitAsync(TimeSpan timeout, CancellationToken ct)
        {
            if (_channel.Reader.TryRead(out var ready))
            {
                return ready;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            try
            {
                return await _channel.Reader.ReadAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return null;
            }
            catch (ChannelClosedException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _dispose();
            }
        }
    }
}
