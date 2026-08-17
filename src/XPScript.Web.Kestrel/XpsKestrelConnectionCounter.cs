namespace XPScript.Web.Kestrel;

internal sealed class XpsKestrelConnectionCounter
{
    private long _active;

    public long Active => Math.Max(0, Interlocked.Read(ref _active));

    public IDisposable Track()
    {
        Interlocked.Increment(ref _active);
        return new Scope(this);
    }

    private sealed class Scope : IDisposable
    {
        private XpsKestrelConnectionCounter? _owner;

        public Scope(XpsKestrelConnectionCounter owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _owner, null);
            if (current is not null) Interlocked.Decrement(ref current._active);
        }
    }
}
