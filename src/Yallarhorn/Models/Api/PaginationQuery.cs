using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Represents pagination query parameters for API requests.
/// </summary>
public class PaginationQuery
{
    private int _page = 1;
    private int _limit = 50;
    private string _order = "desc";

    /// <summary>
    /// Gets or sets the page number (1-indexed). Defaults to 1.
    /// Values less than 1 will be normalized to 1.
    /// </summary>
    [JsonPropertyName("page")]
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>
    /// Gets or sets the number of items per page. Defaults to 50, max 100.
    /// Values outside the range 1-100 will be normalized to valid values.
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit
    {
        get => _limit;
        set
        {
            if (value < 1)
            {
                _limit = 50; // Reset to default for invalid values
            }
            else if (value > 100)
            {
                _limit = 100;
            }
            else
            {
                _limit = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the field to sort by. Optional.
    /// </summary>
    [JsonPropertyName("sort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sort { get; set; }

    /// <summary>
    /// Gets or sets the sort order. Allowed values: "asc" or "desc". Defaults to "desc".
    /// Invalid values will be normalized to "desc".
    /// </summary>
    [JsonPropertyName("order")]
    public string Order
    {
        get => _order;
        set
        {
            var normalized = value?.ToLowerInvariant();
            _order = normalized is "asc" or "desc" ? normalized : "desc";
        }
    }
}