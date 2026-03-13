using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("web-app", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? "dev_key_change_me_please";
var jwtIssuer = jwtSection["Issuer"] ?? "auth-api";
var jwtAudience = jwtSection["Audience"] ?? "web-app";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("web-app");
app.UseAuthentication();
app.UseAuthorization();

var passwordHasher = new PasswordHasher<UserRecord>();
var users = new List<UserRecord>();

app.MapPost("/auth/register", (RegisterRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Email and password are required." });
    }

    if (users.Any(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Conflict(new { error = "Email already registered." });
    }

    var user = new UserRecord
    {
        Id = Guid.NewGuid().ToString(),
        Email = request.Email.Trim(),
        Role = string.IsNullOrWhiteSpace(request.Role) ? "player" : request.Role.Trim()
    };

    user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
    users.Add(user);

    return Results.Ok(new { id = user.Id, email = user.Email, role = user.Role });
});

app.MapPost("/auth/login", (LoginRequest request) =>
{
    var user = users.FirstOrDefault(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase));
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
    if (result == PasswordVerificationResult.Failed)
    {
        return Results.Unauthorized();
    }

    var token = CreateJwt(user, jwtKey, jwtIssuer, jwtAudience);
    return Results.Ok(new { token, user = new { id = user.Id, email = user.Email, role = user.Role } });
});

app.MapGet("/auth/me", (ClaimsPrincipal user) =>
{
    var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    var email = user.FindFirstValue(JwtRegisteredClaimNames.Email);
    var role = user.FindFirstValue(ClaimTypes.Role);

    return Results.Ok(new { id = sub, email, role });
}).RequireAuthorization();

app.Run();

static string CreateJwt(UserRecord user, string key, string issuer, string audience)
{
    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id),
        new(JwtRegisteredClaimNames.Email, user.Email),
        new(ClaimTypes.Role, user.Role)
    };

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(8),
        signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}

record RegisterRequest(string Email, string Password, string? Role);
record LoginRequest(string Email, string Password);

class UserRecord
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "player";
    public string PasswordHash { get; set; } = string.Empty;
}
