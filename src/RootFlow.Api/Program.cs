using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RootFlow.Api.Auth;
using RootFlow.Api.Contracts.Auth;
using RootFlow.Api.Contracts.Chat;
using RootFlow.Application.Auth;
using RootFlow.Application.Auth.Commands;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Chat;
using RootFlow.Application.Chat.Commands;
using RootFlow.Application.Conversations;
using RootFlow.Application.Conversations.Queries;
using RootFlow.Application.Documents;
using RootFlow.Application.Documents.Commands;
using RootFlow.Application.Documents.Queries;
using RootFlow.Infrastructure.DependencyInjection;
using RootFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "RootFlowFrontend";

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.PostConfigure<JwtOptions>(options =>
{
    options.Key = string.IsNullOrWhiteSpace(options.Key)
        ? Environment.GetEnvironmentVariable("ROOTFLOW_JWT_KEY") ?? string.Empty
        : options.Key;
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        jwtOptions.Key = string.IsNullOrWhiteSpace(jwtOptions.Key)
            ? Environment.GetEnvironmentVariable("ROOTFLOW_JWT_KEY") ?? string.Empty
            : jwtOptions.Key;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = JwtTokenGenerator.CreateSigningKey(jwtOptions.Key),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<JwtTokenGenerator>();
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
app.UseAuthentication();
app.UseAuthorization();

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
var auth = app.MapGroup("/api/auth");

documents.RequireAuthorization();
conversations.RequireAuthorization();

auth.MapPost("/signup", async (
    SignupRequest request,
    AuthService authService,
    JwtTokenGenerator jwtTokenGenerator,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateSignupRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        var session = await authService.SignupAsync(
            new SignupCommand(
                request.FullName,
                request.Email,
                request.Password,
                request.WorkspaceName),
            cancellationToken);

        var token = jwtTokenGenerator.Generate(session);
        return Results.Created("/api/auth/me", token.ToResponse(session));
    }
    catch (AuthConflictException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "request"] = [exception.Message]
        });
    }
});

auth.MapPost("/login", async (
    LoginRequest request,
    AuthService authService,
    JwtTokenGenerator jwtTokenGenerator,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateLoginRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        var session = await authService.LoginAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        var token = jwtTokenGenerator.Generate(session);
        return Results.Ok(token.ToResponse(session));
    }
    catch (UnauthorizedAccessException exception)
    {
        return Results.Json(new { error = exception.Message }, statusCode: StatusCodes.Status401Unauthorized);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "request"] = [exception.Message]
        });
    }
});

auth.MapGet("/me", async (
    ClaimsPrincipal user,
    AuthService authService,
    CancellationToken cancellationToken) =>
{
    var session = await authService.GetCurrentSessionAsync(
        user.GetRequiredUserId(),
        user.GetRequiredWorkspaceId(),
        cancellationToken);

    return session is null ? Results.Unauthorized() : Results.Ok(session.ToResponse());
})
.RequireAuthorization();

documents.MapPost("", async (
    IFormFile file,
    ClaimsPrincipal user,
    DocumentService documentService,
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
        new UploadDocumentCommand(user.GetRequiredWorkspaceId(), upload),
        cancellationToken);

    return Results.Created($"/api/documents/{document.Id}", document);
})
.DisableAntiforgery();

documents.MapGet("", async (
    ClaimsPrincipal user,
    DocumentService documentService,
    CancellationToken cancellationToken) =>
{
    var documentsResult = await documentService.ListAsync(
        new ListDocumentsQuery(user.GetRequiredWorkspaceId()),
        cancellationToken);

    return Results.Ok(documentsResult);
});

documents.MapGet("/{documentId:guid}", async (
    Guid documentId,
    ClaimsPrincipal user,
    DocumentService documentService,
    CancellationToken cancellationToken) =>
{
    var document = await documentService.GetByIdAsync(
        new GetDocumentByIdQuery(user.GetRequiredWorkspaceId(), documentId),
        cancellationToken);

    return document is null ? Results.NotFound() : Results.Ok(document);
});

app.MapPost("/api/chat", async (
    AskQuestionRequest request,
    ClaimsPrincipal user,
    ChatService chatService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new { error = "Question is required." });
    }

    var answer = await chatService.AskAsync(
        new AskQuestionCommand(
            user.GetRequiredWorkspaceId(),
            request.Question,
            request.ConversationId,
            request.MaxContextChunks),
        cancellationToken);

    return Results.Ok(answer);
})
.RequireAuthorization();

conversations.MapGet("", async (
    ClaimsPrincipal user,
    ConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var conversationList = await conversationService.ListAsync(
        new ListConversationsQuery(user.GetRequiredWorkspaceId()),
        cancellationToken);

    return Results.Ok(conversationList);
});

conversations.MapGet("/{conversationId:guid}", async (
    Guid conversationId,
    ClaimsPrincipal user,
    ConversationService conversationService,
    CancellationToken cancellationToken) =>
{
    var conversation = await conversationService.GetHistoryAsync(
        new GetConversationHistoryQuery(user.GetRequiredWorkspaceId(), conversationId),
        cancellationToken);

    return conversation is null ? Results.NotFound() : Results.Ok(conversation);
});

app.Run();

static Dictionary<string, string[]> ValidateSignupRequest(SignupRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.FullName))
    {
        errors["fullName"] = ["Full name is required."];
    }

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        errors["email"] = ["Email is required."];
    }

    if (string.IsNullOrWhiteSpace(request.Password))
    {
        errors["password"] = ["Password is required."];
    }

    if (string.IsNullOrWhiteSpace(request.WorkspaceName))
    {
        errors["workspaceName"] = ["Workspace name is required."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateLoginRequest(LoginRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        errors["email"] = ["Email is required."];
    }

    if (string.IsNullOrWhiteSpace(request.Password))
    {
        errors["password"] = ["Password is required."];
    }

    return errors;
}

public partial class Program
{
}
