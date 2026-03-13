using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

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

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("game-submit", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromSeconds(10);
        limiter.QueueLimit = 2;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("web-app");
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var seedImages = new List<ImageAsset>
{
    new() { Id = Guid.NewGuid().ToString(), Url = "https://placehold.co/300x300?text=Cat", Tags = new List<string> { "animal" } },
    new() { Id = Guid.NewGuid().ToString(), Url = "https://placehold.co/300x300?text=Dog", Tags = new List<string> { "animal" } },
    new() { Id = Guid.NewGuid().ToString(), Url = "https://placehold.co/300x300?text=Milk", Tags = new List<string> { "food" } },
    new() { Id = Guid.NewGuid().ToString(), Url = "https://placehold.co/300x300?text=Bone", Tags = new List<string> { "animal" } }
};

var seedPuzzles = new List<Puzzle>
{
    new()
    {
        Id = Guid.NewGuid().ToString(),
        Answer = "pet",
        Difficulty = "easy",
        ImageIds = seedImages.Select(i => i.Id).ToList(),
        AcceptableAnswers = new List<string> { "pets" }
    }
};

var seedPacks = new List<Pack>
{
    new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = "Starter Pack",
        Description = "Demo puzzles",
        Published = true,
        PuzzleIds = seedPuzzles.Select(p => p.Id).ToList()
    }
};

var dataPath = Path.Combine(app.Environment.ContentRootPath, "data", "store.json");
Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
var loaded = LoadStore(dataPath);

var images = loaded?.Images ?? seedImages;
var puzzles = loaded?.Puzzles ?? seedPuzzles;
var packs = loaded?.Packs ?? seedPacks;
var tags = loaded?.Tags != null
    ? new HashSet<string>(loaded.Tags, StringComparer.OrdinalIgnoreCase)
    : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "animal", "food" };
var progressStore = loaded?.ProgressStore ?? new Dictionary<string, UserProgress>();

if (packs.Count > 0)
{
    var starterPackId = packs[0].Id;
    foreach (var puzzle in puzzles)
    {
        if (string.IsNullOrWhiteSpace(puzzle.PackId))
        {
            puzzle.PackId = starterPackId;
        }
    }
}

var uploadRoot = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads");
Directory.CreateDirectory(uploadRoot);

void Persist()
{
    SaveStore(dataPath, new DataStore
    {
        Images = images,
        Puzzles = puzzles,
        Packs = packs,
        Tags = tags.ToList(),
        ProgressStore = progressStore
    });
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
    if (string.IsNullOrWhiteSpace(request.PuzzleId) || string.IsNullOrWhiteSpace(request.Guess))
    {
        return Results.BadRequest();
    }
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

    Persist();

    return Results.Ok(new
    {
        correct = isCorrect,
        scoreDelta,
        nextAvailable = remaining > 0
    });
}).RequireAuthorization().RequireRateLimiting("game-submit");

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

app.MapPost("/cms/images", async (HttpRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();

    if (request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("file");
        var urlField = form["url"].ToString();

        if (file is not null)
        {
            if (!IsAllowedImage(file.ContentType)) return Results.BadRequest(new { error = "Invalid file type." });
            if (file.Length > 2_000_000) return Results.BadRequest(new { error = "File too large." });

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadRoot, fileName);

            await using var stream = File.Create(filePath);
            await file.CopyToAsync(stream);

            var absoluteUrl = $"{request.Scheme}://{request.Host}/uploads/{fileName}";
            var image = new ImageAsset { Id = Guid.NewGuid().ToString(), Url = absoluteUrl };
            images.Add(image);
            Persist();
            return Results.Ok(image);
        }

        if (!string.IsNullOrWhiteSpace(urlField))
        {
            var image = new ImageAsset { Id = Guid.NewGuid().ToString(), Url = urlField.Trim() };
            images.Add(image);
            Persist();
            return Results.Ok(image);
        }

        return Results.BadRequest(new { error = "Provide a file or URL." });
    }

    var body = await request.ReadFromJsonAsync<ImageCreateRequest>();
    if (body == null || string.IsNullOrWhiteSpace(body.Url)) return Results.BadRequest();

    var imageFromUrl = new ImageAsset { Id = Guid.NewGuid().ToString(), Url = body.Url.Trim() };
    images.Add(imageFromUrl);
    Persist();
    return Results.Ok(imageFromUrl);
}).RequireAuthorization();

app.MapPut("/cms/images/{id}", (string id, ImageCreateRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var image = images.FirstOrDefault(i => i.Id == id);
    if (image == null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Url)) return Results.BadRequest();
    image.Url = request.Url.Trim();
    Persist();
    return Results.Ok(image);
}).RequireAuthorization();

app.MapDelete("/cms/images/{id}", (string id, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var image = images.FirstOrDefault(i => i.Id == id);
    if (image == null) return Results.NotFound();
    images.Remove(image);
    Persist();
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
    if (!IsValidTag(request.Tag)) return Results.BadRequest();
    tags.Add(request.Tag.Trim());
    Persist();
    return Results.Ok(tags);
}).RequireAuthorization();

app.MapDelete("/cms/tags", (TagRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (!IsValidTag(request.Tag)) return Results.BadRequest();
    tags.Remove(request.Tag.Trim());
    Persist();
    return Results.Ok(tags);
}).RequireAuthorization();

app.MapPost("/cms/images/{id}/tags", (string id, TagRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var image = images.FirstOrDefault(i => i.Id == id);
    if (image == null) return Results.NotFound();
    if (!IsValidTag(request.Tag)) return Results.BadRequest();
    var tag = request.Tag.Trim();
    image.Tags.Add(tag);
    tags.Add(tag);
    Persist();
    return Results.Ok(image);
}).RequireAuthorization();

app.MapDelete("/cms/images/{id}/tags/{tag}", (string id, string tag, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var image = images.FirstOrDefault(i => i.Id == id);
    if (image == null) return Results.NotFound();
    image.Tags.Remove(tag);
    Persist();
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
    if (!IsValidPuzzleSelection(request.PuzzleIds, puzzles)) return Results.BadRequest();
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
    Persist();
    return Results.Ok(pack);
}).RequireAuthorization();

app.MapPut("/cms/packs/{id}", (string id, PackCreateRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var pack = packs.FirstOrDefault(p => p.Id == id);
    if (pack == null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest();
    if (!IsValidPuzzleSelection(request.PuzzleIds, puzzles)) return Results.BadRequest();
    pack.Name = request.Name.Trim();
    pack.Description = request.Description;
    pack.Published = request.Published;
    pack.PuzzleIds = request.PuzzleIds?.ToList() ?? new List<string>();
    foreach (var puzzleId in pack.PuzzleIds)
    {
        var puzzle = puzzles.FirstOrDefault(p => p.Id == puzzleId);
        if (puzzle != null) puzzle.PackId = pack.Id;
    }
    Persist();
    return Results.Ok(pack);
}).RequireAuthorization();

app.MapDelete("/cms/packs/{id}", (string id, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var pack = packs.FirstOrDefault(p => p.Id == id);
    if (pack == null) return Results.NotFound();
    packs.Remove(pack);
    Persist();
    return Results.NoContent();
}).RequireAuthorization();

app.MapPost("/cms/packs/{id}/publish", (string id, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var pack = packs.FirstOrDefault(p => p.Id == id);
    if (pack == null) return Results.NotFound();
    pack.Published = !pack.Published;
    Persist();
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
    if (!IsValidAnswer(request.Answer)) return Results.BadRequest();
    if (!IsValidImageSelection(request.ImageIds, images)) return Results.BadRequest();
    if (!string.IsNullOrWhiteSpace(request.PackId) && IsDuplicateAnswerInPack(request.PackId, request.Answer, puzzles))
    {
        return Results.BadRequest(new { error = "Duplicate answer in pack." });
    }

    var puzzle = new Puzzle
    {
        Id = Guid.NewGuid().ToString(),
        Answer = request.Answer.Trim(),
        Hint = request.Hint,
        Difficulty = request.Difficulty,
        ImageIds = request.ImageIds.ToList(),
        AcceptableAnswers = request.AcceptableAnswers?.Select(a => a.Trim()).Where(a => !string.IsNullOrWhiteSpace(a)).ToList() ?? new List<string>(),
        PackId = request.PackId?.Trim() ?? string.Empty
    };

    puzzles.Add(puzzle);
    Persist();
    return Results.Ok(puzzle);
}).RequireAuthorization();

app.MapPut("/cms/puzzles/{id}", (string id, PuzzleCreateRequest request, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var puzzle = puzzles.FirstOrDefault(p => p.Id == id);
    if (puzzle == null) return Results.NotFound();
    if (!IsValidAnswer(request.Answer)) return Results.BadRequest();
    if (!IsValidImageSelection(request.ImageIds, images)) return Results.BadRequest();
    if (!string.IsNullOrWhiteSpace(request.PackId) && IsDuplicateAnswerInPack(request.PackId, request.Answer, puzzles, id))
    {
        return Results.BadRequest(new { error = "Duplicate answer in pack." });
    }

    puzzle.Answer = request.Answer.Trim();
    puzzle.Hint = request.Hint;
    puzzle.Difficulty = request.Difficulty;
    puzzle.ImageIds = request.ImageIds.ToList();
    puzzle.AcceptableAnswers = request.AcceptableAnswers?.Select(a => a.Trim()).Where(a => !string.IsNullOrWhiteSpace(a)).ToList() ?? new List<string>();
    puzzle.PackId = request.PackId?.Trim() ?? puzzle.PackId;

    Persist();
    return Results.Ok(puzzle);
}).RequireAuthorization();

app.MapDelete("/cms/puzzles/{id}", (string id, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var puzzle = puzzles.FirstOrDefault(p => p.Id == id);
    if (puzzle == null) return Results.NotFound();
    puzzles.Remove(puzzle);
    Persist();
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

static bool IsAllowedImage(string? contentType)
{
    return contentType is "image/jpeg" or "image/png" or "image/webp";
}

static bool IsValidTag(string? tag)
{
    return !string.IsNullOrWhiteSpace(tag) && tag.Trim().Length <= 32;
}

static bool IsValidAnswer(string? answer)
{
    return !string.IsNullOrWhiteSpace(answer) && answer.Trim().Length <= 32;
}

static bool IsValidImageSelection(List<string>? imageIds, List<ImageAsset> images)
{
    if (imageIds == null || imageIds.Count != 4) return false;
    if (imageIds.Distinct().Count() != 4) return false;
    return imageIds.All(id => images.Any(i => i.Id == id));
}

static bool IsValidPuzzleSelection(List<string>? puzzleIds, List<Puzzle> puzzles)
{
    if (puzzleIds == null) return true;
    if (puzzleIds.Distinct().Count() != puzzleIds.Count) return false;
    return puzzleIds.All(id => puzzles.Any(p => p.Id == id));
}

static bool IsDuplicateAnswerInPack(string packId, string answer, List<Puzzle> puzzles, string? ignoreId = null)
{
    var normalized = NormalizeGuess(answer);
    return puzzles.Any(p => p.PackId == packId && p.Id != ignoreId && NormalizeGuess(p.Answer) == normalized);
}

static DataStore? LoadStore(string path)
{
    if (!File.Exists(path)) return null;
    var json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<DataStore>(json);
}

static void SaveStore(string path, DataStore store)
{
    var json = JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(path, json);
}

record SubmitGuessRequest(string PuzzleId, string Guess);
record TagRequest(string Tag);
record ImageCreateRequest(string Url);

record PackCreateRequest(string Name, string? Description, bool Published, List<string>? PuzzleIds);
record PuzzleCreateRequest(string Answer, string? Hint, string? Difficulty, List<string> ImageIds, List<string>? AcceptableAnswers, string? PackId);

class DataStore
{
    public List<ImageAsset> Images { get; set; } = new();
    public List<Puzzle> Puzzles { get; set; } = new();
    public List<Pack> Packs { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, UserProgress> ProgressStore { get; set; } = new();
}

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
