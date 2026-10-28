using KukaFm.Middleware.Api.Data;

namespace KukaFm.Middleware.Api.Tests.Data;

public class PagingTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Valid_ReturnsNull(int page, int pageSize) =>
        Assert.Null(Paging.Validate(page, pageSize));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PageBelowOne_IsInvalid(int page)
    {
        var errors = Paging.Validate(page, 100);

        Assert.NotNull(errors);
        Assert.Equal(["page"], errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void PageSizeOutsideOneTo100_IsInvalid(int pageSize)
    {
        var errors = Paging.Validate(1, pageSize);

        Assert.NotNull(errors);
        Assert.Equal(["pageSize"], errors.Keys);
    }

    [Fact]
    public void BothInvalid_ReportsBoth()
    {
        var errors = Paging.Validate(0, 0);

        Assert.NotNull(errors);
        Assert.Equal(["page", "pageSize"], errors.Keys.Order());
    }

    [Theory]
    [InlineData(1, 100, 0L)]
    [InlineData(3, 25, 50L)]
    [InlineData(int.MaxValue, 100, 214748364600L)]
    public void Skip_IsComputedWithoutOverflow(int page, int pageSize, long expected) =>
        Assert.Equal(expected, Paging.Skip(page, pageSize));
}
