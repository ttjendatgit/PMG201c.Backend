using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PMG201c.Backend.Configuration;
using PMG201c.Backend.Data;
using PMG201c.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AssessmentService>();
builder.Services.AddSingleton<FileExtractionService>();
builder.Services.AddScoped<AssessmentFileService>();
builder.Services.AddSingleton<RubricParserService>();
builder.Services.AddScoped<RubricService>();
builder.Services.AddScoped<SubmissionService>();
// AI grading provider – switched by AI:Provider config key
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.AddSingleton<AiPromptBuilderService>();

var aiProvider = builder.Configuration["AI:Provider"] ?? "Mock";
if (aiProvider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
{
    var orSection      = builder.Configuration.GetSection("AI:OpenRouter");
    var baseUrl        = orSection["BaseUrl"] ?? "https://openrouter.ai/api/v1";
    var timeoutSeconds = orSection.GetValue<int>("TimeoutSeconds", 120);

    builder.Services.AddHttpClient<IAiGradingService, OpenRouterAiGradingService>(client =>
    {
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/');
        client.Timeout     = TimeSpan.FromSeconds(timeoutSeconds);
        client.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost:5000/pmg201c-gradeai");
        client.DefaultRequestHeaders.Add("X-Title", "PMG201c GradeAI");
    });
}
else
{
    builder.Services.AddScoped<IAiGradingService, MockAiGradingService>();
}

builder.Services.AddScoped<GradingJobService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<ExcelExportService>();

// Controllers
builder.Services.AddControllers();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:AccessSecret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Swagger UI with Bearer auth support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        service = "PMG201c GradeAI Backend",
        timestamp = DateTime.UtcNow
    });
})
.WithName("HealthCheck")
.WithTags("System");

app.Run();
