using RanaImageTool.Models;

namespace RanaImageTool.Services;

public interface IBatchRunner
{
    Task<int> RunBatchAsync(
        string? path,
        string[] extensions,
        string activityName,
        Func<SourceJob, Task<ProcessedJob>> processAction);
}
