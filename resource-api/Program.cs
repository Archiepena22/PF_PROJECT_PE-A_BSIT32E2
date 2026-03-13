using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

var images = new List<ImageAsset>
{
    new() { Id = Guid.NewGuid().ToString(), Url = "https://placehold.co/300x300?text=Cat", Tags = new List<string> { "animal" } },
    new() { Id = Guid.NewGuid().ToString(), Url = "https://placehold.co/300x300?text=Dog", Tags = new List<string> { "animal" } },
    new() { Id = Guid.NewGuid().ToString(), Url = "https://placehold.co/300x300?text=Milk", Tags = new List<string> { "food" } },
    new() { Id = Guid.NewGuid().ToString(), Url = "https://placehold.co/300x300?text=Bone", Tags = new List<string> { "animal" } }
};

var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "animal", "food" };

var puzzles = new List<Puzzle>
{
    new()
    {
        Id = Guid.NewGuid().ToString(),
        Answer = "pet",
        Difficulty = "easy",
        ImageIds = images.Select(i => i.Id).ToList(),
        AcceptableAnswers = new List<string> { "pets" }
    }
};

var packs = new List<Pack>
{
    new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = "Starter Pack",
        Description = "Demo puzzles",
        Published = true,
        PuzzleIds = puzzles.Select(p => p.Id).ToList()
    }
};

var progressStore = new Dictionary<string, UserProgress>();

var starterPackId = packs[0].Id;
foreach (var puzzle in puzzles)
{
    puzzle.PackId = starterPackId;
}

app.MapGet("/packs", (HttpRequest request, ClaimsPrincipal user) =>
{
    var random = request.Query["random"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
    var published = packs.Where(p => p.Published).ToList();

    if (random)
    {
        published = published.OrderBy(_ => Random.Shared.Next()).ToList();
    }

    return Results.Ok(published.Select(p => new { p.Id, p.Name, p.Description }));
}).RequireAuthorization();

app.MapGet("/puzzles/next", (string packId, ClaimsPrincipal user) =>
{
    var pack = packs.FirstOrDefault(p => p.Id == packId && p.Published);
    if (pack == null)
    {
        return Results.NotFound();
    }

    var userId = GetUserId(user);
    var progress = GetProgress(progressStore, userId);
    var solved = progress.SolvedByPack.GetValueOrDefault(packId) ?? new HashSet<string>();

    var available = pack.PuzzleIds.Where(id => !solved.Contains(id)).ToList();
    if (available.Count == 0)
    {
        return Results.Ok(new { completed = true });
    }

    var nextPuzzleId = available[Random.Shared.Next(available.Count)];
    var puzzle = puzzles.First(p => p.Id == nextPuzzleId);
    var imageUrls = puzzle.ImageIds.Select(id => images.First(img => img.Id == id).Url).ToList();

    return Results.Ok(new
    {
        puzzleId = puzzle.Id,
        packId = pack.Id,
        images = imageUrls,
        hint = puzzle.Hint,
        difficulty = puzzle.Difficulty
    });
}).RequireAuthorization();

app.MapPost("/game/submit", (SubmitGuessRequest request, ClaimsPrincipal user) =>
{
    var puzzle = puzzles.FirstOrDefault(p => p.Id == request.PuzzleId);
    if (puzzle == null)
    {
        return Results.NotFound();
    }

    var normalizedGuess = NormalizeGuess(request.Guess);
    var normalizedAnswer = NormalizeGuess(puzzle.Answer);

    var isCorrect = normalizedGuess == normalizedAnswer || puzzle.AcceptableAnswers.Any(a => NormalizeGuess(a) == normalizedGuess);

    var userId = GetUserId(user);
    var progress = GetProgress(progressStore, userId);

    progress.Attempts += 1;
    var scoreDelta = isCorrect ? 10 : -1;
    progress.Score += scoreDelta;

    if (isCorrect)
    {
        if (!progress.SolvedByPack.TryGetValue(puzzle.PackId, out var solved))
        {
            solved = new HashSet<string>();
            progress.SolvedByPack[puzzle.PackId] = solved;
        }

        solved.Add(puzzle.Id);
        progress.SolvedTotal = progress.SolvedByPack.Values.Sum(set => set.Count);
    }

    var pack = packs.FirstOrDefault(p => p.Id == puzzle.PackId);
    var remaining = 0;
    if (pack != null)
    {
        var solvedSet = progress.SolvedByPack.GetValueOrDefault(pack.Id);
        remaining = pack.PuzzleIds.Count(id => solvedSet == null || !solvedSet.Contains(id));
    }

    return Results.Ok(new
    {
        correct = isCorrect,
        scoreDelta,
        nextAvailable = remaining > 0
    });
}).RequireAuthorization();

app.MapGet("/profile/progress", (ClaimsPrincipal user) =>
{
    var userId = GetUserId(user);
    var progress = GetProgress(progressStore, userId);

    return Results.Ok(new
    {
        solved = progress.SolvedTotal,
        attempts = progress.Attempts,
        score = progress.Score
    });
}).RequireAuthorization();

app.MapGet("/cms/images", (ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    return Results.Ok(images);
}).RequireAuthorization();

app.MapPost("/cms/images", (ImageCreateRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Url)) return Results.BadRequest();

    var image = new ImageAsset { Id = Guid.NewGuid().ToString(), Url = request.Url.Trim() };
    images.Add(image);
    return Results.Ok(image);
}).RequireAuthorization();

app.MapPut("/cms/images/{id}", (string id, ImageCreateRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var image = images.FirstOrDefault(i => i.Id == id);
    if (image == null) return Results.NotFound();
    image.Url = request.Url.Trim();
    return Results.Ok(image);
}).RequireAuthorization();

app.MapDelete("/cms/images/{id}", (string id, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var image = images.FirstOrDefault(i => i.Id == id);
    if (image == null) return Results.NotFound();
    images.Remove(image);
    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/cms/tags", (ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    return Results.Ok(tags);
}).RequireAuthorization();

app.MapPost("/cms/tags", (TagRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Tag)) return Results.BadRequest();
    tags.Add(request.Tag.Trim());
    return Results.Ok(tags);
}).RequireAuthorization();

app.MapDelete("/cms/tags", (TagRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Tag)) return Results.BadRequest();
    tags.Remove(request.Tag.Trim());
    return Results.Ok(tags);
}).RequireAuthorization();

app.MapPost("/cms/images/{id}/tags", (string id, TagRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var image = images.FirstOrDefault(i => i.Id == id);
    if (image == null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Tag)) return Results.BadRequest();
    var tag = request.Tag.Trim();
    image.Tags.Add(tag);
    tags.Add(tag);
    return Results.Ok(image);
}).RequireAuthorization();

app.MapDelete("/cms/images/{id}/tags/{tag}", (string id, string tag, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var image = images.FirstOrDefault(i => i.Id == id);
    if (image == null) return Results.NotFound();
    image.Tags.Remove(tag);
    return Results.Ok(image);
}).RequireAuthorization();

app.MapGet("/cms/packs", (ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    return Results.Ok(packs);
}).RequireAuthorization();

app.MapPost("/cms/packs", (PackCreateRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest();
    var pack = new Pack
    {
        Id = Guid.NewGuid().ToString(),
        Name = request.Name.Trim(),
        Description = request.Description,
        Published = request.Published,
        PuzzleIds = request.PuzzleIds?.ToList() ?? new List<string>()
    };
    packs.Add(pack);
    foreach (var puzzleId in pack.PuzzleIds)
    {
        var puzzle = puzzles.FirstOrDefault(p => p.Id == puzzleId);
        if (puzzle != null) puzzle.PackId = pack.Id;
    }
    return Results.Ok(pack);
}).RequireAuthorization();

app.MapPut("/cms/packs/{id}", (string id, PackCreateRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var pack = packs.FirstOrDefault(p => p.Id == id);
    if (pack == null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest();
    pack.Name = request.Name.Trim();
    pack.Description = request.Description;
    pack.Published = request.Published;
    pack.PuzzleIds = request.PuzzleIds?.ToList() ?? new List<string>();
    foreach (var puzzleId in pack.PuzzleIds)
    {
        var puzzle = puzzles.FirstOrDefault(p => p.Id == puzzleId);
        if (puzzle != null) puzzle.PackId = pack.Id;
    }
    return Results.Ok(pack);
}).RequireAuthorization();

app.MapDelete("/cms/packs/{id}", (string id, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var pack = packs.FirstOrDefault(p => p.Id == id);
    if (pack == null) return Results.NotFound();
    packs.Remove(pack);
    return Results.NoContent();
}).RequireAuthorization();

app.MapPost("/cms/packs/{id}/publish", (string id, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var pack = packs.FirstOrDefault(p => p.Id == id);
    if (pack == null) return Results.NotFound();
    pack.Published = !pack.Published;
    return Results.Ok(pack);
}).RequireAuthorization();

app.MapGet("/cms/puzzles", (ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    return Results.Ok(puzzles);
}).RequireAuthorization();

app.MapPost("/cms/puzzles", (PuzzleCreateRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (request.ImageIds == null || request.ImageIds.Count != 4) return Results.BadRequest();
    if (string.IsNullOrWhiteSpace(request.Answer)) return Results.BadRequest();

    var puzzle = new Puzzle
    {
        Id = Guid.NewGuid().ToString(),
        Answer = request.Answer.Trim(),
        Hint = request.Hint,
        Difficulty = request.Difficulty,
        ImageIds = request.ImageIds.ToList(),
        AcceptableAnswers = request.AcceptableAnswers?.ToList() ?? new List<string>()
    };

    puzzles.Add(puzzle);
    return Results.Ok(puzzle);
}).RequireAuthorization();

app.MapPut("/cms/puzzles/{id}", (string id, PuzzleCreateRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var puzzle = puzzles.FirstOrDefault(p => p.Id == id);
    if (puzzle == null) return Results.NotFound();
    if (request.ImageIds == null || request.ImageIds.Count != 4) return Results.BadRequest();

    puzzle.Answer = request.Answer.Trim();
    puzzle.Hint = request.Hint;
    puzzle.Difficulty = request.Difficulty;
    puzzle.ImageIds = request.ImageIds.ToList();
    puzzle.AcceptableAnswers = request.AcceptableAnswers?.ToList() ?? new List<string>();

    return Results.Ok(puzzle);
}).RequireAuthorization();

app.MapDelete("/cms/puzzles/{id}", (string id, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var puzzle = puzzles.FirstOrDefault(p => p.Id == id);
    if (puzzle == null) return Results.NotFound();
    puzzles.Remove(puzzle);
    return Results.NoContent();
}).RequireAuthorization();

app.Run();

static string GetUserId(ClaimsPrincipal user)
{
    return user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anon";
}

static bool IsAdmin(ClaimsPrincipal user)
{
    return user.IsInRole("admin") || user.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "admin");
}

static UserProgress GetProgress(Dictionary<string, UserProgress> store, string userId)
{
    if (!store.TryGetValue(userId, out var progress))
    {
        progress = new UserProgress();
        store[userId] = progress;
    }

    return progress;
}

static string NormalizeGuess(string input)
{
    var trimmed = input.Trim().ToLowerInvariant();
    return new string(trimmed.Where(c => c != ' ' && c != '-').ToArray());
}

record SubmitGuessRequest(string PuzzleId, string Guess);
record TagRequest(string Tag);
record ImageCreateRequest(string Url);

record PackCreateRequest(string Name, string? Description, bool Published, List<string>? PuzzleIds);
record PuzzleCreateRequest(string Answer, string? Hint, string? Difficulty, List<string> ImageIds, List<string>? AcceptableAnswers);

class ImageAsset
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

class Puzzle
{
    public string Id { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Hint { get; set; }
    public string? Difficulty { get; set; }
    public List<string> ImageIds { get; set; } = new();
    public List<string> AcceptableAnswers { get; set; } = new();
}

class Pack
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Published { get; set; }
    public List<string> PuzzleIds { get; set; } = new();
}

class UserProgress
{
    public int SolvedTotal { get; set; }
    public int Attempts { get; set; }
    public int Score { get; set; }
    public Dictionary<string, HashSet<string>> SolvedByPack { get; set; } = new();
}
