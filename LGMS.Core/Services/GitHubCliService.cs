using System.Text.Json;

namespace LGMS.Services;

public sealed record GitHubAccountInfo(
    string Host,
    string Username,
    bool IsActive);

public sealed record GitHubRepositoryInfo(
    string Name,
    string Owner,
    string Url,
    bool IsPrivate,
    string? DefaultBranch);

public sealed class GitHubCliService
{
    private readonly ProcessRunner _runner;
    private readonly GitAccountOperationLock _accountLock;
    private const string Gh = "gh";

    public GitHubCliService(ProcessRunner runner, GitAccountOperationLock accountLock)
    {
        _runner = runner;
        _accountLock = accountLock;
    }

    public async Task<List<GitHubAccountInfo>> GetAccountsAsync(CancellationToken ct = default)
    {
        var result = await _runner.RunAsync(
            Gh,
            ["auth", "status", "--json", "hosts"],
            cancellationToken: ct,
            timeout: TimeSpan.FromSeconds(15));

        if (!result.Success)
        {
            // If gh auth status returns non-zero because not logged in, return empty or check error
            if (result.Error.Contains("not logged in") || string.IsNullOrWhiteSpace(result.Output))
            {
                return new List<GitHubAccountInfo>();
            }
            throw new InvalidOperationException($"Failed to query GitHub CLI accounts: {result.Error}");
        }

        using var document = JsonDocument.Parse(result.Output);
        var accounts = new List<GitHubAccountInfo>();

        if (document.RootElement.TryGetProperty("hosts", out var hostsElement) &&
            hostsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var host in hostsElement.EnumerateObject())
            {
                if (host.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var account in host.Value.EnumerateArray())
                    {
                        var username = account.TryGetProperty("login", out var loginProp)
                            ? loginProp.GetString()
                            : null;

                        if (string.IsNullOrWhiteSpace(username)) continue;

                        bool isActive = account.TryGetProperty("active", out var activeProp) &&
                                        activeProp.ValueKind == JsonValueKind.True;

                        accounts.Add(new GitHubAccountInfo(host.Name, username, isActive));
                    }
                }
            }
        }

        return accounts;
    }

    public async Task SwitchAccountAsync(string host, string username, CancellationToken ct = default)
    {
        ValidateHost(host);
        ValidateUsername(username);

        var result = await _runner.RunAsync(
            Gh,
            ["auth", "switch", "--hostname", host, "--user", username],
            cancellationToken: ct,
            timeout: TimeSpan.FromSeconds(15));

        if (!result.Success)
        {
            throw new InvalidOperationException($"GitHub CLI switch account failed: {result.Error}");
        }
    }

    public async Task<List<GitHubRepositoryInfo>> ListRepositoriesAsync(
        string host,
        string username,
        CancellationToken ct = default)
    {
        return await _accountLock.ExecuteAsync(async token =>
        {
            await SwitchAccountAsync(host, username, token);

            var result = await _runner.RunAsync(
                Gh,
                ["repo", "list", "--limit", "200", "--json", "name,owner,url,isPrivate,defaultBranchRef"],
                cancellationToken: token,
                timeout: TimeSpan.FromSeconds(30));

            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to list repositories: {result.Error}");
            }

            using var document = JsonDocument.Parse(result.Output);
            var repositories = new List<GitHubRepositoryInfo>();

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var repo in document.RootElement.EnumerateArray())
                {
                    string name = repo.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                    string owner = "";
                    if (repo.TryGetProperty("owner", out var ownerProp) &&
                        ownerProp.TryGetProperty("login", out var ownerLoginProp))
                    {
                        owner = ownerLoginProp.GetString() ?? "";
                    }

                    string url = repo.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                    bool isPrivate = repo.TryGetProperty("isPrivate", out var privProp) && privProp.GetBoolean();

                    string? defaultBranch = null;
                    if (repo.TryGetProperty("defaultBranchRef", out var branchProp) &&
                        branchProp.ValueKind == JsonValueKind.Object &&
                        branchProp.TryGetProperty("name", out var branchNameProp))
                    {
                        defaultBranch = branchNameProp.GetString();
                    }

                    repositories.Add(new GitHubRepositoryInfo(name, owner, url, isPrivate, defaultBranch));
                }
            }

            return repositories;
        }, ct);
    }

    public async Task<T> ExecuteForAccountAsync<T>(
        string host,
        string username,
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct = default)
    {
        return await _accountLock.ExecuteAsync(async token =>
        {
            await SwitchAccountAsync(host, username, token);
            return await action(token);
        }, ct);
    }

    public async Task ExecuteForAccountAsync(
        string host,
        string username,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        await _accountLock.ExecuteAsync(async token =>
        {
            await SwitchAccountAsync(host, username, token);
            await action(token);
        }, ct);
    }

    public async Task<ProcessExecutionResult> SetupGitCredentialsAsync(CancellationToken ct = default)
    {
        return await _runner.RunAsync(Gh, ["auth", "setup-git"], cancellationToken: ct);
    }

    public async Task<ProcessExecutionResult> CreateRepositoryAsync(
        string host,
        string username,
        string repoName,
        string description,
        bool isPrivate,
        string? localSourcePath = null,
        CancellationToken ct = default)
    {
        ValidateHost(host);
        ValidateUsername(username);

        return await _accountLock.ExecuteAsync(async token =>
        {
            await SwitchAccountAsync(host, username, token);

            var args = new List<string>
            {
                "repo",
                "create",
                repoName,
                isPrivate ? "--private" : "--public"
            };

            if (!string.IsNullOrWhiteSpace(description))
            {
                args.Add("--description");
                args.Add(description);
            }

            if (!string.IsNullOrWhiteSpace(localSourcePath) && Directory.Exists(localSourcePath))
            {
                args.Add("--source");
                args.Add(".");
                args.Add("--remote");
                args.Add("origin");
                return await _runner.RunAsync(Gh, args, workingDirectory: localSourcePath, cancellationToken: token);
            }

            return await _runner.RunAsync(Gh, args, cancellationToken: token);
        }, ct);
    }

    private static void ValidateHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Contains('/') || host.Contains('\\') || host.Contains(' ') || host.Contains('@'))
        {
            throw new ArgumentException("Invalid GitHub host.");
        }
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 100 || username.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
        {
            throw new ArgumentException("Invalid GitHub username.");
        }
    }
}
