using DidarCrm.Domain.Activities;
using DidarCrm.Domain.Common;

namespace DidarCrm.UnitTests.Domain.Activities;

public sealed class ActivityInputValueObjectTests
{
    [Fact]
    public void Request_key_preserves_its_value()
    {
        const string value = "request-123";

        var requestKey = RequestKey.Create(value);

        Assert.Equal(value, requestKey.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_key_rejects_missing_values(string? value)
    {
        Assert.Throws<DomainValidationException>(() => RequestKey.Create(value));
    }

    [Fact]
    public void Request_key_rejects_values_over_maximum_length()
    {
        var value = new string('a', RequestKey.MaxLength + 1);

        Assert.Throws<DomainValidationException>(() => RequestKey.Create(value));
    }

    [Fact]
    public void Activity_title_trims_its_value()
    {
        var title = ActivityTitle.Create("  Follow up with customer  ");

        Assert.Equal("Follow up with customer", title.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Activity_title_rejects_missing_values(string? value)
    {
        Assert.Throws<DomainValidationException>(() => ActivityTitle.Create(value));
    }

    [Fact]
    public void Activity_title_validates_length_after_trimming()
    {
        var acceptedValue = $"  {new string('a', ActivityTitle.MaxLength)}  ";
        var rejectedValue = new string('a', ActivityTitle.MaxLength + 1);

        var title = ActivityTitle.Create(acceptedValue);

        Assert.Equal(ActivityTitle.MaxLength, title.Value.Length);
        Assert.Throws<DomainValidationException>(() => ActivityTitle.Create(rejectedValue));
    }
}
