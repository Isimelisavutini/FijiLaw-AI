using FijiLaw.Domain;
using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed class PostgresDemoAccountSeeder(string connectionString, PostgresMembershipAuthStore authStore, PostgresCreditWalletStore creditStore)
{
    private const string Password = "FijiLawDemo2026!";

    private static readonly (string Email, string Name, string Role, string Plan)[] Accounts =
    {
        ("free@demo.fijilaw.ai", "Free Member Demo", MembershipRoles.Citizen, MembershipPlans.Free),
        ("personal@demo.fijilaw.ai", "Personal Plus Demo", MembershipRoles.Citizen, MembershipPlans.PersonalPlus),
        ("lawyer@demo.fijilaw.ai", "Lawyer Professional Demo", MembershipRoles.Lawyer, MembershipPlans.LawyerProfessional),
        ("firmstarter@demo.fijilaw.ai", "Firm Starter Demo", MembershipRoles.FirmAdmin, MembershipPlans.FirmStarter),
        ("firmpro@demo.fijilaw.ai", "Firm Professional Demo", MembershipRoles.FirmAdmin, MembershipPlans.FirmProfessional),
        ("firmpremium@demo.fijilaw.ai", "Firm Premium Demo", MembershipRoles.FirmAdmin, MembershipPlans.FirmPremium),
        ("institution@demo.fijilaw.ai", "Institutional Partner Demo", MembershipRoles.Institutional, MembershipPlans.Institutional),
        ("admin@demo.fijilaw.ai", "FijiLaw Administrator Demo", MembershipRoles.PlatformAdmin, MembershipPlans.Free)
    };

    public async Task EnsureSeededAsync(CancellationToken ct = default)
    {
        foreach (var account in Accounts)
        {
            var userId = await FindUserIdAsync(account.Email, ct);
            if (userId is null)
            {
                var session = await authStore.RegisterAsync(new RegisterRequest(account.Email, Password, account.Name, account.Plan), ct);
                userId = session.UserId;
            }
            await ConfigureAsync(userId.Value, account.Role, account.Plan, ct);
            await creditStore.GetWalletAsync(userId.Value, account.Plan, ct);
        }
    }

    private async Task<Guid?> FindUserIdAsync(string email, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT id FROM app_users WHERE email=@email LIMIT 1", connection);
        command.Parameters.AddWithValue("email", email);
        var value = await command.ExecuteScalarAsync(ct);
        return value is Guid id ? id : null;
    }

    private async Task ConfigureAsync(Guid userId, string roleCode, string planCode, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        await using (var user = new NpgsqlCommand("UPDATE app_users SET email_verified=TRUE,requested_plan_code=@plan,updated_at=NOW() WHERE id=@uid", connection, tx))
        { user.Parameters.AddWithValue("uid", userId); user.Parameters.AddWithValue("plan", planCode); await user.ExecuteNonQueryAsync(ct); }

        await using (var roles = new NpgsqlCommand("DELETE FROM user_roles WHERE user_id=@uid", connection, tx))
        { roles.Parameters.AddWithValue("uid", userId); await roles.ExecuteNonQueryAsync(ct); }
        await using (var role = new NpgsqlCommand("INSERT INTO user_roles(user_id,role_id) SELECT @uid,id FROM roles WHERE code=@role ON CONFLICT DO NOTHING", connection, tx))
        { role.Parameters.AddWithValue("uid", userId); role.Parameters.AddWithValue("role", roleCode); await role.ExecuteNonQueryAsync(ct); }

        await using (var deactivate = new NpgsqlCommand("UPDATE subscriptions SET status='inactive',updated_at=NOW() WHERE user_id=@uid AND status='active'", connection, tx))
        { deactivate.Parameters.AddWithValue("uid", userId); await deactivate.ExecuteNonQueryAsync(ct); }
        await using (var plan = new NpgsqlCommand("INSERT INTO subscriptions(user_id,plan_id,status,billing_interval,current_period_start,current_period_end,started_at) SELECT @uid,id,'active','demo',NOW(),NOW()+INTERVAL '30 days',NOW() FROM subscription_plans WHERE code=@plan", connection, tx))
        { plan.Parameters.AddWithValue("uid", userId); plan.Parameters.AddWithValue("plan", planCode); await plan.ExecuteNonQueryAsync(ct); }

        await tx.CommitAsync(ct);
    }
}
