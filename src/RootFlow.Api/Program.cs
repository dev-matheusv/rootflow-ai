using Microsoft.Extensions.Options;
using RootFlow.Api.Contracts.Chat;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Chat;
using RootFlow.Application.Chat.Commands;
using RootFlow.Application.Conversations;
using RootFlow.Application.Conversations.Queries;
using RootFlow.Application.Documents;
using RootFlow.Application.Documents.Commands;
using RootFlow.Application.Documents.Queries;
using RootFlow.Infrastructure.Configuration;
using RootFlow.Infrastructure.DependencyInjection;
using RootFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "RootFlowFrontend";

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:4173",
                "https://localhost:4173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddRootFlowInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("IntegrationTesting"))
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<PostgresDatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.MapGet("/", () => Results.Ok(new
{
    name = "RootFlow API",
    status = "ready",
    environment = app.Environment.EnvironmentName
}))
.WithName("GetApiInfo");

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy"
}))
.WithName("GetHealth");

var documents = app.MapGroup("/api/documents");
var conversations = app.MapGroup("/api/conversations");

documents.MapPost("", async (
    IFormFile file,
    DocumentService documentService,
    IOptions<RootFlowOptions> rootFlowOptions,
    CancellationToken cancellationToken) =>
{
    if (file.Length <= 0)
    {
        return Results.BadRequest(new { error = "A non-empty file is required." });
    }

    await using var stream = file.OpenReadStream();

    var upload = new FileUpload(
        file.FileName,
        string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
        file.Length,
        stream);

    var document = await documentService.UploadAsync(
        new UploadDocumentCommand(rootFlowOptions.Value.DefaultWorkspaceId, upload),
        cancellationToken);

    return Results.Created($"/api/documents/{document.Id}", document);
})
.DisableAntiforgery();

documents.MapGet("", async (
    DocumentService documentService,
    IOptions<RootFlowOptions> rootFlowOptions,
    CancellationToken cancellationToken) =>
{
    var documentsResult = await documentService.ListAsync(
        new ListDocumentsQuery(rootFlowOptions.Value.DefaultWorkspaceId),
        cancellationToken);

    return Results.Ok(documentsResult);
});

documents.MapGet("/{documentId:guid}", async (
    Guid documentId,
    DocumentService documentService,
    IOptions<RootFlowOptions> rootFlowOptions,
    CancellationToken cancellationToken) =>
{
    var document = await documentService.GetByIdAsync(
        new GetDocumentByIdQuery(rootFlowOptions.Value.DefaultWorkspaceId, documentId),
        cancellationToken);

    return document is null ? Results.NotFound() : Results.Ok(document);
});

app.MapPost("/api/chat", async (
    AskQuestionRequest request,
    ChatService chatService,
    IOptions<RootFlowOptions> rootFlowOptions,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new { error = "Question is required." });
    }

    var answer = await chatService.AskAsync(
        new AskQuestionCommand(
            rootFlowOptions.Value.DefaultWorkspaceId,
            request.Question,
            request.ConversationId,
            request.MaxContextChunks),
        cancellationToken);

    return Results.Ok(answer);
});

conversations.MapGet("", async (
    ConversationService conversationService,
    IOptions<RootFlowOptions> rootFlowOptions,
    CancellationToken cancellationToken) =>
{
    var conversationList = await conversationService.ListAsync(
        new ListConversationsQuery(rootFlowOptions.Value.DefaultWorkspaceId),
        cancellationToken);

    return Results.Ok(conversationList);
});

conversations.MapGet("/{conversationId:guid}", async (
    Guid conversationId,
    ConversationService conversationService,
    IOptions<RootFlowOptions> rootFlowOptions,
    CancellationToken cancellationToken) =>
{
    var conversation = await conversationService.GetHistoryAsync(
        new GetConversationHistoryQuery(rootFlowOptions.Value.DefaultWorkspaceId, conversationId),
        cancellationToken);

    return conversation is null ? Results.NotFound() : Results.Ok(conversation);
});

app.Run();

public partial class Program
{
}
