using EfUi.Core.Binding;
using FluentAssertions;
using Xunit;

namespace EfUi.Core.Tests.Binding;

public class ScalarValueBinderTests
{
    [Theory]
    [InlineData(typeof(string), "Ada", "Ada")]
    [InlineData(typeof(int), "42", 42)]
    [InlineData(typeof(bool), "true", true)]
    public void Bind_returns_typed_scalar_values(Type targetType, string raw, object expected)
    {
        var sut = new ScalarValueBinder();

        var result = sut.Bind(targetType, raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void Bind_returns_failure_for_invalid_int()
    {
        var sut = new ScalarValueBinder();

        var result = sut.Bind(typeof(int), "abc");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("int");
    }
}
