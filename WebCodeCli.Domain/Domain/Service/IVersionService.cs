namespace WebCodeCli.Domain.Domain.Service;

/// <summary>
/// 版本检查结果
/// </summary>
public class VersionCheckResult
{
    /// <summary>
    /// 是否有更新
    /// </summary>
    public bool HasUpdate { get; set; }
    
    /// <summary>
    /// 当前版本
    /// </summary>
    public string CurrentVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// 最新版本
    /// </summary>
    public string LatestVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// GitHub Release 页面链接
    /// </summary>
    public string ReleaseUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// 更新日志
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;
    
    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? PublishedAt { get; set; }
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 版本服务接口
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// 获取当前版本号
    /// </summary>
    string GetCurrentVersion();
    
    /// <summary>
    /// 检查更新
    /// </summary>
    Task<VersionCheckResult> CheckForUpdateAsync();
}
