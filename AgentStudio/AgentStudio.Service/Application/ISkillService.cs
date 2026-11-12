using AgentStudio.Service.Protos;

namespace AgentStudio.Service.Application;

public interface ISkillService
{
    string SkillsRootDirectory { get; }

    string NormalizeSkillName(string? rawName);

    string? GetSkillDirectoryPath(string? skillName);

    Task<IReadOnlyList<SkillItem>> GetSkillsAsync(CancellationToken cancellationToken = default);

    Task<SkillItem?> GetSkillByNameAsync(string? skillName, CancellationToken cancellationToken = default);

    Task<SkillItem> SaveSkillAsync(
        string name,
        string? description,
        string? content,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSkillAsync(string name, CancellationToken cancellationToken = default);
}
