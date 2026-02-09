namespace RanaImageTool.Models;

// Load -> Process 阶段
public sealed record SourceJob(
    string OriginalFilePath,
    Stream SourceStream
);

// Process -> Save 阶段
public sealed record ProcessedJob(
    string OriginalFilePath,
    string TargetExtension,
    Stream ResultStream,
    bool ShouldDeleteOriginal
);

// Save -> Result 阶段
public sealed record BatchResult(
    string File,
    Exception? Exception
);
