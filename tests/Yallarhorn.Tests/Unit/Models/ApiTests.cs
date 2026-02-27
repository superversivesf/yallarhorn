namespace Yallarhorn.Tests.Unit.Models;

using FluentAssertions;
using System.Text.Json;
using Xunit;
using Yallarhorn.Models.Api;

public class LinkTests
{
    [Fact]
    public void Constructor_Default_SetsDefaultValues()
    {
        // Arrange & Act
        var link = new Link
        {
            Href = "/api/test"
        };

        // Assert
        link.Href.Should().Be("/api/test");
        link.Rel.Should().BeNull();
        link.Method.Should().Be("GET");
    }

    [Fact]
    public void Constructor_WithAllProperties_SetsProperties()
    {
        // Arrange & Act
        var link = new Link
        {
            Href = "/api/channels/123",
            Rel = "self",
            Method = "DELETE"
        };

        // Assert
        link.Href.Should().Be("/api/channels/123");
        link.Rel.Should().Be("self");
        link.Method.Should().Be("DELETE");
    }

    [Fact]
    public void Serialization_ProducesExpectedJson()
    {
        // Arrange
        var link = new Link
        {
            Href = "/api/channels",
            Rel = "next",
            Method = "GET"
        };

        // Act
        var json = JsonSerializer.Serialize(link);

        // Assert
        json.Should().Contain("\"href\":\"/api/channels\"");
        json.Should().Contain("\"rel\":\"next\"");
        json.Should().Contain("\"method\":\"GET\"");
    }

    [Fact]
    public void Deserialization_ProducesExpectedObject()
    {
        // Arrange
        var json = """{"href":"/api/test","rel":"prev","method":"POST"}""";

        // Act
        var link = JsonSerializer.Deserialize<Link>(json);

        // Assert
        link.Should().NotBeNull();
        link!.Href.Should().Be("/api/test");
        link.Rel.Should().Be("prev");
        link.Method.Should().Be("POST");
    }
}

public class PaginationQueryTests
{
    [Fact]
    public void Constructor_Default_SetsDefaultValues()
    {
        // Arrange & Act
        var query = new PaginationQuery();

        // Assert
        query.Page.Should().Be(1);
        query.Limit.Should().Be(50);
        query.Sort.Should().BeNull();
        query.Order.Should().Be("desc");
    }

    [Fact]
    public void Constructor_WithValues_SetsProperties()
    {
        // Arrange & Act
        var query = new PaginationQuery
        {
            Page = 2,
            Limit = 25,
            Sort = "title",
            Order = "asc"
        };

        // Assert
        query.Page.Should().Be(2);
        query.Limit.Should().Be(25);
        query.Sort.Should().Be("title");
        query.Order.Should().Be("asc");
    }

    [Theory]
    [InlineData(0, 1)]      // Page below minimum
    [InlineData(-1, 1)]     // Negative page
    [InlineData(100, 100)]  // Valid page
    public void Page_ShouldBeAtLeast1(int inputPage, int expectedPage)
    {
        // Arrange & Act
        var query = new PaginationQuery { Page = inputPage };

        // Assert
        query.Page.Should().Be(expectedPage);
    }

    [Theory]
    [InlineData(0, 50)]     // Limit below minimum
    [InlineData(-1, 50)]    // Negative limit
    [InlineData(150, 100)]  // Limit above maximum
    [InlineData(75, 75)]    // Valid limit
    public void Limit_ShouldBeConstrainedToValidRange(int inputLimit, int expectedLimit)
    {
        // Arrange & Act
        var query = new PaginationQuery { Limit = inputLimit };

        // Assert
        query.Limit.Should().Be(expectedLimit);
    }

    [Fact]
    public void Order_ShouldDefaultToDesc()
    {
        // Arrange & Act
        var query = new PaginationQuery();

        // Assert
        query.Order.Should().Be("desc");
    }

    [Theory]
    [InlineData("asc", "asc")]
    [InlineData("desc", "desc")]
    [InlineData("ASC", "asc")]
    [InlineData("DESC", "desc")]
    [InlineData("invalid", "desc")]
    public void Order_ShouldOnlyAcceptAscOrDesc(string inputOrder, string expectedOrder)
    {
        // Arrange & Act
        var query = new PaginationQuery { Order = inputOrder };

        // Assert
        query.Order.Should().Be(expectedOrder);
    }

    [Fact]
    public void Serialization_ProducesCamelCaseJson()
    {
        // Arrange
        var query = new PaginationQuery
        {
            Page = 2,
            Limit = 25,
            Sort = "created_at",
            Order = "asc"
        };

        // Act
        var json = JsonSerializer.Serialize(query);

        // Assert
        json.Should().Contain("\"page\":2");
        json.Should().Contain("\"limit\":25");
        json.Should().Contain("\"sort\":\"created_at\"");
        json.Should().Contain("\"order\":\"asc\"");
    }
}

public class PaginatedResponseTests
{
    [Fact]
    public void Constructor_Default_SetsDefaultValues()
    {
        // Arrange & Act
        var response = new PaginatedResponse<string>();

        // Assert
        response.Data.Should().NotBeNull();
        response.Data.Should().BeEmpty();
        response.Page.Should().Be(1);
        response.Limit.Should().Be(50);
        response.TotalCount.Should().Be(0);
        response.TotalPages.Should().Be(0);
        response.HasNext.Should().BeFalse();
        response.HasPrevious.Should().BeFalse();
        response.Links.Should().NotBeNull();
        response.Links.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithData_SetsProperties()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3" };

        // Act
        var response = new PaginatedResponse<string>
        {
            Data = items,
            Page = 1,
            Limit = 10,
            TotalCount = 25,
            TotalPages = 3
        };

        // Assert
        response.Data.Should().HaveCount(3);
        response.Data.Should().Contain("item1");
        response.Page.Should().Be(1);
        response.Limit.Should().Be(10);
        response.TotalCount.Should().Be(25);
        response.TotalPages.Should().Be(3);
    }

    [Fact]
    public void HasPrevious_FirstPage_ReturnsFalse()
    {
        // Arrange & Act
        var response = new PaginatedResponse<string>
        {
            Page = 1,
            TotalPages = 5
        };

        // Assert
        response.HasPrevious.Should().BeFalse();
    }

    [Fact]
    public void HasPrevious_NotFirstPage_ReturnsTrue()
    {
        // Arrange & Act
        var response = new PaginatedResponse<string>
        {
            Page = 2,
            TotalPages = 5
        };

        // Assert
        response.HasPrevious.Should().BeTrue();
    }

    [Fact]
    public void HasNext_LastPage_ReturnsFalse()
    {
        // Arrange & Act
        var response = new PaginatedResponse<string>
        {
            Page = 5,
            TotalPages = 5
        };

        // Assert
        response.HasNext.Should().BeFalse();
    }

    [Fact]
    public void HasNext_NotLastPage_ReturnsTrue()
    {
        // Arrange & Act
        var response = new PaginatedResponse<string>
        {
            Page = 2,
            TotalPages = 5
        };

        // Assert
        response.HasNext.Should().BeTrue();
    }

    [Fact]
    public void Links_CanBeSetWithHATEOASLinks()
    {
        // Arrange
        var links = new Dictionary<string, Link>
        {
            { "self", new Link { Href = "/api/channels?page=2&limit=10", Rel = "self" } },
            { "next", new Link { Href = "/api/channels?page=3&limit=10", Rel = "next" } },
            { "prev", new Link { Href = "/api/channels?page=1&limit=10", Rel = "prev" } },
            { "first", new Link { Href = "/api/channels?page=1&limit=10", Rel = "first" } },
            { "last", new Link { Href = "/api/channels?page=5&limit=10", Rel = "last" } }
        };

        // Act
        var response = new PaginatedResponse<string>
        {
            Data = new List<string> { "item1", "item2" },
            Links = links
        };

        // Assert
        response.Links.Should().HaveCount(5);
        response.Links["self"].Href.Should().Be("/api/channels?page=2&limit=10");
        response.Links["next"].Href.Should().Be("/api/channels?page=3&limit=10");
    }

    [Fact]
    public void Serialization_ProducesExpectedJson()
    {
        // Arrange
        var response = new PaginatedResponse<string>
        {
            Data = new List<string> { "item1", "item2" },
            Page = 1,
            Limit = 10,
            TotalCount = 25,
            TotalPages = 3,
            Links = new Dictionary<string, Link>
            {
                { "self", new Link { Href = "/api/test?page=1", Rel = "self" } }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response);

        // Assert
        json.Should().Contain("\"data\":");
        json.Should().Contain("\"page\":1");
        json.Should().Contain("\"limit\":10");
        json.Should().Contain("\"totalCount\":25");
        json.Should().Contain("\"totalPages\":3");
        json.Should().Contain("\"hasPrevious\":false");
        json.Should().Contain("\"hasNext\":true");
        json.Should().Contain("\"_links\":");
    }

    [Fact]
    public void HasNext_WhenTotalPagesIsZero_ReturnsFalse()
    {
        // Arrange & Act
        var response = new PaginatedResponse<string>
        {
            Page = 1,
            TotalPages = 0
        };

        // Assert
        response.HasNext.Should().BeFalse();
    }

    [Fact]
    public void WithComplexType_SerializesCorrectly()
    {
        // Arrange
        var response = new PaginatedResponse<TestItem>
        {
            Data = new List<TestItem>
            {
                new TestItem { Id = 1, Name = "Item 1" },
                new TestItem { Id = 2, Name = "Item 2" }
            },
            Page = 1,
            Limit = 10,
            TotalCount = 2,
            TotalPages = 1
        };

        // Act
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<PaginatedResponse<TestItem>>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Data.Should().HaveCount(2);
        deserialized.Data[0].Id.Should().Be(1);
        deserialized.Data[0].Name.Should().Be("Item 1");
    }

    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

public class PaginatedResponseCreateFactoryTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsCorrectResponse()
    {
        // Arrange
        // Note: Pagination/slicing of data is done by the caller (repository/service layer)
        // Create just wraps already-paginated data
        var items = new List<string> { "c", "d" }; // Simulating page 2 items (already sliced)
        var basePath = "/api/channels";

        // Act
        var response = PaginatedResponse<string>.Create(
            items,
            page: 2,
            limit: 2,
            totalCount: 5,
            basePath: basePath);

        // Assert
        response.Data.Should().HaveCount(2); // items passed through as-is
        response.Page.Should().Be(2);
        response.Limit.Should().Be(2);
        response.TotalCount.Should().Be(5);
        response.TotalPages.Should().Be(3); // ceil(5/2) = 3
        response.HasNext.Should().BeTrue(); // page 2 of 3
        response.HasPrevious.Should().BeTrue(); // page 2
    }

    [Fact]
    public void Create_FirstPage_HasCorrectLinks()
    {
        // Arrange
        var items = new List<string> { "a", "b" };
        var basePath = "/api/items";

        // Act
        var response = PaginatedResponse<string>.Create(
            items,
            page: 1,
            limit: 2,
            totalCount: 5,
            basePath: basePath);

        // Assert
        response.Links.Should().ContainKey("self");
        response.Links.Should().ContainKey("first");
        response.Links.Should().ContainKey("last");
        response.Links.Should().ContainKey("next");
        response.Links["self"].Href.Should().Contain("page=1");
        response.Links["first"].Href.Should().Contain("page=1");
        response.Links["last"].Href.Should().Contain("page=3");
        response.Links["next"].Href.Should().Contain("page=2");
        response.Links.Should().NotContainKey("prev");
    }

    [Fact]
    public void Create_LastPage_HasCorrectLinks()
    {
        // Arrange
        var items = new List<string> { "e" };
        var basePath = "/api/items";

        // Act
        var response = PaginatedResponse<string>.Create(
            items,
            page: 3,
            limit: 2,
            totalCount: 5,
            basePath: basePath);

        // Assert
        response.Links.Should().ContainKey("self");
        response.Links.Should().ContainKey("first");
        response.Links.Should().ContainKey("last");
        response.Links.Should().ContainKey("prev");
        response.Links.Should().NotContainKey("next");
        response.Links["self"].Href.Should().Contain("page=3");
        response.Links["last"].Href.Should().Contain("page=3");
        response.Links["prev"].Href.Should().Contain("page=2");
    }

    [Fact]
    public void Create_WithQueryParameters_IncludesQueryInLinks()
    {
        // Arrange
        var items = new List<string> { "a" };
        var basePath = "/api/channels?enabled=true&sort=title";

        // Act
        var response = PaginatedResponse<string>.Create(
            items,
            page: 1,
            limit: 10,
            totalCount: 1,
            basePath: basePath);

        // Assert
        response.Links["self"].Href.Should().Contain("enabled=true");
        response.Links["self"].Href.Should().Contain("sort=title");
    }

    [Fact]
    public void Create_EmptyResult_ReturnsValidResponse()
    {
        // Arrange
        var items = new List<string>();
        var basePath = "/api/items";

        // Act
        var response = PaginatedResponse<string>.Create(
            items,
            page: 1,
            limit: 10,
            totalCount: 0,
            basePath: basePath);

        // Assert
        response.Data.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
        response.TotalPages.Should().Be(0);
        response.HasNext.Should().BeFalse();
        response.HasPrevious.Should().BeFalse();
    }

    [Fact]
    public void Create_SinglePage_HasNoNextOrPrev()
    {
        // Arrange
        var items = new List<string> { "a", "b" };
        var basePath = "/api/items";

        // Act
        var response = PaginatedResponse<string>.Create(
            items,
            page: 1,
            limit: 10,
            totalCount: 2,
            basePath: basePath);

        // Assert
        response.Links.Should().ContainKey("self");
        response.Links.Should().ContainKey("first");
        response.Links.Should().ContainKey("last");
        response.Links.Should().NotContainKey("next");
        response.Links.Should().NotContainKey("prev");
    }
}