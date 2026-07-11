using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Orchestration;
using EfUi.Core.Rendering;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using EfUi.Core.Tests.TestDoubles;
using Xunit;

namespace EfUi.Core.Tests.Orchestration;

public sealed class EfUiFlowOrchestratorTests
{
    [Fact]
    public void RenderIndexPage_uses_the_injected_rendering_adapter()
    {
        using var db = CreateDb();
        var discovery = new EntityDiscoveryResult(Array.Empty<EntityMetadata>(), Array.Empty<EntityDiscoveryIssue>());
        var renderer = new RecordingRenderer();
        var sut = new EfUiFlowOrchestrator(
            new StubMetadataProvider(discovery),
            new StubCrudService(),
            renderer);

        var result = sut.RenderIndexPage("/efui", discovery);

        result.Should().Be("index");
        renderer.IndexCalls.Should().ContainSingle();
    }

    [Fact]
    public void RenderErrorPage_uses_the_injected_rendering_adapter()
    {
        var discovery = new EntityDiscoveryResult([], []);
        var renderer = new RecordingRenderer();
        var sut = new EfUiFlowOrchestrator(
            new StubMetadataProvider(discovery),
            new StubCrudService(),
            renderer);

        var result = sut.RenderErrorPage("/efui", "Users", ["Unsupported entity"]);

        result.Should().Be("error");
        renderer.ErrorCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task BuildRenderedListView_can_prepare_an_unwindowed_delete_response()
    {
        await using var db = await CreateDbAsync();
        db.Users.AddRange(
            new User { Name = "Ada", Email = "ada@example.com" },
            new User { Name = "Grace", Email = "grace@example.com" });
        await db.SaveChangesAsync();

        var metadata = new EntityMetadata(
            "User",
            "users",
            typeof(User),
            Property("Id", typeof(int), isPrimaryKey: true),
            [Property("Id", typeof(int), isPrimaryKey: true), Property("Name", typeof(string), isEditableOnCreate: true, isEditableOnUpdate: true)],
            [Property("Name", typeof(string), isEditableOnUpdate: true)]);
        var sut = new EfUiFlowOrchestrator();

        var view = await sut.BuildRenderedListViewAsync("/efui", db, metadata, new TableQuery(Limit: 1), includeAllRows: true);

        view.Rows.Should().HaveCount(2);
        view.Limit.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_uses_the_injected_crud_adapter()
    {
        await using var db = await CreateDbAsync();
        var crud = new StubCrudService();
        var sut = new EfUiFlowOrchestrator(
            new StubMetadataProvider(new EntityDiscoveryResult([], [])),
            crud,
            new RecordingRenderer());

        var values = new Dictionary<string, string?> { ["Name"] = "Ada" };
        var result = await sut.CreateAsync(db, "users", values);

        result.IsSuccess.Should().BeTrue();
        crud.CreatedRoute.Should().Be("users");
        crud.CreatedValues.Should().BeEquivalentTo(values);
    }

    private static EntityPropertyMetadata Property(
        string name,
        Type clrType,
        bool isEditableOnCreate = false,
        bool isEditableOnUpdate = false,
        bool isPrimaryKey = false)
        => new(name, clrType, isEditableOnCreate, isEditableOnUpdate, isPrimaryKey);

    private static SampleModelDbContext CreateDb()
        => new(new DbContextOptionsBuilder<SampleModelDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options);

    private static async Task<SampleModelDbContext> CreateDbAsync()
    {
        var db = CreateDb();
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private sealed class StubMetadataProvider(EntityDiscoveryResult discovery) : IEntityMetadataProvider
    {
        public EntityDiscoveryResult GetDiscoveryResult(DbContext dbContext) => discovery;
        public IReadOnlyList<EntityMetadata> GetEntities(DbContext dbContext) => discovery.Entities;
        public EntityMetadata GetEntity(DbContext dbContext, string routeName) => discovery.Entities.Single(x => x.RouteName == routeName);
    }

    private sealed class StubCrudService : IEntityCrudService
    {
        public string? CreatedRoute { get; private set; }
        public IReadOnlyDictionary<string, string?>? CreatedValues { get; private set; }

        public Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string?> values)
        {
            CreatedRoute = entityRoute;
            CreatedValues = values;
            return Task.FromResult(CrudOperationResult.Success());
        }

        public Task<CrudOperationResult> CreateAsync(DbContext dbContext, string entityRoute, IReadOnlyDictionary<string, string[]> values)
            => Task.FromResult(CrudOperationResult.Success());

        public Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string?> values)
            => Task.FromResult(CrudOperationResult.Success());

        public Task<CrudOperationResult> UpdateAsync(DbContext dbContext, string entityRoute, object key, IReadOnlyDictionary<string, string[]> values)
            => Task.FromResult(CrudOperationResult.Success());

        public Task<CrudOperationResult> DeleteAsync(DbContext dbContext, string entityRoute, object key)
            => Task.FromResult(CrudOperationResult.Success());
    }

    private sealed class RecordingRenderer : IHtmlPageRenderer
    {
        public List<string> IndexCalls { get; } = [];
        public List<string> ErrorCalls { get; } = [];

        public string RenderIndex(string routePrefix, IReadOnlyList<EntityMetadata> entities, IReadOnlyList<string>? warnings = null, IReadOnlyList<string>? errors = null)
        {
            IndexCalls.Add(routePrefix);
            return "index";
        }

        public string RenderList(string routePrefix, EntityMetadata entity, RenderedListView view, bool showActions = true, string? antiForgeryToken = null) => "list";
        public string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, IReadOnlyDictionary<string, string[]>? submittedValues = null, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions = null, string? antiForgeryToken = null) => "form";
        public string RenderEditForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, object? key, IReadOnlyDictionary<string, string[]>? submittedValues = null, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions = null, string? antiForgeryToken = null) => "form";
        public string RenderErrorPage(string routePrefix, string title, IReadOnlyList<string> messages)
        {
            ErrorCalls.Add(title);
            return "error";
        }
    }
}
