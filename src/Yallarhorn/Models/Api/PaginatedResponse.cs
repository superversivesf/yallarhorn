using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Represents a paginated API response with HATEOAS links.
/// </summary>
/// <typeparam name="T">The type of items in the response.</typeparam>
public class PaginatedResponse<T>
{
    /// <summary>
    /// Gets or sets the data items for the current page.
    /// </summary>
    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the current page number (1-indexed).
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 50;

    /// <summary>
    /// Gets or sets the total number of items across all pages.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    [JsonPropertyName("hasPrevious")]
    public bool HasPrevious => Page > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    [JsonPropertyName("hasNext")]
    public bool HasNext => Page < TotalPages;

    /// <summary>
    /// Gets or sets the HATEOAS links for navigation.
    /// Key is the link relation (e.g., "self", "next", "prev", "first", "last").
    /// </summary>
    [JsonPropertyName("_links")]
    public Dictionary<string, Link> Links { get; set; } = new();

    /// <summary>
    /// Creates a new paginated response with automatically generated HATEOAS links.
    /// </summary>
    /// <param name="data">The data items for the current page.</param>
    /// <param name="page">The current page number.</param>
    /// <param name="limit">The number of items per page.</param>
    /// <param name="totalCount">The total number of items across all pages.</param>
    /// <param name="basePath">The base path for constructing links (may include query parameters).</param>
    /// <returns>A new PaginatedResponse instance with generated links.</returns>
    public static PaginatedResponse<T> Create(
        IEnumerable<T> data,
        int page,
        int limit,
        int totalCount,
        string basePath)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)limit);
        var links = GenerateLinks(basePath, page, limit, totalPages);

        return new PaginatedResponse<T>
        {
            Data = data.ToList(),
            Page = page,
            Limit = limit,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Links = links
        };
    }

    /// <summary>
    /// Generates HATEOAS links for pagination navigation.
    /// </summary>
    private static Dictionary<string, Link> GenerateLinks(
        string basePath,
        int currentPage,
        int limit,
        int totalPages)
    {
        var links = new Dictionary<string, Link>();

        // Parse base path to preserve existing query parameters
        var (path, existingQuery) = ParseBasePath(basePath);

        // Self link (current page)
        links["self"] = new Link
        {
            Href = BuildUrl(path, existingQuery, currentPage, limit),
            Rel = "self"
        };

        // First page link
        links["first"] = new Link
        {
            Href = BuildUrl(path, existingQuery, 1, limit),
            Rel = "first"
        };

        // Last page link
        if (totalPages > 0)
        {
            links["last"] = new Link
            {
                Href = BuildUrl(path, existingQuery, totalPages, limit),
                Rel = "last"
            };
        }

        // Next page link (only if not on last page)
        if (currentPage < totalPages)
        {
            links["next"] = new Link
            {
                Href = BuildUrl(path, existingQuery, currentPage + 1, limit),
                Rel = "next"
            };
        }

        // Previous page link (only if not on first page)
        if (currentPage > 1)
        {
            links["prev"] = new Link
            {
                Href = BuildUrl(path, existingQuery, currentPage - 1, limit),
                Rel = "prev"
            };
        }

        return links;
    }

    /// <summary>
    /// Parses a base path into path and query components.
    /// </summary>
    private static (string path, string query) ParseBasePath(string basePath)
    {
        var separatorIndex = basePath.IndexOf('?');
        if (separatorIndex < 0)
        {
            return (basePath, string.Empty);
        }

        return (basePath[..separatorIndex], basePath[(separatorIndex + 1)..]);
    }

    /// <summary>
    /// Builds a URL with pagination query parameters.
    /// </summary>
    private static string BuildUrl(string path, string existingQuery, int page, int limit)
    {
        var queryParts = new List<string> { $"page={page}", $"limit={limit}" };

        // Preserve existing query parameters (but override page/limit if present)
        if (!string.IsNullOrEmpty(existingQuery))
        {
            foreach (var part in existingQuery.Split('&'))
            {
                if (!part.StartsWith("page=") && !part.StartsWith("limit="))
                {
                    queryParts.Add(part);
                }
            }
        }

        return $"{path}?{string.Join("&", queryParts)}";
    }
}