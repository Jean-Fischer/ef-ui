using EfUi.Core.Binding;
using FluentAssertions;
using Xunit;

namespace EfUi.Core.Tests.Binding;

public class ScalarValueBinderTests
{
    private enum SampleStatus
    {
        Draft,
        Published
    }

    private sealed class UnsupportedType;

    [Theory]
    [InlineData(typeof(string), "Ada", "Ada")]
    [InlineData(typeof(int), "42", 42)]
    [InlineData(typeof(bool), "true", true)]
    [InlineData(typeof(decimal), "12.5", 12.5)]
    public void Bind_returns_typed_scalar_values(Type targetType, string raw, object expected)
    {
        var sut = new ScalarValueBinder();

        var result = sut.Bind(targetType, raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void Bind_returns_datetime_value()
    {
        var sut = new ScalarValueBinder();
        var expected = new DateTime(2026, 5, 17, 10, 30, 0);

        var result = sut.Bind(typeof(DateTime), "2026-05-17T10:30:00");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void Bind_returns_null_for_blank_nullable_int()
    {
        var sut = new ScalarValueBinder();

        var result = sut.Bind(typeof(int?), string.Empty);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Bind_returns_guid_value()
    {
        var sut = new ScalarValueBinder();
        var expected = Guid.Parse("f81d4fae-7dec-11d0-a765-00a0c91e6bf6");

        var result = sut.Bind(typeof(Guid), "f81d4fae-7dec-11d0-a765-00a0c91e6bf6");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void Bind_returns_enum_value()
    {
        var sut = new ScalarValueBinder();

        var result = sut.Bind(typeof(SampleStatus), "published");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(SampleStatus.Published);
    }

    [Fact]
    public void Bind_returns_failure_for_invalid_int()
    {
        var sut = new ScalarValueBinder();

        var result = sut.Bind(typeof(int), "abc");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("int");
    }

    [Fact]
    public void Bind_returns_failure_for_unsupported_type()
    {
        var sut = new ScalarValueBinder();

        var result = sut.Bind(typeof(UnsupportedType), "value");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Type UnsupportedType is not supported.");
    }
}
