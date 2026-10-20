using LGMS.Models;

namespace LGMS.Services;

/// <summary>
/// Service responsible for auto-generating Git commit messages using DeepSeek AI,
/// analyzing diffs, staged files, last commit vs first commit context, and limitations.
/// </summary>
public interface IDeepSeekCommitService : IAICommitService
{
}
