namespace WorkflowDashboard.Api.Services.Git;

public interface IGitService
{
    Task<bool> IsGitRepoAsync(string repoPath);
    Task<bool> HasCommitsAsync(string repoPath);
    Task<string?> DetectDefaultBranchAsync(string repoPath);
    Task<bool> BranchExistsAsync(string repoPath, string branchName);
    Task CreateAndCheckoutBranchAsync(string repoPath, string fromBranch, string newBranch);
    Task CheckoutBranchAsync(string repoPath, string branchName);
}
