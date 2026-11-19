using DidarCrm.Application.Common.Results;

namespace DidarCrm.UnitTests.Application.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_its_value()
    {
        var result = Result<string>.Success("activity-created");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("activity-created", result.Value);
    }

    [Fact]
    public void Failure_exposes_its_error()
    {
        var error = new Error("Contact.NotFound", "Contact was not found.", ErrorType.NotFound);

        var result = Result<string>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Success_does_not_expose_an_error()
    {
        var result = Result<string>.Success("activity-created");

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_does_not_expose_a_value()
    {
        var result = Result<string>.Failure(
            new Error("Contact.NotFound", "Contact was not found.", ErrorType.NotFound));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
