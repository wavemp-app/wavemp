using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace WaveMP.Data;

/// <summary>
/// Keeps the standard names of the migrations history table (<c>__EFMigrationsHistory</c> with <c>MigrationId</c>
/// and <c>ProductVersion</c>). The snake_case naming convention would otherwise rename its columns as well, and
/// databases created before the convention was introduced could no longer be migrated. Customizing the history
/// table by deriving from the provider's history repository is the approach documented by EF Core.
/// </summary>
/// <remarks>
/// The provider does not check whether the history table exists: it queries the table and catches the error of a
/// missing one. EF Core logs that failed query as an error before the provider catches it, so the first start of a new
/// installation would log errors although nothing is wrong. This repository checks first, as EF Core does for the other
/// providers, so an empty database yields no applied migrations without a failed query. The provider dropped its former
/// check because it looked in the schema <c>public</c> only; <c>to_regclass</c> resolves the table name through the
/// <c>search_path</c> like the query itself.
/// </remarks>
#pragma warning disable EF1001 // The provider's history repository is internal API, as in the EF Core documentation.
internal sealed class WaveMpHistoryRepository(HistoryRepositoryDependencies dependencies)
    : NpgsqlHistoryRepository(dependencies)
{
    protected override string ExistsSql
    {
        get
        {
            string table = SqlGenerationHelper.DelimitIdentifier(TableName, TableSchema);
            RelationalTypeMapping text = Dependencies.TypeMappingSource.GetMapping(typeof(string));
            return $"SELECT to_regclass({text.GenerateSqlLiteral(table)}) IS NOT NULL";
        }
    }

    /// <summary>
    /// Whether the database and the history table in it exist; asked by EF Core before it reads the applied
    /// migrations.
    /// </summary>
    public override bool Exists()
    {
        return Dependencies.DatabaseCreator.Exists()
            && InterpretExistsResult(Dependencies.RawSqlCommandBuilder
                .Build(ExistsSql)
                .ExecuteScalar(CreateCommandParameters()));
    }

    /// <inheritdoc cref="Exists"/>
    public override async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        return await Dependencies.DatabaseCreator.ExistsAsync(cancellationToken)
            && InterpretExistsResult(await Dependencies.RawSqlCommandBuilder
                .Build(ExistsSql)
                .ExecuteScalarAsync(CreateCommandParameters(), cancellationToken));
    }

    protected override void ConfigureTable(EntityTypeBuilder<HistoryRow> history)
    {
        base.ConfigureTable(history);
        history.HasKey(row => row.MigrationId).HasName("PK___EFMigrationsHistory");
        history.Property(row => row.MigrationId).HasColumnName(nameof(HistoryRow.MigrationId));
        history.Property(row => row.ProductVersion).HasColumnName(nameof(HistoryRow.ProductVersion));
    }

    private RelationalCommandParameterObject CreateCommandParameters()
    {
        return new RelationalCommandParameterObject(
            Dependencies.Connection,
            null,
            null,
            Dependencies.CurrentContext.Context,
            Dependencies.CommandLogger,
            CommandSource.Migrations);
    }
}
#pragma warning restore EF1001
