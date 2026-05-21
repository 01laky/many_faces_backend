using System.Text.Json.Serialization;

namespace BeDemo.Api.Models.Requests.Profile;

public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    [JsonPropertyName("enableAnimatedGradient")]
    public bool? EnableAnimatedGradient { get; set; }
}

