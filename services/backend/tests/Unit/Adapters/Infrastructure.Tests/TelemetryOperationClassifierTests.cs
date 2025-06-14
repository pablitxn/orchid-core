using Infrastructure.Telemetry;
using MediatR;

namespace Infrastructure.Tests.Telemetry
{

public class TelemetryOperationClassifierTests
{
    [Theory]
    [InlineData("ConsumeCreditsCommand", false)]
    [InlineData("ListChatSessionsQuery", false)]
    [InlineData("CreateChatSessionCommand", false)]
    [InlineData("UpdateChatSessionCommand", false)]
    [InlineData("DeleteChatSessionCommand", false)]
    [InlineData("GetChatSessionQuery", false)]
    [InlineData("ListUsersQuery", false)]
    [InlineData("CreateUserCommand", false)]
    [InlineData("UpdateUserCommand", false)]
    [InlineData("LoginCommand", false)]
    [InlineData("RegisterCommand", false)]
    [InlineData("ChainOfSpreadsheetCommand", true)]
    [InlineData("SendChatMessageCommand", true)]
    [InlineData("StreamChatMessageCommand", true)]
    [InlineData("GenerateEmbeddingCommand", true)]
    [InlineData("ProcessWithAICommand", true)]
    [InlineData("InvokeSemanticFunctionCommand", true)]
    [InlineData("ExecutePromptCommand", true)]
    [InlineData("GenerateCompletionCommand", true)]
    public void IsAiOperation_ClassifiesOperationsCorrectly(string operationName, bool expectedIsAi)
    {
        // Arrange
        var request = new TestRequest(operationName);

        // Act
        var isAiOperation = TelemetryOperationClassifier.IsAiOperation(request);

        // Assert
        Assert.Equal(expectedIsAi, isAiOperation);
    }

    [Fact]
    public void IsAiOperation_ExcludesCrudOperationsInAiNamespace()
    {
        // These should be excluded even though they're in an AI namespace
        var crudRequests = new[]
        {
            new AiNamespaceRequest("ListAIModelsQuery"),
            new AiNamespaceRequest("CreateAIModelCommand"),
            new AiNamespaceRequest("UpdateAIModelCommand"),
            new AiNamespaceRequest("DeleteAIModelCommand"),
            new AiNamespaceRequest("GetAIModelQuery")
        };

        foreach (var request in crudRequests)
        {
            var isAiOperation = TelemetryOperationClassifier.IsAiOperation(request);
            Assert.False(isAiOperation, $"{request.GetType().Name} should not be classified as AI operation");
        }
    }

    [Fact]
    public void IsAiOperation_IncludesAiOperationsInAiNamespace()
    {
        // These should be included because they're AI operations in AI namespace
        var aiRequests = new[]
        {
            new AiNamespaceRequest("GenerateResponseCommand"),
            new AiNamespaceRequest("ProcessNaturalLanguageCommand"),
            new AiNamespaceRequest("InvokeSemanticFunctionCommand")
        };

        foreach (var request in aiRequests)
        {
            var isAiOperation = TelemetryOperationClassifier.IsAiOperation(request);
            Assert.True(isAiOperation, $"{request.GetType().Name} should be classified as AI operation");
        }
    }

    private class TestRequest : IRequest
    {
        public string OperationName { get; }
        
        public TestRequest(string operationName)
        {
            OperationName = operationName;
        }

        public override string ToString() => OperationName;
    }

    private class AiNamespaceRequest : IRequest
    {
        public string OperationName { get; }
        
        public AiNamespaceRequest(string operationName)
        {
            OperationName = operationName;
        }

        public override string ToString() => OperationName;
    }
}
}

// Namespace for AI-related requests
namespace Application.UseCases.AI
{
    public class ListAIModelsQuery : IRequest { }
    public class CreateAIModelCommand : IRequest { }
    public class UpdateAIModelCommand : IRequest { }
    public class DeleteAIModelCommand : IRequest { }
    public class GetAIModelQuery : IRequest { }
    public class GenerateResponseCommand : IRequest { }
    public class ProcessNaturalLanguageCommand : IRequest { }
    public class InvokeSemanticFunctionCommand : IRequest { }
}