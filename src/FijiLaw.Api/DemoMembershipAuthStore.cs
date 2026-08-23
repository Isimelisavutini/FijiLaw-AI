using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FijiLaw.Domain;

namespace FijiLaw.Api;

public sealed class DemoMembershipAuthStore
{
    private readonly byte[]? _passwordHash;
    private readonly ConcurrentDictionary<string, DemoSession> _sessions = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, AuthenticatedMember> _members;

    public DemoMembershipAuthStore(string? sharedPassword)
    {
        IsEnabled = !string.IsNullOrWhiteSpace(sharedPassword);
        _passwordHash = IsEnabled ? SHA256.HashData(Encoding.UTF8.GetBytes(sharedPassword!)) : null;
        _members = BuildMembers();
    }

    public bool IsEnabled { get; }

    public AuthSessionResult Login(LoginRequest request)
    {
        if (!IsEnabled) throw new InvalidOperationException("Demo authentication is disabled.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (!_members.TryGetValue(email, out var member) || !PasswordMatches(request.Password))
            throw new UnauthorizedAccessException();

        CleanupExpiredSessions();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(12);
        _sessions[token] = new DemoSession(member.UserId, expiresAt);
        return new AuthSessionResult(token, expiresAt, member.UserId, member.Email, member.DisplayName);
    }

    public AuthenticatedMember? Resolve(string? token)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(token)) return null;
        if (!_sessions.TryGetValue(token, out var session)) return null;
        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }
        return _members.Values.FirstOrDefault(x => x.UserId == session.UserId);
    }

    public void Revoke(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token)) _sessions.TryRemove(token, out _);
    }

    public IReadOnlyList<object> PublicAccounts() => _members.Values
        .OrderBy(x => x.PlanCode)
        .Select(x => (object)new { x.Email, x.DisplayName, x.PlanCode, x.Roles })
        .ToArray();

    private bool PasswordMatches(string password)
    {
        if (_passwordHash is null || string.IsNullOrEmpty(password)) return false;
        var supplied = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(_passwordHash, supplied);
    }

    private void CleanupExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _sessions)
            if (pair.Value.ExpiresAt <= now) _sessions.TryRemove(pair.Key, out _);
    }

    private static IReadOnlyDictionary<string, AuthenticatedMember> BuildMembers()
    {
        static AuthenticatedMember Member(string id, string email, string name, string role, string plan, params string[] permissions) =>
            new(Guid.Parse(id), email, name, true, new[] { role }, permissions, plan, plan == MembershipPlans.Free ? "free" : "active", DateTimeOffset.UtcNow.AddDays(30));

        var dashboard = MembershipPermissions.DashboardAccess;
        var billing = MembershipPermissions.BillingView;
        var analytics = MembershipPermissions.AnalyticsView;
        var firmBase = new[] { dashboard, MembershipPermissions.CasesManage, MembershipPermissions.DocumentsAnalyse, MembershipPermissions.ReferralsManage, MembershipPermissions.LeadsView, MembershipPermissions.LeadsManage, MembershipPermissions.FirmManage, analytics, billing };

        var members = new[]
        {
            Member("10000000-0000-0000-0000-000000000001", "free@demo.fijilaw.ai", "Free Member Demo", MembershipRoles.Citizen, MembershipPlans.Free),
            Member("10000000-0000-0000-0000-000000000002", "personal@demo.fijilaw.ai", "Personal Plus Demo", MembershipRoles.Citizen, MembershipPlans.PersonalPlus,
                dashboard, MembershipPermissions.CasesCreate, MembershipPermissions.CasesViewOwn, MembershipPermissions.DocumentsAnalyse, MembershipPermissions.DocumentsStore, MembershipPermissions.ReferralsRequest, billing),
            Member("10000000-0000-0000-0000-000000000003", "lawyer@demo.fijilaw.ai", "Lawyer Professional Demo", MembershipRoles.Lawyer, MembershipPlans.LawyerProfessional,
                dashboard, MembershipPermissions.CasesManage, MembershipPermissions.DocumentsAnalyse, MembershipPermissions.ReferralsManage, MembershipPermissions.LeadsView, MembershipPermissions.LeadsManage, MembershipPermissions.LawyerProfileManage, analytics, billing),
            Member("10000000-0000-0000-0000-000000000004", "firmstarter@demo.fijilaw.ai", "Firm Starter Demo", MembershipRoles.FirmAdmin, MembershipPlans.FirmStarter, firmBase),
            Member("10000000-0000-0000-0000-000000000005", "firmpro@demo.fijilaw.ai", "Firm Professional Demo", MembershipRoles.FirmAdmin, MembershipPlans.FirmProfessional, firmBase.Append(MembershipPermissions.FirmUsersManage).ToArray()),
            Member("10000000-0000-0000-0000-000000000006", "firmpremium@demo.fijilaw.ai", "Firm Premium Demo", MembershipRoles.FirmAdmin, MembershipPlans.FirmPremium, firmBase.Append(MembershipPermissions.FirmUsersManage).Append(MembershipPermissions.DirectoryPriorityPlacement).ToArray()),
            Member("10000000-0000-0000-0000-000000000007", "institution@demo.fijilaw.ai", "Institutional Partner Demo", MembershipRoles.Institutional, MembershipPlans.Institutional, dashboard),
            Member("10000000-0000-0000-0000-000000000008", "admin@demo.fijilaw.ai", "FijiLaw Administrator Demo", MembershipRoles.PlatformAdmin, MembershipPlans.Free,
                dashboard, MembershipPermissions.CasesCreate, MembershipPermissions.CasesViewOwn, MembershipPermissions.CasesManage, MembershipPermissions.DocumentsAnalyse, MembershipPermissions.DocumentsStore,
                MembershipPermissions.ReferralsRequest, MembershipPermissions.ReferralsManage, MembershipPermissions.LeadsView, MembershipPermissions.LeadsManage, MembershipPermissions.LawyerProfileManage,
                MembershipPermissions.FirmManage, MembershipPermissions.FirmUsersManage, analytics, billing, MembershipPermissions.BillingManage, MembershipPermissions.DirectoryPriorityPlacement)
        };

        return members.ToDictionary(x => x.Email, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record DemoSession(Guid UserId, DateTimeOffset ExpiresAt);
}
