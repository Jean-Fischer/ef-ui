using EfUi.Core.Metadata;
using EfUi.Core.Query;
using EfUi.Core.Rendering;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfUi.Core.Tests.Query;

public sealed class EntityListQueryValidatorTests
{
    [Fact]
    public void Query_validator_implementation_types_are_not_public_package_api()
    {
        typeof(EntityListQueryCapabilities).IsPublic.Should().BeFalse();
        typeof(EntityListQueryFieldCapabilities).IsPublic.Should().BeFalse();
        typeof(EntityListQueryValidator).IsPublic.Should().BeFalse();
        typeof(EntityListQueryValidationResult).IsPublic.Should().BeFalse();
    }

    [Fact]
    public void Capability_and_validator_entry_points_guard_null_arguments()
    {
        using var db = CreateDb();
        var metadata = new EfEntityMetadataProvider().GetEntity(db, "orders");
        var query = new TableQuery([], []);
        var validator = new EntityListQueryValidator();

        FluentActions.Invoking(() => EntityListQueryCapabilities.Create((DbContext)null!, metadata))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("dbContext");
        FluentActions.Invoking(() => EntityListQueryCapabilities.Create((Microsoft.EntityFrameworkCore.Metadata.IModel)null!, metadata))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("model");
        FluentActions.Invoking(() => validator.Validate((DbContext)null!, metadata, query))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("dbContext");
        FluentActions.Invoking(() => validator.Validate((Microsoft.EntityFrameworkCore.Metadata.IModel)null!, metadata, query))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("model");
    }

    [Fact]
    public void Validation_result_guards_null_collections()
    {
        FluentActions.Invoking(() => new EntityListQueryValidationResult(null!, [], []))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("appliedFilters");
        FluentActions.Invoking(() => new EntityListQueryValidationResult([], null!, []))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("appliedSorts");
        FluentActions.Invoking(() => new EntityListQueryValidationResult([], [], null!))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("errors");
    }

    [Fact]
    public void Visible_scalar_properties_are_queryable_and_eq_is_available_for_typed_values()
    {
        using var db = CreateDb();
        var metadata = new EfEntityMetadataProvider().GetEntity(db, "orders");
        var capabilities = EntityListQueryCapabilities.Create(db, metadata);

        capabilities.Fields["Id"].IsFilterable.Should().BeTrue();
        capabilities.Fields["Id"].SupportedOperators.Should().Contain("eq");
        capabilities.Fields["Name"].SupportedOperators.Should().Contain("eq");
        capabilities.Fields["Name"].SupportedOperators.Should().Contain("contains");
    }

    [Fact]
    public void Unsupported_fields_are_rejected_while_valid_clauses_survive()
    {
        using var db = CreateDb();
        var metadata = new EfEntityMetadataProvider().GetEntity(db, "orders");
        var query = new TableQuery(
            [new("Name", "contains", "book"), new("Hidden", "eq", "1")],
            [new("Name", "asc"), new("Hidden", "desc")]);

        var result = new EntityListQueryValidator().Validate(db, metadata, query);

        result.AppliedFilters.Should().ContainSingle().Which.Should().Be(query.Filters[0]);
        result.AppliedSorts.Should().ContainSingle().Which.Should().Be(query.Sorts[0]);
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().OnlyContain(error => error.Field == "Hidden");
    }

    [Fact]
    public void Mapped_fk_display_string_supports_contains_and_eq()
    {
        using var db = CreateDb();
        var metadata = new EfEntityMetadataProvider().GetEntity(db, "orders");
        var field = EntityListQueryCapabilities.Create(db, metadata).Fields["CustomerId"];

        field.IsFilterable.Should().BeTrue();
        field.IsSortable.Should().BeTrue();
        field.SupportedOperators.Should().Contain(["contains", "eq"]);
    }

    [Fact]
    public void IModel_overloads_validate_a_valid_mapped_fk_display_filter_and_sort()
    {
        using var db = CreateDb();
        var metadata = new EfEntityMetadataProvider().GetEntity(db, "orders");
        var query = new TableQuery([new("CustomerId", "contains", "book")], [new("CustomerId", "asc")]);

        var capabilities = EntityListQueryCapabilities.Create(db.Model, metadata);
        var result = new EntityListQueryValidator().Validate(db.Model, metadata, query);

        capabilities.Fields["CustomerId"].IsFilterable.Should().BeTrue();
        capabilities.Fields["CustomerId"].IsSortable.Should().BeTrue();
        result.AppliedFilters.Should().Equal(query.Filters);
        result.AppliedSorts.Should().Equal(query.Sorts);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Unsupported_operators_produce_structured_errors()
    {
        using var db = CreateDb();
        var metadata = new EfEntityMetadataProvider().GetEntity(db, "orders");

        var result = new EntityListQueryValidator().Validate(
            db,
            metadata,
            new TableQuery([new("Name", "startsWith", "book")], [new("Name", "sideways")]));

        result.AppliedFilters.Should().BeEmpty();
        result.AppliedSorts.Should().BeEmpty();
        result.Errors.Should().Contain(error => error.Code == "unsupported-filter-operator" && error.Field == "Name");
        result.Errors.Should().Contain(error => error.Code == "unsupported-sort-direction" && error.Field == "Name");
    }

    [Fact]
    public void Computed_display_property_is_renderable_but_not_queryable()
    {
        using var db = CreateDb();
        var metadata = new EfEntityMetadataProvider().GetEntity(db, "computed_orders");
        var capabilities = EntityListQueryCapabilities.Create(db, metadata);

        capabilities.Fields["CustomerId"].IsFilterable.Should().BeFalse();
        capabilities.Fields["CustomerId"].IsSortable.Should().BeFalse();
        capabilities.Fields["CustomerId"].IsDisplayOnly.Should().BeTrue();

        var result = new EntityListQueryValidator().Validate(
            db,
            metadata,
            new TableQuery([new("CustomerId", "contains", "book")], [new("CustomerId", "asc")]));

        result.AppliedFilters.Should().BeEmpty();
        result.AppliedSorts.Should().BeEmpty();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Equal(
            new EntityListQueryError("field-display-only", "Field 'CustomerId' is display-only and cannot be filtered by the provider.", "CustomerId"),
            new EntityListQueryError("field-display-only", "Field 'CustomerId' is display-only and cannot be sorted by the provider.", "CustomerId"));
    }

    [Fact]
    public void Duplicate_filters_and_sorts_preserve_best_effort_ordering_without_adding_primary_key_sort()
    {
        using var db = CreateDb();
        var metadata = new EfEntityMetadataProvider().GetEntity(db, "orders");
        var query = new TableQuery(
            [new("Name", "eq", "one"), new("Name", "eq", "two")],
            [new("Name", "desc"), new("Name", "asc")]);

        var result = new EntityListQueryValidator().Validate(db, metadata, query);

        result.AppliedFilters.Should().Equal(query.Filters);
        result.AppliedSorts.Should().Equal(query.Sorts);
    }

    private static QueryDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<QueryDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new QueryDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class QueryDbContext(DbContextOptions<QueryDbContext> options) : DbContext(options)
    {
        public DbSet<QueryOrder> Orders => Set<QueryOrder>();
        public DbSet<ComputedOrder> ComputedOrders => Set<ComputedOrder>();
        public DbSet<QueryCustomer> Customers => Set<QueryCustomer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<QueryOrder>(builder =>
            {
                builder.ToTable("orders");
                builder.HasKey(order => order.Id);
                builder.HasOne(order => order.Customer).WithMany().HasForeignKey(order => order.CustomerId);
            });
            modelBuilder.Entity<ComputedOrder>(builder =>
            {
                builder.ToTable("computed_orders");
                builder.HasKey(order => order.Id);
                builder.HasOne(order => order.Customer).WithMany().HasForeignKey(order => order.CustomerId);
            });
            modelBuilder.Entity<QueryCustomer>(builder => builder.HasKey(customer => customer.Id));
        }
    }

    private sealed class QueryOrder
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        [EfUiDisplayColumn(nameof(QueryCustomer.Name))]
        public QueryCustomer Customer { get; set; } = null!;
    }

    private sealed class ComputedOrder
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public QueryCustomer Customer { get; set; } = null!;
    }

    [EfUiDisplayColumn(nameof(QueryCustomer.ComputedLabel))]
    private sealed class QueryCustomer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string ComputedLabel => Name;
    }
}
