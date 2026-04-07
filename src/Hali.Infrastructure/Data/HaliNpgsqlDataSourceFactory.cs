using Hali.Domain.Enums;
using Npgsql;
using Npgsql.NameTranslation;

namespace Hali.Infrastructure.Data;

/// <summary>
/// Builds an <see cref="NpgsqlDataSource"/> with all custom Hali PostgreSQL enums registered.
/// Npgsql requires explicit MapEnum registration on the data source — without this, any
/// DB write involving a custom enum column throws NotSupportedException at runtime.
/// </summary>
public static class HaliNpgsqlDataSourceFactory
{
    private static readonly NpgsqlSnakeCaseNameTranslator Snake = new();

    public static NpgsqlDataSource Build(string connectionString, bool useNetTopologySuite = false)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        MapEnums(builder);
        if (useNetTopologySuite)
        {
            builder.UseNetTopologySuite();
        }
        return builder.Build();
    }

    public static void MapEnums(NpgsqlDataSourceBuilder builder)
    {
        builder.MapEnum<AccountType>("account_type", Snake);
        builder.MapEnum<AuthMethod>("auth_method", Snake);
        builder.MapEnum<SignalState>("signal_state", Snake);
        builder.MapEnum<ParticipationType>("participation_type", Snake);
        builder.MapEnum<OfficialPostType>("official_post_type", Snake);
        builder.MapEnum<LocationPrecisionType>("location_precision_type", Snake);
        builder.MapEnum<CivicCategory>("civic_category", Snake);
    }
}
