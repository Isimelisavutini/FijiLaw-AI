using Npgsql;

namespace FijiLaw.Infrastructure;

public sealed class PostgresMembershipInitializer(string connectionString)
{
    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        const string sql = """
            CREATE EXTENSION IF NOT EXISTS pgcrypto;

            CREATE TABLE IF NOT EXISTS app_users (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
              email TEXT NOT NULL UNIQUE,
              display_name TEXT,
              identity_provider TEXT,
              identity_subject TEXT,
              phone_number TEXT,
              requested_plan_code TEXT,
              email_verified BOOLEAN NOT NULL DEFAULT FALSE,
              phone_verified BOOLEAN NOT NULL DEFAULT FALSE,
              identity_verified_at TIMESTAMPTZ,
              status TEXT NOT NULL DEFAULT 'pending',
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
              updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
              UNIQUE (identity_provider, identity_subject)
            );
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS requested_plan_code TEXT;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS identity_provider TEXT;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS identity_subject TEXT;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS phone_number TEXT;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS phone_verified BOOLEAN NOT NULL DEFAULT FALSE;
            ALTER TABLE app_users ADD COLUMN IF NOT EXISTS identity_verified_at TIMESTAMPTZ;
            ALTER TABLE app_users ALTER COLUMN status SET DEFAULT 'pending';
            CREATE UNIQUE INDEX IF NOT EXISTS ux_app_users_external_identity
              ON app_users(identity_provider, identity_subject)
              WHERE identity_provider IS NOT NULL AND identity_subject IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ux_app_users_phone
              ON app_users(phone_number)
              WHERE phone_number IS NOT NULL;

            CREATE TABLE IF NOT EXISTS roles (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(), code TEXT NOT NULL UNIQUE, name TEXT NOT NULL, description TEXT
            );
            CREATE TABLE IF NOT EXISTS user_roles (
              user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
              role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), PRIMARY KEY (user_id, role_id)
            );
            CREATE TABLE IF NOT EXISTS subscription_plans (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(), code TEXT NOT NULL UNIQUE, name TEXT NOT NULL, audience TEXT NOT NULL,
              monthly_price_fjd NUMERIC(12,2), annual_price_fjd NUMERIC(12,2), is_paid BOOLEAN NOT NULL DEFAULT FALSE,
              is_active BOOLEAN NOT NULL DEFAULT TRUE, sort_order INTEGER NOT NULL DEFAULT 0,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS permissions (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(), code TEXT NOT NULL UNIQUE, description TEXT
            );
            CREATE TABLE IF NOT EXISTS role_permissions (
              role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
              permission_id UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
              PRIMARY KEY (role_id, permission_id)
            );
            CREATE TABLE IF NOT EXISTS plan_entitlements (
              plan_id UUID NOT NULL REFERENCES subscription_plans(id) ON DELETE CASCADE,
              permission_id UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
              limit_value INTEGER, PRIMARY KEY (plan_id, permission_id)
            );
            CREATE TABLE IF NOT EXISTS organisations (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(), name TEXT NOT NULL, organisation_type TEXT NOT NULL,
              slug TEXT UNIQUE, verified BOOLEAN NOT NULL DEFAULT FALSE,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS organisation_memberships (
              organisation_id UUID NOT NULL REFERENCES organisations(id) ON DELETE CASCADE,
              user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
              membership_role TEXT NOT NULL DEFAULT 'member', status TEXT NOT NULL DEFAULT 'active',
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), PRIMARY KEY (organisation_id, user_id)
            );
            CREATE TABLE IF NOT EXISTS subscriptions (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
              user_id UUID REFERENCES app_users(id) ON DELETE CASCADE,
              organisation_id UUID REFERENCES organisations(id) ON DELETE CASCADE,
              plan_id UUID NOT NULL REFERENCES subscription_plans(id),
              billing_provider TEXT, provider_customer_id TEXT, provider_subscription_id TEXT,
              status TEXT NOT NULL DEFAULT 'inactive', billing_interval TEXT,
              current_period_start TIMESTAMPTZ, current_period_end TIMESTAMPTZ,
              cancel_at_period_end BOOLEAN NOT NULL DEFAULT FALSE,
              started_at TIMESTAMPTZ, cancelled_at TIMESTAMPTZ,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
              CHECK (user_id IS NOT NULL OR organisation_id IS NOT NULL)
            );
            CREATE TABLE IF NOT EXISTS usage_ledger (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
              user_id UUID REFERENCES app_users(id) ON DELETE SET NULL,
              organisation_id UUID REFERENCES organisations(id) ON DELETE SET NULL,
              subscription_id UUID REFERENCES subscriptions(id) ON DELETE SET NULL,
              usage_type TEXT NOT NULL, quantity NUMERIC(14,4) NOT NULL DEFAULT 1,
              unit TEXT NOT NULL DEFAULT 'request', estimated_cost_fjd NUMERIC(14,6),
              correlation_id TEXT, metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
              created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS billing_events (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(), provider TEXT NOT NULL,
              provider_event_id TEXT NOT NULL UNIQUE, event_type TEXT NOT NULL,
              user_id UUID REFERENCES app_users(id) ON DELETE SET NULL,
              organisation_id UUID REFERENCES organisations(id) ON DELETE SET NULL,
              subscription_id UUID REFERENCES subscriptions(id) ON DELETE SET NULL,
              amount_fjd NUMERIC(12,2), payload JSONB NOT NULL DEFAULT '{}'::jsonb,
              processed_at TIMESTAMPTZ, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            INSERT INTO roles (code,name,description) VALUES
              ('citizen','Registered Citizen','Registered public user'),
              ('lawyer','Lawyer','Individual legal practitioner'),
              ('firm_staff','Law Firm Staff','Staff member of a law firm'),
              ('firm_admin','Law Firm Administrator','Administrator of a law firm'),
              ('institutional','Institutional User','Partner organisation user'),
              ('platform_admin','FijiLaw Administrator','Platform administrator')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO subscription_plans (code,name,audience,monthly_price_fjd,annual_price_fjd,is_paid,sort_order) VALUES
              ('free','Free','citizen',0,0,FALSE,10),
              ('personal_plus','Personal Plus','citizen',20,200,TRUE,20),
              ('lawyer_professional','Lawyer Professional','lawyer',100,1000,TRUE,30),
              ('firm_starter','Law Firm Starter','law_firm',200,2000,TRUE,40),
              ('firm_professional','Law Firm Professional','law_firm',350,3500,TRUE,50),
              ('firm_premium','Law Firm Premium','law_firm',600,6000,TRUE,60),
              ('institutional','Institutional','institution',NULL,NULL,TRUE,70)
            ON CONFLICT (code) DO UPDATE SET
              name=EXCLUDED.name,audience=EXCLUDED.audience,monthly_price_fjd=EXCLUDED.monthly_price_fjd,
              annual_price_fjd=EXCLUDED.annual_price_fjd,is_paid=EXCLUDED.is_paid,sort_order=EXCLUDED.sort_order,updated_at=NOW();

            INSERT INTO permissions (code,description) VALUES
              ('Dashboard.Access','Access paid or authorised dashboard'),('Cases.Create','Create legal matters'),
              ('Cases.ViewOwn','View own matters'),('Cases.Manage','Manage cases'),('Documents.Analyse','Analyse documents'),
              ('Documents.Store','Store documents'),('Referrals.Request','Request referrals'),('Referrals.Manage','Manage referrals'),
              ('Leads.View','View leads'),('Leads.Manage','Manage leads'),('LawyerProfile.Manage','Manage lawyer profile'),
              ('Firm.Manage','Manage law firm'),('FirmUsers.Manage','Manage firm users'),('Analytics.View','View analytics'),
              ('Billing.View','View billing'),('Billing.Manage','Manage billing'),
              ('Directory.PriorityPlacement','Use clearly labelled enhanced placement')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO plan_entitlements (plan_id,permission_id)
            SELECT sp.id,p.id FROM subscription_plans sp CROSS JOIN permissions p
            WHERE sp.code IN ('personal_plus','lawyer_professional','firm_starter','firm_professional','firm_premium','institutional')
              AND p.code='Dashboard.Access' ON CONFLICT DO NOTHING;

            INSERT INTO plan_entitlements (plan_id,permission_id)
            SELECT sp.id,p.id FROM subscription_plans sp CROSS JOIN permissions p
            WHERE sp.code='personal_plus' AND p.code IN ('Cases.Create','Cases.ViewOwn','Documents.Analyse','Documents.Store','Referrals.Request','Billing.View')
            ON CONFLICT DO NOTHING;

            INSERT INTO plan_entitlements (plan_id,permission_id)
            SELECT sp.id,p.id FROM subscription_plans sp CROSS JOIN permissions p
            WHERE sp.code='lawyer_professional' AND p.code IN ('Cases.Manage','Documents.Analyse','Referrals.Manage','Leads.View','Leads.Manage','LawyerProfile.Manage','Analytics.View','Billing.View')
            ON CONFLICT DO NOTHING;

            INSERT INTO plan_entitlements (plan_id,permission_id)
            SELECT sp.id,p.id FROM subscription_plans sp CROSS JOIN permissions p
            WHERE sp.code IN ('firm_starter','firm_professional','firm_premium')
              AND p.code IN ('Cases.Manage','Documents.Analyse','Referrals.Manage','Leads.View','Leads.Manage','Firm.Manage','Analytics.View','Billing.View')
            ON CONFLICT DO NOTHING;

            INSERT INTO plan_entitlements (plan_id,permission_id)
            SELECT sp.id,p.id FROM subscription_plans sp CROSS JOIN permissions p
            WHERE sp.code IN ('firm_professional','firm_premium') AND p.code='FirmUsers.Manage' ON CONFLICT DO NOTHING;

            INSERT INTO plan_entitlements (plan_id,permission_id)
            SELECT sp.id,p.id FROM subscription_plans sp CROSS JOIN permissions p
            WHERE sp.code='firm_premium' AND p.code='Directory.PriorityPlacement' ON CONFLICT DO NOTHING;

            INSERT INTO role_permissions (role_id,permission_id)
            SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
            WHERE r.code='platform_admin'
            ON CONFLICT DO NOTHING;

            CREATE INDEX IF NOT EXISTS idx_subscriptions_user_status ON subscriptions(user_id,status);
            CREATE INDEX IF NOT EXISTS idx_subscriptions_org_status ON subscriptions(organisation_id,status);
            CREATE INDEX IF NOT EXISTS idx_usage_user_created ON usage_ledger(user_id,created_at DESC);
            CREATE INDEX IF NOT EXISTS idx_users_requested_plan ON app_users(requested_plan_code) WHERE requested_plan_code IS NOT NULL;
            CREATE INDEX IF NOT EXISTS idx_users_identity_verified ON app_users(identity_verified_at) WHERE identity_verified_at IS NOT NULL;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
