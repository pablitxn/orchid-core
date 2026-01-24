using Application.UseCases.Project.Common;
using MediatR;

namespace Application.UseCases.Project.CreateProject;

public class CreateProjectByAudioCommand(
    string targetLanguage,
    string fileName,
    string contentType,
    long fileSize,
    string url
) : IRequest<ProjectDto>
{
    public string TargetLanguage { get; } = targetLanguage;
    public string Url { get; } = url;
    public string FileName { get; } = fileName;
    public long FileSize { get; } = fileSize;
    public string ContentType { get; } = contentType;
}
