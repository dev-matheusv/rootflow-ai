using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using RootFlow.Api.Admin;
using RootFlow.Api.Auth;
using RootFlow.Api.Contracts.Admin;
using RootFlow.Api.Contracts.Auth;
using RootFlow.Api.Contracts.Billing;
using RootFlow.Api.Contracts.Chat;
using RootFlow.Api.Contracts.Workspaces;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Auth;
using RootFlow.Application.Billing;
using RootFlow.Application.Billing.Commands;
using RootFlow.Application.Billing.Dtos;
using RootFlow.Application.Billing.Queries;
using RootFlow.Application.Auth.Commands;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Chat;
using RootFlow.Application.Chat.Commands;
using RootFlow.Application.Conversations;
using RootFlow.Application.Conversations.Queries;
using RootFlow.Application.Documents;
using RootFlow.Application.Documents.Commands;
using RootFlow.Application.Documents.Queries;
using RootFlow.Application.PlatformAdmin;
using RootFlow.Application.PlatformAdmin.Queries;
using RootFlow.Application.Workspaces;
using RootFlow.Application.Workspaces.Commands;
using RootFlow.Application.Workspaces.Queries;
using RootFlow.Domain.Workspaces;
using RootFlow.Infrastructure.DependencyInjection;
using RootFlow.Infrastructure.Persistence;

ConfigurePlatformUrlsFromPort();

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "RootFlowFrontend";

builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("IntegrationTesting"))
{
    builder.Logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
}
else
{
    builder.Logging.AddJsonConsole(options =>
    {
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
}

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.Configure<JwtOptions>(options =>
{
    var jwtOptions = ResolveJwtOptions(builder.Configuration);
    options.Issuer = jwtOptions.Issuer;
    options.Audience = jwtOptions.Audience;
    options.Key = jwtOptions.Key;
    options.ExpiresInMinutes = jwtOptions.ExpiresInMinutes;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = ResolveJwtOptions(builder.Configuration);
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
        var allowedCorsOrigins = ResolveAllowedCorsOrigins(builder.Configuration, builder.Environment);
        policy.WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddRootFlowInfrastructure(builder.Configuration);

var app = builder.Build();
var allowedCorsOrigins = ResolveAllowedCorsOrigins(app.Configuration, app.Environment);

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

app.Logger.LogInformation(
    "Starting RootFlow API in {Environment} with {CorsOriginCount} allowed frontend origins.",
    app.Environment.EnvironmentName,
    allowedCorsOrigins.Length);

app.UseForwardedHeaders();
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
var billing = app.MapGroup("/api/billing");
var admin = app.MapGroup("/api/admin");
var workspaces = app.MapGroup("/api/workspaces");

documents.RequireAuthorization();
conversations.RequireAuthorization();
billing.RequireAuthorization();
admin.RequireAuthorization();
workspaces.RequireAuthorization();

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

auth.MapPost("/forgot-password", async (
    ForgotPasswordRequest request,
    AuthService authService,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateForgotPasswordRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        var message = await authService.RequestPasswordResetAsync(
            new ForgotPasswordCommand(request.Email),
            cancellationToken);

        return Results.Ok(new MessageResponse(message));
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "request"] = [exception.Message]
        });
    }
});

auth.MapPost("/reset-password", async (
    ResetPasswordRequest request,
    AuthService authService,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateResetPasswordRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        await authService.ResetPasswordAsync(
            new ResetPasswordCommand(request.Token, request.NewPassword),
            cancellationToken);

        return Results.Ok(new MessageResponse("Your password has been updated. You can now sign in with the new password."));
    }
    catch (InvalidPasswordResetTokenException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "request"] = [exception.Message]
        });
    }
});

billing.MapGet("/plans", async (
    WorkspaceBillingService workspaceBillingService,
    StripeBillingOptions stripeBillingOptions,
    CancellationToken cancellationToken) =>
{
    var plans = await workspaceBillingService.ListPlansAsync(
        new ListBillingPlansQuery(),
        cancellationToken);

    return Results.Ok(plans.Select(plan => plan.ToResponse(
        ResolveStripePlanPriceId(stripeBillingOptions, plan.Code))));
});

billing.MapGet("/credit-packs", async (
    WorkspacePaymentService workspacePaymentService,
    CancellationToken cancellationToken) =>
{
    var creditPacks = await workspacePaymentService.ListCreditPacksAsync(cancellationToken);
    return Results.Ok(creditPacks.Select(creditPack => creditPack.ToResponse()));
});

admin.MapGet("/dashboard", async (
    ClaimsPrincipal user,
    IPlatformAdminAccessService platformAdminAccessService,
    PlatformAdminDashboardService platformAdminDashboardService,
    CancellationToken cancellationToken) =>
{
    if (!platformAdminAccessService.HasAccess(user.GetRequiredUserEmail()))
    {
        return Results.Forbid();
    }

    var dashboard = await platformAdminDashboardService.GetDashboardAsync(
        new GetPlatformAdminDashboardQuery(),
        cancellationToken);

    return Results.Ok(dashboard.ToResponse());
});

billing.MapPost("/checkout/subscription", async (
    CreateWorkspaceSubscriptionCheckoutRequest request,
    ClaimsPrincipal user,
    WorkspacePaymentService workspacePaymentService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.PlanCode))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["planCode"] = ["Plan code is required."]
        });
    }

    try
    {
        var checkoutSession = await workspacePaymentService.CreateSubscriptionCheckoutAsync(
            new CreateWorkspaceSubscriptionCheckoutCommand(user.GetRequiredWorkspaceId(), request.PlanCode),
            cancellationToken);

        return Results.Ok(checkoutSession.ToResponse());
    }
    catch (BillingCheckoutUnavailableException exception)
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

billing.MapPost("/checkout/credits", async (
    CreateWorkspaceCreditPurchaseCheckoutRequest request,
    ClaimsPrincipal user,
    WorkspacePaymentService workspacePaymentService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.CreditPackCode))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["creditPackCode"] = ["Credit pack code is required."]
        });
    }

    try
    {
        var checkoutSession = await workspacePaymentService.CreateCreditPurchaseCheckoutAsync(
            new CreateWorkspaceCreditPurchaseCheckoutCommand(user.GetRequiredWorkspaceId(), request.CreditPackCode),
            cancellationToken);

        return Results.Ok(checkoutSession.ToResponse());
    }
    catch (BillingCheckoutUnavailableException exception)
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

billing.MapPost("/checkout", async (
    CreateBillingCheckoutRequest request,
    ClaimsPrincipal user,
    WorkspacePaymentService workspacePaymentService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.PriceId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["priceId"] = ["Price id is required."]
        });
    }

    try
    {
        var checkoutSession = await workspacePaymentService.CreateCheckoutAsync(
            new CreateWorkspaceBillingCheckoutCommand(user.GetRequiredWorkspaceId(), request.PriceId),
            cancellationToken);

        return Results.Ok(new BillingCheckoutRedirectResponse(
            checkoutSession.SessionId,
            checkoutSession.CheckoutUrl));
    }
    catch (BillingCheckoutUnavailableException exception)
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

app.MapPost("/api/billing/webhooks/stripe", async (
    HttpRequest request,
    WorkspacePaymentService workspacePaymentService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    request.EnableBuffering();
    request.Body.Position = 0;

    using var reader = new StreamReader(request.Body, leaveOpen: true);
    var payload = await reader.ReadToEndAsync(cancellationToken);
    request.Body.Position = 0;

    var signatureHeader = request.Headers["Stripe-Signature"].ToString();

    try
    {
        await workspacePaymentService.HandleStripeWebhookAsync(payload, signatureHeader, cancellationToken);
        return Results.Ok(new MessageResponse("Stripe webhook processed."));
    }
    catch (BillingWebhookValidationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (BillingWebhookProcessingException exception)
    {
        logger.LogError(
            exception,
            "Stripe webhook processing encountered a recoverable synchronization error. Returning HTTP 202 so the request does not fail hard.");

        return Results.Accepted(
            value: new MessageResponse("Stripe webhook received with logged processing warnings."));
    }
    catch (Exception exception)
    {
        logger.LogError(
            exception,
            "Stripe webhook failed unexpectedly before billing synchronization completed. Returning HTTP 202 after logging the failure.");

        return Results.Accepted(
            value: new MessageResponse("Stripe webhook received with unexpected logged processing warnings."));
    }
});

workspaces.MapPost("/{workspaceId:guid}/invites", async (
    Guid workspaceId,
    InviteWorkspaceMemberRequest request,
    ClaimsPrincipal user,
    WorkspaceCollaborationService workspaceCollaborationService,
    CancellationToken cancellationToken) =>
{
    if (user.GetRequiredWorkspaceId() != workspaceId)
    {
        return Results.Forbid();
    }

    var validationErrors = ValidateInviteWorkspaceMemberRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    if (!TryParseWorkspaceRole(request.Role, out var role))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["role"] = ["Role must be Owner, Admin, or Member."]
        });
    }

    try
    {
        var invitation = await workspaceCollaborationService.InviteAsync(
            new InviteWorkspaceMemberCommand(
                workspaceId,
                user.GetRequiredUserId(),
                request.Email,
                role),
            cancellationToken);

        return Results.Ok(invitation.ToResponse());
    }
    catch (WorkspaceAccessDeniedException exception)
    {
        return Results.Json(new { error = exception.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (WorkspaceInviteConflictException exception)
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

workspaces.MapPost("/invites/accept", async (
    AcceptWorkspaceInviteRequest request,
    ClaimsPrincipal user,
    WorkspaceCollaborationService workspaceCollaborationService,
    JwtTokenGenerator jwtTokenGenerator,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateAcceptWorkspaceInviteRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        var session = await workspaceCollaborationService.AcceptInviteAsync(
            new AcceptWorkspaceInviteCommand(user.GetRequiredUserId(), request.Token),
            cancellationToken);

        var token = jwtTokenGenerator.Generate(session);
        return Results.Ok(token.ToResponse(session));
    }
    catch (InvalidWorkspaceInvitationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (WorkspaceInviteConflictException exception)
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

workspaces.MapGet("/{workspaceId:guid}/members", async (
    Guid workspaceId,
    ClaimsPrincipal user,
    WorkspaceCollaborationService workspaceCollaborationService,
    CancellationToken cancellationToken) =>
{
    if (user.GetRequiredWorkspaceId() != workspaceId)
    {
        return Results.Forbid();
    }

    try
    {
        var members = await workspaceCollaborationService.ListMembersAsync(
            new ListWorkspaceMembersQuery(workspaceId, user.GetRequiredUserId()),
            cancellationToken);

        return Results.Ok(members.Select(member => member.ToResponse()));
    }
    catch (WorkspaceAccessDeniedException exception)
    {
        return Results.Json(new { error = exception.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
});

workspaces.MapGet("/{workspaceId:guid}/billing/summary", async (
    Guid workspaceId,
    ClaimsPrincipal user,
    WorkspaceBillingService workspaceBillingService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    Guid authenticatedWorkspaceId;
    Guid authenticatedUserId;

    try
    {
        authenticatedWorkspaceId = user.GetRequiredWorkspaceId();
        authenticatedUserId = user.GetRequiredUserId();
    }
    catch (InvalidOperationException exception)
    {
        logger.LogWarning(
            exception,
            "Rejected workspace billing summary request for workspace {WorkspaceId} because the authenticated billing claims were missing or invalid.",
            workspaceId);

        return Results.Unauthorized();
    }

    if (authenticatedWorkspaceId != workspaceId)
    {
        return Results.Forbid();
    }

    logger.LogInformation(
        "Received workspace billing summary request for workspace {WorkspaceId} from user {UserId}.",
        workspaceId,
        authenticatedUserId);

    try
    {
        var summary = await workspaceBillingService.GetCreditSummaryAsync(
            new GetWorkspaceCreditSummaryQuery(workspaceId),
            cancellationToken);

        return Results.Ok(summary.ToResponse());
    }
    catch (Exception exception)
    {
        logger.LogError(
            exception,
            "Workspace billing summary request failed unexpectedly for workspace {WorkspaceId}. Returning a safe fallback summary.",
            workspaceId);

        return Results.Ok(new WorkspaceCreditSummaryDto(
            null,
            null,
            new WorkspaceCreditBalanceDto(workspaceId, 0, 0, DateTime.UtcNow),
            true).ToResponse());
    }
});

workspaces.MapPost("/{workspaceId:guid}/billing/checkout/subscription", async (
    Guid workspaceId,
    CreateWorkspaceSubscriptionCheckoutRequest request,
    ClaimsPrincipal user,
    WorkspacePaymentService workspacePaymentService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    Guid authenticatedWorkspaceId;
    try
    {
        authenticatedWorkspaceId = user.GetRequiredWorkspaceId();
    }
    catch (InvalidOperationException exception)
    {
        logger.LogWarning(
            exception,
            "Rejected subscription checkout creation for workspace {WorkspaceId} because the authenticated billing claims were missing or invalid.",
            workspaceId);

        return Results.Unauthorized();
    }

    if (authenticatedWorkspaceId != workspaceId)
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(request.PlanCode))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["planCode"] = ["Plan code is required."]
        });
    }

    try
    {
        var checkoutSession = await workspacePaymentService.CreateSubscriptionCheckoutAsync(
            new CreateWorkspaceSubscriptionCheckoutCommand(workspaceId, request.PlanCode),
            cancellationToken);

        return Results.Ok(checkoutSession.ToResponse());
    }
    catch (BillingCheckoutUnavailableException exception)
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

workspaces.MapPost("/{workspaceId:guid}/billing/checkout/credits", async (
    Guid workspaceId,
    CreateWorkspaceCreditPurchaseCheckoutRequest request,
    ClaimsPrincipal user,
    WorkspacePaymentService workspacePaymentService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    Guid authenticatedWorkspaceId;
    try
    {
        authenticatedWorkspaceId = user.GetRequiredWorkspaceId();
    }
    catch (InvalidOperationException exception)
    {
        logger.LogWarning(
            exception,
            "Rejected credit checkout creation for workspace {WorkspaceId} because the authenticated billing claims were missing or invalid.",
            workspaceId);

        return Results.Unauthorized();
    }

    if (authenticatedWorkspaceId != workspaceId)
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(request.CreditPackCode))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["creditPackCode"] = ["Credit pack code is required."]
        });
    }

    try
    {
        var checkoutSession = await workspacePaymentService.CreateCreditPurchaseCheckoutAsync(
            new CreateWorkspaceCreditPurchaseCheckoutCommand(workspaceId, request.CreditPackCode),
            cancellationToken);

        return Results.Ok(checkoutSession.ToResponse());
    }
    catch (BillingCheckoutUnavailableException exception)
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

    try
    {
        var answer = await chatService.AskAsync(
            new AskQuestionCommand(
                user.GetRequiredWorkspaceId(),
                request.Question,
                request.ConversationId,
                request.MaxContextChunks,
                user.GetRequiredUserId()),
            cancellationToken);

        return Results.Ok(answer);
    }
    catch (WorkspaceSubscriptionInactiveException exception)
    {
        return Results.Json(
            new { error = exception.Message, code = "inactive_subscription" },
            statusCode: StatusCodes.Status402PaymentRequired);
    }
    catch (InsufficientWorkspaceCreditsException exception)
    {
        return Results.Json(
            new { error = exception.Message, code = "insufficient_credits" },
            statusCode: StatusCodes.Status402PaymentRequired);
    }
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

static Dictionary<string, string[]> ValidateForgotPasswordRequest(ForgotPasswordRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        errors["email"] = ["Email is required."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateResetPasswordRequest(ResetPasswordRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Token))
    {
        errors["token"] = ["Reset token is required."];
    }

    if (string.IsNullOrWhiteSpace(request.NewPassword))
    {
        errors["newPassword"] = ["New password is required."];
    }
    else if (request.NewPassword.Length < 8)
    {
        errors["newPassword"] = ["Password must be at least 8 characters."];
    }
    else if (request.NewPassword.Length > 128)
    {
        errors["newPassword"] = ["Password must be 128 characters or less."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateInviteWorkspaceMemberRequest(InviteWorkspaceMemberRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        errors["email"] = ["Email is required."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateAcceptWorkspaceInviteRequest(AcceptWorkspaceInviteRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Token))
    {
        errors["token"] = ["Invite token is required."];
    }

    return errors;
}

static bool TryParseWorkspaceRole(string? rawRole, out WorkspaceRole role)
{
    if (string.IsNullOrWhiteSpace(rawRole))
    {
        role = WorkspaceRole.Member;
        return true;
    }

    return Enum.TryParse(rawRole.Trim(), ignoreCase: true, out role);
}

static string? ResolveStripePlanPriceId(StripeBillingOptions stripeBillingOptions, string planCode)
{
    return stripeBillingOptions.PlanPrices
        .FirstOrDefault(option => string.Equals(option.PlanCode, planCode, StringComparison.OrdinalIgnoreCase))
        ?.PriceId
        ?.Trim();
}

static void ConfigurePlatformUrlsFromPort()
{
    var port = Environment.GetEnvironmentVariable("PORT");
    var aspNetCoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    var urls = Environment.GetEnvironmentVariable("URLS");

    if (string.IsNullOrWhiteSpace(port) || !string.IsNullOrWhiteSpace(aspNetCoreUrls) || !string.IsNullOrWhiteSpace(urls))
    {
        return;
    }

    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://0.0.0.0:{port.Trim()}");
}

static JwtOptions ResolveJwtOptions(IConfiguration configuration)
{
    var options = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    options.Key = FirstNonEmpty(configuration["ROOTFLOW_JWT_KEY"], configuration["Jwt:Key"], options.Key) ?? string.Empty;

    if (string.IsNullOrWhiteSpace(options.Key))
    {
        throw new InvalidOperationException(
            "JWT signing key is not configured. Set ROOTFLOW_JWT_KEY or Jwt:Key before starting the API.");
    }

    return options;
}

static string[] ResolveAllowedCorsOrigins(IConfiguration configuration, IHostEnvironment environment)
{
    var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    var envOrigins = SplitDelimitedValues(configuration["ROOTFLOW_ALLOWED_ORIGINS"]);
    var resolvedOrigins = envOrigins
        .Concat(configuredOrigins)
        .Select(origin => origin.Trim())
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (resolvedOrigins.Length > 0)
    {
        return resolvedOrigins;
    }

    if (environment.IsDevelopment() || environment.IsEnvironment("IntegrationTesting"))
    {
        return
        [
            "http://localhost:5173",
            "https://localhost:5173",
            "http://127.0.0.1:5173",
            "https://127.0.0.1:5173",
            "http://localhost:4173",
            "https://localhost:4173",
            "http://127.0.0.1:4173",
            "https://127.0.0.1:4173"
        ];
    }

    throw new InvalidOperationException(
        "No allowed frontend origins are configured. Set ROOTFLOW_ALLOWED_ORIGINS or Cors:AllowedOrigins.");
}

static string[] SplitDelimitedValues(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? []
        : value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static string? FirstNonEmpty(params string?[] values)
{
    return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}

public partial class Program
{
}
