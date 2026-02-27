using System.Text.Json.Serialization;

namespace Yallarhorn.Models;

/// <summary>
/// Represents a standardized error response for API errors.
/// </summary>
public sealed class ErrorDetails
{
    /// <summary>
    /// Gets or sets the HTTP status code of the error.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed error message.
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    /// Gets or sets the request ID for tracing.
    /// </summary>
    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    /// <summary>
    /// Gets or sets the stack trace (only populated in development).
    /// </summary>
    [JsonPropertyName("stackTrace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the error.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new ErrorDetails instance.
    /// </summary>
    public ErrorDetails() { }

    /// <summary>
    /// Creates a new ErrorDetails instance with specified values.
    /// </summary>
    public ErrorDetails(int status, string error, string? detail = null)
    {
        Status = status;
        Error = error;
        Detail = detail;
    }

    /// <summary>
    /// Returns a JSON string representation of the error details.
    /// </summary>
    public override string ToString()
    {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }
}