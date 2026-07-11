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

public sealed class RelatedLabelEnricherTests
{
    [Fact]
    public async Task Applies_related_labels_and_queries_only_keys_in_the_returned_page()
    {
        var interceptor = new CommandRecorder();
        await using var db = await CreateDbAsync(interceptor);
        db.Groups.AddRange(
            Enumerable.Range(1, 100).Select(id => new Group { Id = id, Name = $"Group {id}" }));
        db.Users.AddRange(
            new User { Id = 1, Name = "first", GroupId = 1 },
            new User { Id = 2, Name = "second", GroupId = 2 },
            new User { Id = 3, Name = "third", GroupId = 3 });
        await db.SaveChangesAsync();
        interceptor.Reset();

        var metadata = new EfEntityMetadataProvider().GetEntity(db, "users");
        var result = await new EntityListQueryExecutor().ExecuteAsync(
            db,
            metadata,
            new TableQuery([], [], Offset: 1, Limit: 1));

        result.Errors.Should().BeEmpty();
        result.Rows.Should().ContainSingle();
        result.Rows[0].Cells["GroupId"].RawValue.Should().Be("2");
        result.Rows[0].Cells["GroupId"].DisplayText.Should().Be("Group 2");
        interceptor.Commands.Should().HaveCount(2);
        var relatedCommand = interceptor.Commands.Single(command => command.CommandText.Contains("FROM \"Groups\"", StringComparison.OrdinalIgnoreCase));
        relatedCommand.CommandText.Should().Contain("WHERE");
        var relatedQueryText = string.Join(
            Environment.NewLine,
            new[] { relatedCommand.CommandText }
                .Concat(relatedCommand.Parameters.Select(parameter => parameter.Value?.ToString() ?? string.Empty)));
        relatedQueryText.Should().MatchRegex(@"(?<!\d)2(?!\d)");
        relatedQueryText.Should().NotMatchRegex(@"(?<!\d)[13](?!\d)");
    }

    [Fact]
    public async Task Scalar_foreign_key_only_relationships_support_provider_label_queries()
    {
        await using var db = await CreateDbAsync();
        db.ScalarGroups.AddRange(
            new ScalarGroup { Id = 1, Name = "Beta" },
            new ScalarGroup { Id = 2, Name = "Alpha" },
            new ScalarGroup { Id = 3, Name = "Zulu" });
        db.ScalarUsers.AddRange(
            new ScalarUser { Id = 1, GroupId = 1 },
            new ScalarUser { Id = 2, GroupId = 2 },
            new ScalarUser { Id = 3, GroupId = 3 },
            new ScalarUser { Id = 4 });
        await db.SaveChangesAsync();

        var metadata = new EfEntityMetadataProvider().GetEntities(db).Single(entity => entity.ClrType == typeof(ScalarUser));
        var result = await new EntityListQueryExecutor().ExecuteAsync(
            db,
            metadata,
            new TableQuery(
                [new TableFilterClause("GroupId", "contains", "a")],
                [new TableSortClause("GroupId", "asc")],
                Offset: 1,
                Limit: 1));

        result.Errors.Should().BeEmpty();
        result.Rows.Select(row => row.Key).Should().Equal("1");
        result.Rows.Select(row => row.Cells["GroupId"].DisplayText).Should().Equal("Beta");
    }

    [Fact]
    public async Task Null_scalar_foreign_keys_do_not_throw_or_match_related_labels()
    {
        await using var db = await CreateDbAsync();
        db.ScalarGroups.Add(new ScalarGroup { Id = 1, Name = "Alpha" });
        db.ScalarUsers.AddRange(
            new ScalarUser { Id = 1, GroupId = 1 },
            new ScalarUser { Id = 2 });
        await db.SaveChangesAsync();

        var metadata = new EfEntityMetadataProvider().GetEntities(db).Single(entity => entity.ClrType == typeof(ScalarUser));
        var result = await new EntityListQueryExecutor().ExecuteAsync(
            db,
            metadata,
            new TableQuery([new TableFilterClause("GroupId", "contains", "a")], []));

        result.Errors.Should().BeEmpty();
        result.Rows.Select(row => row.Key).Should().Equal("1");
        result.Rows[0].Cells["GroupId"].DisplayText.Should().Be("Alpha");
    }

    [Fact]
    public async Task Two_foreign_keys_to_the_same_principal_use_each_navigation_display_override()
    {
        await using var db = await CreateDbAsync();
        db.Groups.AddRange(
            new Group { Id = 1, Name = "Primary name", Code = "P1" },
            new Group { Id = 2, Name = "Secondary name", Code = "S2" });
        db.MultiReferenceUsers.Add(new MultiReferenceUser
        {
            Id = 1,
            PrimaryGroupId = 1,
            SecondaryGroupId = 2
        });
        await db.SaveChangesAsync();

        var metadata = new EfEntityMetadataProvider().GetEntity(db, "multireferenceusers");
        var result = await new EntityListQueryExecutor().ExecuteAsync(
            db, metadata, new TableQuery([], [], Limit: 1));

        result.Errors.Should().BeEmpty();
        result.Rows.Should().ContainSingle();
        result.Rows[0].Cells["PrimaryGroupId"].DisplayText.Should().Be("Primary name");
        result.Rows[0].Cells["SecondaryGroupId"].DisplayText.Should().Be("S2");
    }

    [Fact]
    public async Task Shadow_principal_keys_return_structured_related_query_errors()
    {
        await using var db = await CreateDbAsync();
        db.ShadowKeyGroups.Add(new ShadowKeyGroup { Name = "shadow" });
        db.ShadowKeyUsers.Add(new ShadowKeyUser { Id = 1, ShadowGroupId = 1 });
        await db.SaveChangesAsync();

        var metadata = new EfEntityMetadataProvider().GetEntity(db, "shadowkeyusers");
        var result = await new EntityListQueryExecutor().ExecuteAsync(
            db,
            metadata,
            new TableQuery([new TableFilterClause("ShadowGroupId", "contains", "shadow")], []));

        result.Rows.Should().BeEmpty();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().OnlyContain(error =>
            error.Code == "unsupported-related-query-field" && error.Field == "ShadowGroupId");
    }

    [Fact]
    public async Task Missing_and_null_related_keys_use_raw_and_empty_fallbacks()
    {
        await using var db = await CreateDbAsync();
        db.Groups.Add(new Group { Id = 1, Name = "Known" });
        db.Users.AddRange(
            new User { Id = 1, Name = "missing", GroupId = 1 },
            new User { Id = 2, Name = "null" });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Groups WHERE Id = 1");
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");

        var metadata = new EfEntityMetadataProvider().GetEntity(db, "users");
        var result = await new EntityListQueryExecutor().ExecuteAsync(
            db, metadata, new TableQuery([], [], Limit: 10));

        result.Errors.Should().BeEmpty();
        result.Rows.Select(row => row.Cells["GroupId"].DisplayText).Should().Equal("1", "");
    }

    [Fact]
    public async Task Related_label_filter_and_sort_are_applied_before_entity_windowing()
    {
        await using var db = await CreateDbAsync();
        db.Groups.AddRange(
            new Group { Id = 1, Name = "Beta" },
            new Group { Id = 2, Name = "Gamma" },
            new Group { Id = 3, Name = "Alpha" });
        db.Users.AddRange(
            new User { Id = 1, Name = "beta", GroupId = 1 },
            new User { Id = 2, Name = "gamma", GroupId = 2 },
            new User { Id = 3, Name = "alpha", GroupId = 3 });
        await db.SaveChangesAsync();

        var metadata = new EfEntityMetadataProvider().GetEntity(db, "users");
        var result = await new EntityListQueryExecutor().ExecuteAsync(
            db,
            metadata,
            new TableQuery(
                [new TableFilterClause("GroupId", "contains", "a")],
                [new TableSortClause("GroupId", "asc")],
                Offset: 1,
                Limit: 1));

        result.Errors.Should().BeEmpty();
        result.Rows.Select(row => row.Key).Should().Equal("1");
        result.Rows[0].Cells["GroupId"].DisplayText.Should().Be("Beta");
    }

    [Fact]
    public async Task List_reads_do_not_start_an_explicit_transaction()
    {
        var recorder = new CommandRecorder();
        await using var db = await CreateDbAsync(recorder);
        db.Groups.Add(new Group { Id = 1, Name = "Group" });
        db.Users.Add(new User { Id = 1, Name = "user", GroupId = 1 });
        await db.SaveChangesAsync();
        recorder.Reset();

        var metadata = new EfEntityMetadataProvider().GetEntity(db, "users");
        var result = await new EntityListQueryExecutor().ExecuteAsync(
            db, metadata, new TableQuery([], [], Limit: 1));

        result.Errors.Should().BeEmpty();
        db.Database.CurrentTransaction.Should().BeNull();
    }

    [Fact]
    public async Task Computed_related_display_labels_render_but_are_display_only_for_queries()
    {
        await using var db = await CreateDbAsync();
        db.Groups.Add(new Group { Id = 1, Name = "Group" });
        db.Users.Add(new User { Id = 1, Name = "user", GroupId = 1 });
        await db.SaveChangesAsync();

        var discovered = new EfEntityMetadataProvider().GetEntity(db, "users");
        var properties = discovered.AllProperties
            .Select(property => property.Name == "GroupId"
                ? property with { RelatedDisplayPropertyName = "ComputedLabel" }
                : property)
            .ToList();
        var metadata = new EntityMetadata(
            discovered.DisplayName,
            discovered.RouteName,
            discovered.ClrType,
            discovered.PrimaryKeyProperty,
            properties,
            discovered.EditableProperties,
            discovered.CreateEditableFields,
            discovered.UpdateEditableFields,
            discovered.RelatedManagementLinks);

        var rendered = await new EntityListQueryExecutor().ExecuteAsync(
            db, metadata, new TableQuery([], [], Limit: 1));
        var queried = await new EntityListQueryExecutor().ExecuteAsync(
            db, metadata, new TableQuery([new TableFilterClause("GroupId", "contains", "Group")], []));

        rendered.Errors.Should().BeEmpty();
        rendered.Rows[0].Cells["GroupId"].DisplayText.Should().Be("Group (computed)");
        queried.Rows.Should().ContainSingle();
        queried.Errors.Should().ContainSingle(error => error.Code == "field-display-only");
    }

    private static async Task<SampleModelDbContext> CreateDbAsync(DbCommandInterceptor? recorder = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = new DbContextOptionsBuilder<SampleModelDbContext>().UseSqlite(connection);
        if (recorder is not null)
        {
            builder.AddInterceptors(recorder);
        }

        var db = new SampleModelDbContext(builder.Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task Cancellation_token_is_passed_to_related_label_materialization()
    {
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancellationRecorder(cancellation.Token);
        await using var db = await CreateDbAsync(interceptor);
        db.Groups.Add(new Group { Id = 1, Name = "Group" });
        db.Users.Add(new User { Id = 1, Name = "user", GroupId = 1 });
        await db.SaveChangesAsync();
        var metadata = new EfEntityMetadataProvider().GetEntity(db, "users");
        var action = () => new EntityListQueryExecutor().ExecuteAsync(
            db, metadata, new TableQuery([], [], Limit: 1), cancellation.Token);

        await FluentActions.Awaiting(action).Should().ThrowAsync<OperationCanceledException>();
        interceptor.RelatedCancellationToken.Should().Be(cancellation.Token);
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        private readonly List<RecordedCommand> _commands = [];

        public IReadOnlyList<RecordedCommand> Commands => _commands;

        public void Reset() => _commands.Clear();

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Capture(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        private void Capture(DbCommand command)
        {
            _commands.Add(new RecordedCommand(
                command.CommandText,
                command.Parameters.Cast<DbParameter>()
                    .Select(parameter => new CapturedParameter(parameter.Value))
                    .ToList()));
        }
    }

    private sealed class CancellationRecorder(CancellationToken expectedToken) : DbCommandInterceptor
    {
        public CancellationToken? RelatedCancellationToken { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("FROM \"Groups\"", StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult(result);
            }

            RelatedCancellationToken = cancellationToken;
            throw new OperationCanceledException(expectedToken);
        }
    }

    private sealed record RecordedCommand(string CommandText, IReadOnlyList<CapturedParameter> Parameters);
    private sealed record CapturedParameter(object? Value);
}
