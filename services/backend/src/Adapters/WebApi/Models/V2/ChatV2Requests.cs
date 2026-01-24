using Application.UseCases.ChatV2.Common;

namespace WebApi.Models.V2;

/// <summary>
/// Request model for sending a V2 message.
/// </summary>
/// <param name="Content">The text content of the message.</param>
/// <param name="Artifacts">Optional list of artifacts attached to the message.</param>
public sealed record SendMessageV2Request(
    string Content,
    List<ArtifactDto>? Artifacts = null
);
