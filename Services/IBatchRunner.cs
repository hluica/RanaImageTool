namespace RanaImageTool.Services;

public interface IBatchRunner
{
    /// <summary>
    /// 运行批量处理任务
    /// </summary>
    /// <param name="path">目标目录</param>
    /// <param name="extensions">需要匹配的扩展名</param>
    /// <param name="activityName">任务显示名称</param>
    /// <param name="action">对每个文件执行的动作</param>
    /// <returns>退出代码 (0 成功, 1 有错误)</returns>
    Task<int> RunBatchAsync(string? path, string[] extensions, string activityName, Action<string> action);
}
