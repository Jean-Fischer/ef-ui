using System.Data.Common;
using EfUi.Core.Metadata;
using EfUi.Core.Query;
using EfUi.Core.Rendering;
using EfUi.Core.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EfUi.Core.Tests.Query;

public sealed class EntityListQueryExecutorTests
{
    [Fact]
    public async Task Executes_typed_filter_sort_and_window_in_the_provider()
    {
        await using var interceptor = new RecordingCommandInterceptor();
        await using var db = await CreateDbAsync(interceptor);
        db.ProviderRecords.AddRange(
            Record(1, "match-late", 7, true, ProviderRole.Editor, new DateTime(2026, 1, 3)),
            Record(2, "other", 7, true, ProviderRole.Editor, new DateTime(2026, 1, 1)),
            Record(3, "match-early", 7, true, ProviderRole.Editor, new DateTime(2026, 1, 2)),
            Record(4, "match-disabled", 7, false, ProviderRole.Editor, new DateTime(2026, 1, 4)));
        await db.SaveChangesAsync();
        interceptor.Reset();

        var result = await ExecuteAsync(db, new TableQuery(
            [new("Name", "contains", "match"), new("IsActive", "eq", "true")],
            [new("CreatedAt", "asc")],
            Offset: 1,
            Limit: 1));

        result.Errors.Should().BeEmpty();
        result.Rows.Select(row => row.Key).Should().Equal("1");
        result.Rows[0].Cells["Name"].RawValue.Should().Be("match-late");
        result.Rows[0].Cells["Name"].DisplayText.Should().Be("match-late");
        interceptor.Commands.Should().ContainSingle();
        interceptor.Commands[0].Should().Contain("WHERE");
        interceptor.Commands[0].Should().MatchRegex("(?i)(LIMIT|OFFSET)");
    }

    [Fact]
    public async Task Executes_nullable_bool_enum_guid_and_datetime_equality_as_typed_values()
    {
        await using var db = await CreateDbAsync();
        var expectedId = Guid.NewGuid();
        db.ProviderRecords.AddRange(
            new ProviderRecord { Id = 1, Name = "typed", NullableNumber = 9, IsActive = true, Role = ProviderRole.Editor, ExternalId = expectedId, CreatedAt = new DateTime(2026, 2, 3) },
            new ProviderRecord { Id = 2, Name = "other", NullableNumber = null, IsActive = false, Role = ProviderRole.Viewer, ExternalId = Guid.NewGuid(), CreatedAt = new DateTime(2026, 2, 4) });
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(db, new TableQuery([
            new("NullableNumber", "eq", "9"),
            new("IsActive", "eq", "true"),
            new("Role", "eq", "Editor"),
            new("ExternalId", "eq", expectedId.ToString()),
            new("CreatedAt", "eq", "2026-02-03T00:00:00")
        ], [], Limit: 10));

        result.Errors.Should().BeEmpty();
        result.Rows.Select(row => row.Key).Should().Equal("1");
    }

    [Fact]
    public async Task Invalid_clauses_are_discarded_while_valid_clauses_execute()
    {
        await using var db = await CreateDbAsync();
        db.ProviderRecords.AddRange(Record(1, "keep", 1, true, ProviderRole.Viewer, new DateTime(2026, 1, 1)), Record(2, "drop", 2, true, ProviderRole.Viewer, new DateTime(2026, 1, 2)));
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(db, new TableQuery(
            [new("Name", "contains", "keep"), new("NoSuchField", "eq", "x")],
            [new("Id", "asc"), new("NoSuchField", "desc")]));

        result.AppliedFilters.Should().Equal(new TableFilterClause("Name", "contains", "keep"));
        result.AppliedSorts.Should().Equal(new TableSortClause("Id", "asc"));
        result.Errors.Should().ContainSingle(error => error.Code == "unsupported-filter-field");
        result.Rows.Select(row => row.Key).Should().Equal("1");
    }

    [Fact]
    public async Task Invalid_values_are_structured_errors_and_do_not_run_in_memory()
    {
        await using var interceptor = new RecordingCommandInterceptor();
        await using var db = await CreateDbAsync(interceptor);
        db.ProviderRecords.Add(Record(1, "value", 1, true, ProviderRole.Viewer, new DateTime(2026, 1, 1)));
        await db.SaveChangesAsync();
        interceptor.Reset();

        var result = await ExecuteAsync(db, new TableQuery([
            new("Id", "eq", "not-an-int"),
            new("Id", "contains", "1"),
            new("Name", "contains", "val")
        ], []));

        result.Errors.Should().Contain(error => error.Code == "invalid-filter-value" && error.Field == "Id");
        result.Errors.Should().Contain(error => error.Code == "unsupported-filter-operator" && error.Field == "Id");
        result.AppliedFilters.Should().ContainSingle().Which.Field.Should().Be("Name");
        result.Rows.Select(row => row.Key).Should().Equal("1");
        interceptor.Commands.Should().ContainSingle(command => command.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Defaults_to_primary_key_ascending_and_uses_primary_key_as_tie_break()
    {
        await using var db = await CreateDbAsync();
        db.ProviderRecords.AddRange(
            Record(3, "same", 1, true, ProviderRole.Viewer, new DateTime(2026, 1, 1)),
            Record(1, "same", 1, true, ProviderRole.Viewer, new DateTime(2026, 1, 1)),
            Record(2, "same", 1, true, ProviderRole.Viewer, new DateTime(2026, 1, 1)));
        await db.SaveChangesAsync();

        var defaultResult = await ExecuteAsync(db, new TableQuery([], [], Limit: 10));
        var sortedResult = await ExecuteAsync(db, new TableQuery([], [new("CreatedAt", "asc")], Limit: 10));

        defaultResult.Rows.Select(row => row.Key).Should().Equal("1", "2", "3");
        sortedResult.Rows.Select(row => row.Key).Should().Equal("1", "2", "3");
    }

    [Fact]
    public async Task Empty_windows_return_no_rows()
    {
        await using var db = await CreateDbAsync();
        db.ProviderRecords.Add(Record(1, "one", 1, true, ProviderRole.Viewer, new DateTime(2026, 1, 1)));
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(db, new TableQuery([], [], Offset: 5, Limit: 2));

        result.Rows.Should().BeEmpty();
        result.Offset.Should().Be(5);
        result.Limit.Should().Be(2);
    }

    [Fact]
    public async Task Cancellation_is_passed_to_provider_and_not_converted_to_validation_error()
    {
        await using var db = await CreateDbAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => ExecuteAsync(db, new TableQuery([], []), cancellation.Token);

        await FluentActions.Awaiting(action).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Unrelated_database_failures_are_not_returned_as_validation_errors()
    {
        await using var db = await CreateDbAsync();
        await db.DisposeAsync();

        var action = () => ExecuteAsync(db, new TableQuery([], []));

        await FluentActions.Awaiting(action).Should().ThrowAsync<ObjectDisposedException>();
    }

    private static ProviderRecord Record(int id, string name, int? nullableNumber, bool active, ProviderRole role, DateTime createdAt)
        => new() { Id = id, Name = name, NullableNumber = nullableNumber, IsActive = active, Role = role, ExternalId = Guid.NewGuid(), CreatedAt = createdAt };

    private static async Task<EntityListQueryResult> ExecuteAsync(SampleModelDbContext db, TableQuery query, CancellationToken cancellationToken = default)
    {
        var metadata = new EfEntityMetadataProvider().GetEntity(db, "provider_records");
        return await new EntityListQueryExecutor().ExecuteAsync(db, metadata, query, cancellationToken);
    }

    private static async Task<SampleModelDbContext> CreateDbAsync(RecordingCommandInterceptor? interceptor = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = new DbContextOptionsBuilder<SampleModelDbContext>().UseSqlite(connection);
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        var db = new SampleModelDbContext(builder.Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private sealed class RecordingCommandInterceptor : DbCommandInterceptor, IAsyncDisposable
    {
        private readonly List<string> _commands = [];
        public IReadOnlyList<string> Commands => _commands;
        public void Reset() => _commands.Clear();

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            _commands.Add(command.CommandText);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            _commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
