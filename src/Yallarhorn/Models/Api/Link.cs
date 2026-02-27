using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Represents a HATEOAS link for REST API responses.
/// </summary>
public class Link
{
    /// <summary>
    /// Gets or sets the hyperlink reference (URL).
    /// </summary>
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relationship type (e.g., "self", "next", "prev", "first", "last").
    /// </summary>
    [JsonPropertyName("rel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Rel { get; set; }

    /// <summary>
    /// Gets or sets the HTTP method for the link (default: "GET").
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";
}