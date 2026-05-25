using Toykhan.Library.SapB1.ServiceLayer;

namespace Toykhan.Library.SapB1.ServiceLayer.Tests;

public sealed class SapODataQueryTests
{
    [Fact]
    public void New_ReturnsEmptyQuery()
    {
        var q = SapODataQuery.New();

        Assert.Null(q.Filter);
        Assert.Null(q.Select);
        Assert.Null(q.OrderBy);
        Assert.Null(q.Top);
        Assert.Null(q.Skip);
        Assert.Null(q.Expand);
        Assert.Null(q.Apply);
        Assert.Null(q.PageSize);
        Assert.False(q.CaseInsensitive);
        Assert.False(q.InlineCount);
    }

    [Fact]
    public void FluentChaining_SetsAllProperties()
    {
        var q = SapODataQuery.New()
            .WithFilter("DocDate gt '2025-01-01'")
            .WithSelect("DocEntry,DocDate")
            .WithOrderBy("DocEntry desc")
            .WithTop(50)
            .WithSkip(100)
            .WithExpand("DocumentLines")
            .WithApply("groupby((CardCode))")
            .WithPageSize(25)
            .AsCaseInsensitive()
            .WithInlineCount();

        Assert.Equal("DocDate gt '2025-01-01'", q.Filter);
        Assert.Equal("DocEntry,DocDate", q.Select);
        Assert.Equal("DocEntry desc", q.OrderBy);
        Assert.Equal(50, q.Top);
        Assert.Equal(100, q.Skip);
        Assert.Equal("DocumentLines", q.Expand);
        Assert.Equal("groupby((CardCode))", q.Apply);
        Assert.Equal(25, q.PageSize);
        Assert.True(q.CaseInsensitive);
        Assert.True(q.InlineCount);
    }

    [Fact]
    public void FluentMethods_ReturnSameInstance()
    {
        var q = SapODataQuery.New();
        var returned = q.WithFilter("test");

        Assert.Same(q, returned);
    }
}
