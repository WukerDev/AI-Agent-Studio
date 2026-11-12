using System.Text;
using System.Text.RegularExpressions;
using AgentStudio.Service.Application;
using AgentStudio.Service.Protos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentStudio.Service.Infrastructure;

public class SkillService : ISkillService
{
    private readonly ILogger<SkillService> logger;
    private readonly string skillsRootDirectory;

    public SkillService(IConfiguration configuration, ILogger<SkillService> logger)
    {
        this.logger = logger;
        this.skillsRootDirectory = ResolveSkillsRootDirectory(configuration);
        EnsureDirectoryAndSeedDefaults();
    }

    public string SkillsRootDirectory => this.skillsRootDirectory;

    public string NormalizeSkillName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        var trimmed = rawName.Trim();
        if (trimmed.StartsWith("Skill:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["Skill:".Length..].Trim();
        }

        var lower = trimmed.ToLowerInvariant()
            .Replace("ą", "a")
            .Replace("ć", "c")
            .Replace("ę", "e")
            .Replace("ł", "l")
            .Replace("ń", "n")
            .Replace("ó", "o")
            .Replace("ś", "s")
            .Replace("ź", "z")
            .Replace("ż", "z");

        var replaced = Regex.Replace(lower, @"[^a-z0-9]+", "-").Trim('-');
        if (replaced.Length > 64)
        {
            replaced = replaced[..64].TrimEnd('-');
        }

        return replaced;
    }

    public string? GetSkillDirectoryPath(string? skillName)
    {
        var normalized = NormalizeSkillName(skillName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var directPath = Path.Combine(this.skillsRootDirectory, normalized);
        if (Directory.Exists(directPath) && File.Exists(Path.Combine(directPath, "SKILL.md")))
        {
            return directPath;
        }

        if (Directory.Exists(this.skillsRootDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(this.skillsRootDirectory, "SKILL.md", SearchOption.AllDirectories))
            {
                var dir = Path.GetDirectoryName(file);
                if (dir != null && string.Equals(Path.GetFileName(dir), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return dir;
                }
            }
        }

        return directPath;
    }

    public async Task<IReadOnlyList<SkillItem>> GetSkillsAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectoryAndSeedDefaults();

        var results = new List<SkillItem>();
        if (!Directory.Exists(this.skillsRootDirectory))
        {
            return results;
        }

        var skillFiles = Directory.EnumerateFiles(this.skillsRootDirectory, "SKILL.md", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in skillFiles)
        {
            try
            {
                var rawText = await File.ReadAllTextAsync(filePath, cancellationToken);
                var folderName = Path.GetFileName(Path.GetDirectoryName(filePath) ?? string.Empty);
                var parsed = ParseSkillMarkdown(rawText, folderName);

                if (string.IsNullOrWhiteSpace(parsed.Name) || !seenNames.Add(parsed.Name))
                {
                    continue;
                }

                results.Add(new SkillItem
                {
                    Name = parsed.Name,
                    Description = parsed.Description,
                    Content = parsed.Body,
                    Path = $"skills/{parsed.Name}/SKILL.md"
                });
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not read SKILL.md at {FilePath}", filePath);
            }
        }

        return results
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<SkillItem?> GetSkillByNameAsync(string? skillName, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSkillName(skillName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var allSkills = await GetSkillsAsync(cancellationToken);
        return allSkills.FirstOrDefault(s => s.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<SkillItem> SaveSkillAsync(
        string name,
        string? description,
        string? content,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeSkillName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Skill name is required and must contain alphanumeric characters.", nameof(name));
        }

        if (!AgentSkillFrontmatter.ValidateName(normalizedName, out var nameFailureReason))
        {
            throw new ArgumentException($"Invalid skill name '{normalizedName}': {nameFailureReason}", nameof(name));
        }

        var parsedExisting = ParseSkillMarkdown(content ?? string.Empty, normalizedName);
        var resolvedDescription = !string.IsNullOrWhiteSpace(description)
            ? description.Trim()
            : (!string.IsNullOrWhiteSpace(parsedExisting.Description)
                ? parsedExisting.Description
                : $"Specialized Microsoft Agent Skill: {normalizedName}");

        resolvedDescription = Regex.Replace(resolvedDescription, @"[\r\n]+", " ").Trim();
        if (resolvedDescription.Length > 1024)
        {
            resolvedDescription = resolvedDescription[..1024];
        }

        if (!AgentSkillFrontmatter.ValidateDescription(resolvedDescription, out var descFailureReason))
        {
            throw new ArgumentException($"Invalid skill description: {descFailureReason}", nameof(description));
        }

        var bodyContent = !string.IsNullOrWhiteSpace(parsedExisting.Body)
            ? parsedExisting.Body.Trim()
            : $"# {normalizedName}\n\n{resolvedDescription}";

        var skillDir = Path.Combine(this.skillsRootDirectory, normalizedName);
        Directory.CreateDirectory(skillDir);

        var skillFilePath = Path.Combine(skillDir, "SKILL.md");
        var markdownFileContent = BuildSkillMarkdown(normalizedName, resolvedDescription, bodyContent);

        await File.WriteAllTextAsync(skillFilePath, markdownFileContent, new UTF8Encoding(false), cancellationToken);

        return new SkillItem
        {
            Name = normalizedName,
            Description = resolvedDescription,
            Content = bodyContent,
            Path = $"skills/{normalizedName}/SKILL.md"
        };
    }

    public Task<bool> DeleteSkillAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeSkillName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Task.FromResult(false);
        }

        var skillDir = Path.Combine(this.skillsRootDirectory, normalizedName);
        var fullSkillDir = Path.GetFullPath(skillDir);
        var fullRootDir = Path.GetFullPath(this.skillsRootDirectory);

        if (!fullSkillDir.StartsWith(fullRootDir, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullSkillDir, fullRootDir, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        if (Directory.Exists(fullSkillDir))
        {
            Directory.Delete(fullSkillDir, recursive: true);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public static string BuildSkillMarkdown(string normalizedName, string description, string body)
    {
        var escapedDesc = description.Replace("'", "''");
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {normalizedName}");
        sb.AppendLine($"description: '{escapedDesc}'");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(body.Trim());
        return sb.ToString();
    }

    public static (string Name, string Description, string Body) ParseSkillMarkdown(string rawMarkdown, string fallbackFolderName)
    {
        var name = fallbackFolderName;
        var description = string.Empty;
        var body = rawMarkdown ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rawMarkdown))
        {
            return (name, description, string.Empty);
        }

        var normalized = rawMarkdown.Replace("\r\n", "\n");
        if (normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            var endIdx = normalized.IndexOf("\n---", 4, StringComparison.Ordinal);
            if (endIdx > 0)
            {
                var frontmatterBlock = normalized[4..endIdx];
                var afterFrontmatter = normalized[(endIdx + 4)..];
                if (afterFrontmatter.StartsWith("\n", StringComparison.Ordinal))
                {
                    afterFrontmatter = afterFrontmatter[1..];
                }

                body = afterFrontmatter.Trim();

                foreach (var rawLine in frontmatterBlock.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = line["name:".Length..].Trim().Trim('\'', '"');
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            name = val;
                        }
                    }
                    else if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = line["description:".Length..].Trim().Trim('\'', '"').Replace("''", "'");
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            description = val;
                        }
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            var firstNonHeader = body
                .Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#'));
            description = !string.IsNullOrWhiteSpace(firstNonHeader)
                ? firstNonHeader
                : $"Agent skill {name}";
        }

        return (name, description, body);
    }

    private static string ResolveSkillsRootDirectory(IConfiguration configuration)
    {
        var configured = configuration["Skills:Directory"] ?? configuration["Skills:RootPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var currentDir = Directory.GetCurrentDirectory();
        var parentSkills = Path.Combine(currentDir, "..", "skills");
        if (Directory.Exists(parentSkills))
        {
            return Path.GetFullPath(parentSkills);
        }

        var localSkills = Path.Combine(currentDir, "skills");
        return Path.GetFullPath(localSkills);
    }

    private void EnsureDirectoryAndSeedDefaults()
    {
        try
        {
            Directory.CreateDirectory(this.skillsRootDirectory);

            SeedSkillIfMissing(
                "code-review",
                "Perform structured architecture, security, and code quality reviews for .NET and Vue/TypeScript projects. Use when asked to review code, audit security, or recommend refactoring.",
                """
                # Code Review & Architecture Skill

                When performing a code review or architectural analysis, follow these structured steps:

                1. **Security & Boundaries**: Verify authentication, authorization, input validation, and safe error handling.
                2. **Performance & Scalability**: Check async/await patterns, database query efficiency, and memory allocations.
                3. **Maintainability**: Ensure clear separation of concerns, strong typing, and concise naming.
                4. **Actionable Summary**: Present findings grouped into Critical, Warning, and Recommendation bullets.
                """);

            SeedSkillIfMissing(
                "sql-architect",
                "Design and optimize PostgreSQL schemas, B-Tree and JSONB GIN indexes, and Entity Framework Core queries. Use when asked about database design or query optimization.",
                """
                # PostgreSQL & EF Core Architect Skill

                Follow these database engineering guidelines:

                1. **Indexing Strategy**: Use B-Tree indexes for equality/range filters and GIN indexes for `jsonb` columns.
                2. **Entity Framework Core**: Prefer `AsNoTracking()` for read-only queries and avoid N+1 query patterns.
                3. **Schema Integrity**: Enforce primary keys, foreign key constraints, and explicit column types.
                """);

            SeedSkillIfMissing(
                "polish-executive-report",
                "Format technical responses as concise Polish executive summaries with key conclusions, bullet points, and next steps.",
                """
                # Raport Ekspercki (Polski)

                Formatuj odpowiedź zgodnie z poniższą strukturą:

                1. **Podsumowanie wykonawcze (TL;DR)** — 1-2 zwięzłe zdania z głównym wynikiem.
                2. **Kluczowe ustalenia i dane** — wypunktowana lista najważniejszych faktów technicznych lub wyników obliczeń.
                3. **Rekomendacje** — konkretne kolejne kroki techniczne.
                """);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Could not seed default skills directory at {SkillsRoot}", this.skillsRootDirectory);
        }
    }

    private void SeedSkillIfMissing(string name, string description, string body)
    {
        var dir = Path.Combine(this.skillsRootDirectory, name);
        var file = Path.Combine(dir, "SKILL.md");
        if (!File.Exists(file))
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(file, BuildSkillMarkdown(name, description, body), new UTF8Encoding(false));
        }
    }
}
