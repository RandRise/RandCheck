namespace UaeDocumentIntelligence.Api.Security;

public sealed class RequestLimitOptions
{
    public bool Enabled { get; set; } = true;
    public int WindowSeconds { get; set; } = 60;
    public int MaxPartitions { get; set; } = 8192;
    public int GlobalRequests { get; set; } = 1200;
    public int GlobalConcurrent { get; set; } = 64;
    public int IpRequests { get; set; } = 300;
    public int IpConcurrent { get; set; } = 16;
    public int OrganizationRequests { get; set; } = 600;
    public int OrganizationConcurrent { get; set; } = 16;
    public int ActorRequests { get; set; } = 180;
    public int ActorConcurrent { get; set; } = 6;
    public int OrganizationWorkRequests { get; set; } = 60;
    public int OrganizationWorkConcurrent { get; set; } = 4;
    public int ActorWorkRequests { get; set; } = 20;
    public int ActorWorkConcurrent { get; set; } = 2;

    public static RequestLimitOptions Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var local = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var options = new RequestLimitOptions { Enabled = !local };
        configuration.GetSection("RequestLimits").Bind(options);
        if (!local && !options.Enabled) throw new InvalidOperationException("Request limits cannot be disabled outside Development or Testing.");
        if (options.WindowSeconds is < 1 or > 3600 || options.MaxPartitions is < 16 or > 65536 ||
            new[] { options.GlobalRequests, options.IpRequests, options.OrganizationRequests, options.ActorRequests,
                options.OrganizationWorkRequests, options.ActorWorkRequests }.Any(x => x is < 1 or > 100000) ||
            new[] { options.GlobalConcurrent, options.IpConcurrent, options.OrganizationConcurrent, options.ActorConcurrent,
                options.OrganizationWorkConcurrent, options.ActorWorkConcurrent }.Any(x => x is < 1 or > 1024))
            throw new InvalidOperationException("Request limits must use bounded positive configuration values.");
        return options;
    }
}

public sealed record RequestBudget(string Key, int Requests, int Concurrent);

// A bounded partition registry with atomic multi-budget admission. No waiting queue,
// timer per key, client-supplied identity or eviction of an active/unexpired budget.
public sealed class RequestBudgets(RequestLimitOptions options, TimeProvider time)
{
    private sealed class State(long start)
    {
        public long Start = start;
        public int Used;
        public int Active;
    }
    private readonly object _sync = new();
    private readonly Dictionary<string, State> _states = new(StringComparer.Ordinal);
    public IDisposable? TryAcquire(IReadOnlyList<RequestBudget> budgets, out int retryAfter)
    {
        lock (_sync)
        {
            retryAfter = 1;
            var now = time.GetTimestamp();
            bool Expired(State state) => time.GetElapsedTime(state.Start, now).TotalSeconds >= options.WindowSeconds;
            var missing = budgets.Count(b => !_states.ContainsKey(b.Key));
            if (_states.Count + missing > options.MaxPartitions)
            {
                foreach (var key in _states.Where(p => p.Value.Active == 0 && Expired(p.Value)).Select(p => p.Key).ToArray()) _states.Remove(key);
                missing = budgets.Count(b => !_states.ContainsKey(b.Key));
                if (_states.Count + missing > options.MaxPartitions) { retryAfter = options.WindowSeconds; return null; }
            }
            foreach (var budget in budgets)
            {
                if (!_states.TryGetValue(budget.Key, out var state)) continue;
                if (Expired(state)) { state.Start = now; state.Used = 0; }
                if (state.Used >= budget.Requests)
                {
                    retryAfter = Math.Max(1, (int)Math.Ceiling(options.WindowSeconds - time.GetElapsedTime(state.Start, now).TotalSeconds));
                    return null;
                }
                if (state.Active >= budget.Concurrent) return null;
            }
            var acquired = new List<State>(budgets.Count);
            foreach (var budget in budgets)
            {
                if (!_states.TryGetValue(budget.Key, out var state)) _states.Add(budget.Key, state = new State(now));
                state.Used++; state.Active++; acquired.Add(state);
            }
            return new Release(() => { lock (_sync) foreach (var state in acquired) state.Active--; });
        }
    }
    private sealed class Release(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
