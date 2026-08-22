using FijiLaw.Domain;
using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed class PostgresMembershipRepository(string connectionString)
{
    public async Task<IReadOnlyList<MembershipPlanSummary>> GetPlansAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT sp.code, sp.name, sp.audience, sp.monthly_price_fjd, sp.annual_price_fjd, sp.is_paid,
                   COALESCE(array_agg(p.code ORDER BY p.code) FILTER (WHERE p.code IS NOT NULL), ARRAY[]::text[]) AS entitlements
            FROM subscription_plans sp
            LEFT JOIN plan_entitlements pe ON pe.plan_id = sp.id
            LEFT JOIN permissions p ON p.id = pe.permission_id
            WHERE sp.is_active = TRUE
            GROUP BY sp.id, sp.code, sp.name, sp.audience, sp.monthly_price_fjd, sp.annual_price_fjd, sp.is_paid, sp.sort_order
            ORDER BY sp.sort_order, sp.monthly_price_fjd NULLS LAST;
            """;

        var results = new List<MembershipPlanSummary>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(new MembershipPlanSummary(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.GetBoolean(5), reader.GetFieldValue<string[]>(6)));
        }
        return results;
    }

    public async Task<MembershipAccessSnapshot?> GetAccessAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = """
            WITH active_subscription AS (
                SELECT s.*, sp.code AS plan_code
                FROM subscriptions s
                JOIN subscription_plans sp ON sp.id = s.plan_id
                WHERE s.user_id = @userId
                  AND s.status IN ('active','trialing')
                  AND (s.current_period_end IS NULL OR s.current_period_end > NOW())
                ORDER BY s.created_at DESC
                LIMIT 1
            ),
            role_codes AS (
                SELECT DISTINCT r.code
                FROM user_roles ur JOIN roles r ON r.id=ur.role_id
                WHERE ur.user_id=@userId
            ),
            effective_permissions AS (
                SELECT DISTINCT p.code
                FROM active_subscription a
                JOIN subscription_plans sp ON sp.id=a.plan_id
                JOIN plan_entitlements pe ON pe.plan_id=sp.id
                JOIN permissions p ON p.id=pe.permission_id
                UNION
                SELECT DISTINCT p.code
                FROM user_roles ur
                JOIN role_permissions rp ON rp.role_id=ur.role_id
                JOIN permissions p ON p.id=rp.permission_id
                WHERE ur.user_id=@userId
            )
            SELECT u.id,
                   COALESCE(a.plan_code,'free') AS plan_code,
                   COALESCE(a.status,'active') AS subscription_status,
                   a.current_period_end,
                   COALESCE((SELECT array_agg(code ORDER BY code) FROM role_codes), ARRAY[]::text[]) AS roles,
                   COALESCE((SELECT array_agg(code ORDER BY code) FROM effective_permissions), ARRAY[]::text[]) AS permissions
            FROM app_users u
            LEFT JOIN active_subscription a ON TRUE
            WHERE u.id=@userId AND u.status='active';
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new MembershipAccessSnapshot(
            reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<string[]>(4), reader.GetFieldValue<string[]>(5));
    }

    public async Task RecordUsageAsync(UsageEntry entry, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO usage_ledger
                (user_id, organisation_id, subscription_id, usage_type, quantity, unit, estimated_cost_fjd, correlation_id)
            VALUES
                (@userId, @organisationId, @subscriptionId, @usageType, @quantity, @unit, @estimatedCostFjd, @correlationId);
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", (object?)entry.UserId ?? DBNull.Value);
        command.Parameters.AddWithValue("organisationId", (object?)entry.OrganisationId ?? DBNull.Value);
        command.Parameters.AddWithValue("subscriptionId", (object?)entry.SubscriptionId ?? DBNull.Value);
        command.Parameters.AddWithValue("usageType", entry.UsageType);
        command.Parameters.AddWithValue("quantity", entry.Quantity);
        command.Parameters.AddWithValue("unit", entry.Unit);
        command.Parameters.AddWithValue("estimatedCostFjd", (object?)entry.EstimatedCostFjd ?? DBNull.Value);
        command.Parameters.AddWithValue("correlationId", (object?)entry.CorrelationId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }
}
