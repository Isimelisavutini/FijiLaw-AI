using System.Collections.Concurrent;
using FijiLaw.Domain;

namespace FijiLaw.Api;

public sealed class DemoCreditWalletStore : ICreditWalletStore
{
    private readonly ConcurrentDictionary<Guid, WalletState> _wallets = new();
    private readonly ConcurrentDictionary<Guid, List<CreditTransactionSummary>> _history = new();
    private readonly object _gate = new();

    public Task<CreditWalletSnapshot> GetWalletAsync(Guid userId, string planCode, CancellationToken ct = default)
    {
        lock (_gate)
        {
            EnsureAllowance(userId, planCode);
            return Task.FromResult(ToSnapshot(userId, _wallets[userId]));
        }
    }

    public Task<CreditReservation?> ReserveAsync(Guid userId, string planCode, int credits, string serviceCode, string correlationId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            EnsureAllowance(userId, planCode);
            var state = _wallets[userId];
            if (state.Balance < credits) return Task.FromResult<CreditReservation?>(null);
            var before = state.Balance; state.Balance -= credits;
            var id = Guid.NewGuid();
            AddHistory(userId, new CreditTransactionSummary(id, "usage", "reserved", -credits, before, state.Balance, serviceCode, correlationId, null, DateTimeOffset.UtcNow));
            return Task.FromResult<CreditReservation?>(new CreditReservation(id, userId, credits, serviceCode, correlationId));
        }
    }

    public Task CompleteAsync(CreditReservation reservation, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var list = _history.GetOrAdd(reservation.UserId, _ => new());
            var index = list.FindIndex(x => x.Id == reservation.TransactionId && x.Status == "reserved");
            if (index >= 0)
            {
                list[index] = list[index] with { Status = "completed" };
                _wallets[reservation.UserId].LifetimeUsed += reservation.Credits;
            }
            return Task.CompletedTask;
        }
    }

    public Task RefundAsync(CreditReservation reservation, string reason, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var list = _history.GetOrAdd(reservation.UserId, _ => new());
            var index = list.FindIndex(x => x.Id == reservation.TransactionId && x.Status == "reserved");
            if (index >= 0)
            {
                var state = _wallets[reservation.UserId];
                state.Balance += reservation.Credits;
                list[index] = list[index] with { Status = "refunded" };
                AddHistory(reservation.UserId, new CreditTransactionSummary(Guid.NewGuid(), "refund", "completed", reservation.Credits, state.Balance - reservation.Credits, state.Balance, reservation.ServiceCode, reservation.CorrelationId, reason, DateTimeOffset.UtcNow));
            }
            return Task.CompletedTask;
        }
    }

    public Task<CreditWalletSnapshot> GrantAsync(Guid userId, string planCode, int credits, string reason, bool purchased = false, string? providerReference = null, CancellationToken ct = default)
    {
        lock (_gate)
        {
            EnsureAllowance(userId, planCode);
            var state = _wallets[userId]; var before = state.Balance; state.Balance += credits;
            if (purchased) state.LifetimePurchased += credits; else state.LifetimeGranted += credits;
            AddHistory(userId, new CreditTransactionSummary(Guid.NewGuid(), purchased ? "purchase" : "adjustment", "completed", credits, before, state.Balance, null, null, providerReference ?? reason, DateTimeOffset.UtcNow));
            return Task.FromResult(ToSnapshot(userId, state));
        }
    }

    public Task<IReadOnlyList<CreditTransactionSummary>> GetHistoryAsync(Guid userId, int limit = 50, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var items = _history.TryGetValue(userId, out var list) ? list.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 100)).ToArray() : Array.Empty<CreditTransactionSummary>();
            return Task.FromResult<IReadOnlyList<CreditTransactionSummary>>(items);
        }
    }

    private void EnsureAllowance(Guid userId, string planCode)
    {
        var state = _wallets.GetOrAdd(userId, _ => new WalletState());
        var key = FijiLawCreditCatalog.AllowanceKey(planCode, DateTimeOffset.UtcNow);
        if (state.LastAllowanceKey == key) return;
        var allowance = FijiLawCreditCatalog.IncludedCredits(planCode);
        if (allowance <= 0) { state.LastAllowanceKey = key; return; }
        var before = state.Balance; state.Balance += allowance; state.LifetimeGranted += allowance; state.LastAllowanceKey = key;
        AddHistory(userId, new CreditTransactionSummary(Guid.NewGuid(), "allowance", "completed", allowance, before, state.Balance, null, null, key, DateTimeOffset.UtcNow));
    }

    private void AddHistory(Guid userId, CreditTransactionSummary item) => _history.GetOrAdd(userId, _ => new()).Add(item);
    private static CreditWalletSnapshot ToSnapshot(Guid userId, WalletState s) => new(userId, s.Balance, s.LifetimePurchased, s.LifetimeGranted, s.LifetimeUsed, s.LastAllowanceKey);

    private sealed class WalletState
    {
        public int Balance { get; set; }
        public long LifetimePurchased { get; set; }
        public long LifetimeGranted { get; set; }
        public long LifetimeUsed { get; set; }
        public string? LastAllowanceKey { get; set; }
    }
}
