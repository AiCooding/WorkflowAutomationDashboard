using System.Diagnostics;
using System.Text;

namespace WorkflowDashboard.Api.Services.Git;

public sealed class GitService(ILogger<GitService> logger) : IGitService
{
    public async Task<bool> IsGitRepoAsync(string repoPath)
    {
        var (exit, _) = await RunGitAsync(repoPath, "rev-parse --git-dir");
        return exit == 0;
    }

    public async Task<bool> HasCommitsAsync(string repoPath)
    {
        var (exit, _) = await RunGitAsync(repoPath, "rev-parse HEAD");
        return exit == 0;
    }

    public async Task<string?> DetectDefaultBranchAsync(string repoPath)
    {
        // Try: git symbolic-ref refs/remotes/origin/HEAD
        var (exit, output) = await RunGitAsync(repoPath, "symbolic-ref refs/remotes/origin/HEAD");
        if (exit == 0 && !string.IsNullOrWhiteSpace(output))
        {
            // output like "refs/remotes/origin/main"
            var parts = output.Trim().Split('/');
            return parts[^1];
        }
        // Fallback: check if main or master exists
        var (mainExit, _) = await RunGitAsync(repoPath, "show-ref --verify --quiet refs/heads/main");
        if (mainExit == 0) return "main";
        var (masterExit, _) = await RunGitAsync(repoPath, "show-ref --verify --quiet refs/heads/master");
        if (masterExit == 0) return "master";
        // Last fallback: current branch
        var (branchExit, branchOut) = await RunGitAsync(repoPath, "branch --show-current");
        return branchExit == 0 && !string.IsNullOrWhiteSpace(branchOut) ? branchOut.Trim() : null;
    }

    public async Task<bool> BranchExistsAsync(string repoPath, string branchName)
    {
        var (exit, _) = await RunGitAsync(repoPath, $"show-ref --verify --quiet refs/heads/{branchName}");
        return exit == 0;
    }

    public async Task CreateAndCheckoutBranchAsync(string repoPath, string fromBranch, string newBranch)
    {
        await RunGitAsync(repoPath, $"checkout {fromBranch}");
        await RunGitAsync(repoPath, "pull --ff-only");
        var (exit, stderr) = await RunGitAsync(repoPath, $"checkout -b {newBranch}");
        if (exit != 0)
            throw new InvalidOperationException($"Failed to create branch '{newBranch}': {stderr}");
    }

    public async Task CheckoutBranchAsync(string repoPath, string branchName)
    {
        var (exit, stderr) = await RunGitAsync(repoPath, $"checkout {branchName}");
        if (exit != 0)
            throw new InvalidOperationException($"Failed to checkout branch '{branchName}': {stderr}");
    }

    private async Task<(int ExitCode, string Output)> RunGitAsync(string repoPath, string arguments)
    {
        var psi = new ProcessStartInfo("git")
        {
            Arguments = $"-C \"{repoPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;

        logger.LogDebug("git -C {Path} {Args} → exit {Code}", repoPath, arguments, process.ExitCode);
        return (process.ExitCode, string.IsNullOrWhiteSpace(output) ? error : output);
    }
}
