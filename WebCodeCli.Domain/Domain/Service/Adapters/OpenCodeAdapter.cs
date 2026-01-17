using System.Text;
using System.Text.Json;
using WebCodeCli.Domain.Domain.Model;

namespace WebCodeCli.Domain.Domain.Service.Adapters;

/// <summary>
/// OpenCode CLI工具适配器
/// 处理 OpenCode CLI 的命令构建和 JSON 事件流输出解析
/// 
/// OpenCode CLI 特性:
/// - 支持多种 AI 模型 (通过 models.dev 集成)
/// - MCP 服务器支持
/// - Agent 系统
/// - 会话管理 (--continue, --session)
/// - JSON 格式输出 (--format json)
/// 
/// 命令格式:
/// - 启动 TUI: opencode
/// - 非交互模式: opencode run [message..] --format json
/// - 继续会话: opencode run --session <id> [message..]
/// - 上次会话: opencode run --continue [message..]
/// - 附加文件: opencode run [message..] --file <path>
/// - 指定模型: opencode run [message..] --model <provider/model>
/// 
/// 配置说明:
/// - Command: "opencode"
/// - ArgumentTemplate: 留空或随意填写（适配器不使用此配置）
/// - 适配器会自动构建完整的命令格式
/// 
/// 注意: 与 ClaudeCodeAdapter 类似，此适配器完全控制命令格式，
///       忽略 ArgumentTemplate 配置以确保与官方 CLI 规范一致。
/// </summary>
public class OpenCodeAdapter : ICliToolAdapter
{
    public string[] SupportedToolIds => new[] { "opencode", "opencode-cli" };

    public bool SupportsStreamParsing => true;

    public bool CanHandle(CliToolConfig tool)
    {
        if (tool == null) return false;

        return SupportedToolIds.Any(id => 
            string.Equals(tool.Id, id, StringComparison.OrdinalIgnoreCase)) ||
               (tool.Command?.Contains("opencode", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public string BuildArguments(CliToolConfig tool, string prompt, CliSessionContext context)
    {
        var escapedPrompt = EscapeShellArgument(prompt);

        // OpenCode CLI 命令格式 (根据官方文档):
        // opencode run [message..] [options]
        
        var sb = new StringBuilder();
        sb.Append("run");
        
        // 添加会话恢复参数
        if (context.IsResume && !string.IsNullOrEmpty(context.CliThreadId))
        {
            // 使用 --session 恢复指定会话
            sb.Append($" --session {context.CliThreadId}");
        }
        else if (context.IsResume)
        {
            // 使用 --continue 继续上一个会话
            sb.Append(" --continue");
        }
        
        // 添加提示词 (作为位置参数)
        sb.Append($" \"{escapedPrompt}\"");
        
        // 添加 JSON 格式输出
        sb.Append(" --format json");
        
        return sb.ToString();
    }

    public CliOutputEvent? ParseOutputLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var trimmed = line.Trim();

        // OpenCode 的 --format json 输出 JSON 事件流
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            // 非 JSON 行，检查是否是错误信息
            var isError =
                trimmed.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("error", StringComparison.OrdinalIgnoreCase);

            if (isError)
            {
                return new CliOutputEvent
                {
                    EventType = "error",
                    IsError = true,
                    Content = trimmed,
                    Title = "错误"
                };
            }

            // 其他非 JSON 行作为普通输出
            return new CliOutputEvent
            {
                EventType = "raw",
                IsError = false,
                IsUnknown = false,
                Title = "输出",
                Content = trimmed
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            var outputEvent = new CliOutputEvent
            {
                RawJson = trimmed
            };

            // 解析事件类型
            if (root.TryGetProperty("type", out var typeElement))
            {
                outputEvent.EventType = typeElement.GetString() ?? "unknown";
            }

            // 解析会话ID (sessionID 字段)
            if (root.TryGetProperty("sessionID", out var sessionIdElement))
            {
                outputEvent.SessionId = sessionIdElement.GetString();
            }

            // 根据不同的事件类型解析内容
            // OpenCode 的 JSON 事件包含: type, timestamp, sessionID 等字段
            switch (outputEvent.EventType)
            {
                case "tool_use":
                    ParseToolUseEvent(root, outputEvent);
                    break;

                case "tool_result":
                    ParseToolResultEvent(root, outputEvent);
                    break;

                case "message":
                case "text":
                    ParseMessageEvent(root, outputEvent);
                    break;

                case "error":
                    ParseErrorEvent(root, outputEvent);
                    break;

                case "session_start":
                    ParseSessionStartEvent(root, outputEvent);
                    break;

                case "session_end":
                case "complete":
                    ParseCompleteEvent(root, outputEvent);
                    break;

                default:
                    outputEvent.IsUnknown = true;
                    outputEvent.Title = "未知事件";
                    // 尝试从 part 字段提取信息
                    if (root.TryGetProperty("part", out var partElement))
                    {
                        ParsePartElement(partElement, outputEvent);
                    }
                    break;
            }

            return outputEvent;
        }
        catch (JsonException)
        {
            // JSON 解析失败，返回原始内容
            return new CliOutputEvent
            {
                EventType = "raw",
                IsError = false,
                IsUnknown = false,
                Title = "输出",
                Content = trimmed
            };
        }
    }

    public string? ExtractSessionId(CliOutputEvent outputEvent)
    {
        return outputEvent.SessionId;
    }

    public string? ExtractAssistantMessage(CliOutputEvent outputEvent)
    {
        if (outputEvent.EventType == "message" || 
            outputEvent.EventType == "text" ||
            outputEvent.ItemType == "message")
        {
            return outputEvent.Content;
        }
        return null;
    }

    public string GetEventTitle(CliOutputEvent outputEvent)
    {
        return outputEvent.EventType switch
        {
            "session_start" => "会话开始",
            "message" or "text" => "AI 回复",
            "tool_use" => "工具调用",
            "tool_result" => "工具结果",
            "error" => "错误",
            "session_end" or "complete" => "完成",
            _ => outputEvent.Title ?? "输出"
        };
    }

    public string GetEventBadgeClass(CliOutputEvent outputEvent)
    {
        if (outputEvent.IsError)
            return "badge-error";

        return outputEvent.EventType switch
        {
            "session_start" => "badge-info",
            "message" or "text" => "badge-success",
            "tool_use" => "badge-primary",
            "tool_result" => "badge-secondary",
            "session_end" or "complete" => "badge-info",
            _ => "badge-default"
        };
    }

    public string GetEventBadgeLabel(CliOutputEvent outputEvent)
    {
        if (outputEvent.IsError)
            return "ERROR";

        return outputEvent.EventType switch
        {
            "session_start" => "START",
            "message" or "text" => "MESSAGE",
            "tool_use" => "TOOL",
            "tool_result" => "RESULT",
            "session_end" or "complete" => "DONE",
            _ => outputEvent.EventType?.ToUpperInvariant() ?? "INFO"
        };
    }

    #region Private Parsing Methods

    private void ParseSessionStartEvent(JsonElement root, CliOutputEvent outputEvent)
    {
        outputEvent.Title = "会话开始";
        outputEvent.Content = "OpenCode 会话已启动";
    }

    private void ParseMessageEvent(JsonElement root, CliOutputEvent outputEvent)
    {
        outputEvent.ItemType = "message";
        outputEvent.Title = "AI 回复";
        
        // 尝试从多个可能的字段提取消息内容
        if (root.TryGetProperty("content", out var contentElement))
        {
            outputEvent.Content = contentElement.GetString();
        }
        else if (root.TryGetProperty("text", out var textElement))
        {
            outputEvent.Content = textElement.GetString();
        }
        else if (root.TryGetProperty("part", out var partElement) &&
                 partElement.TryGetProperty("content", out var partContentElement))
        {
            outputEvent.Content = partContentElement.GetString();
        }
    }

    private void ParseToolUseEvent(JsonElement root, CliOutputEvent outputEvent)
    {
        outputEvent.ItemType = "tool_use";
        outputEvent.Title = "工具调用";
        
        var sb = new StringBuilder();
        
        // 从 part 字段提取工具信息
        if (root.TryGetProperty("part", out var partElement))
        {
            if (partElement.TryGetProperty("tool", out var toolElement))
            {
                sb.AppendLine($"工具: {toolElement.GetString()}");
            }
            
            if (partElement.TryGetProperty("state", out var stateElement))
            {
                if (stateElement.TryGetProperty("title", out var titleElement))
                {
                    sb.AppendLine($"操作: {titleElement.GetString()}");
                }
                
                if (stateElement.TryGetProperty("input", out var inputElement))
                {
                    sb.AppendLine($"参数: {inputElement.ToString()}");
                }
            }
        }
        
        outputEvent.Content = sb.ToString().TrimEnd();
    }

    private void ParseToolResultEvent(JsonElement root, CliOutputEvent outputEvent)
    {
        outputEvent.ItemType = "tool_result";
        outputEvent.Title = "工具结果";
        
        if (root.TryGetProperty("result", out var resultElement))
        {
            outputEvent.Content = resultElement.ToString();
        }
        else if (root.TryGetProperty("part", out var partElement))
        {
            if (partElement.TryGetProperty("state", out var stateElement))
            {
                if (stateElement.TryGetProperty("output", out var outputElement))
                {
                    outputEvent.Content = outputElement.GetString();
                }
                
                if (stateElement.TryGetProperty("status", out var statusElement))
                {
                    var status = statusElement.GetString();
                    if (status == "error" || status == "failed")
                    {
                        outputEvent.IsError = true;
                    }
                }
            }
        }
    }

    private void ParseErrorEvent(JsonElement root, CliOutputEvent outputEvent)
    {
        outputEvent.IsError = true;
        outputEvent.Title = "错误";
        
        if (root.TryGetProperty("error", out var errorElement))
        {
            outputEvent.Content = errorElement.GetString();
        }
        else if (root.TryGetProperty("message", out var messageElement))
        {
            outputEvent.Content = messageElement.GetString();
        }
    }

    private void ParseCompleteEvent(JsonElement root, CliOutputEvent outputEvent)
    {
        outputEvent.Title = "完成";
        outputEvent.Content = "任务执行完成";
    }

    private void ParsePartElement(JsonElement partElement, CliOutputEvent outputEvent)
    {
        // part 元素通常包含工具调用的详细信息
        if (partElement.TryGetProperty("type", out var typeElement))
        {
            outputEvent.ItemType = typeElement.GetString();
        }
        
        if (partElement.TryGetProperty("content", out var contentElement))
        {
            outputEvent.Content = contentElement.GetString();
        }
        
        outputEvent.Title = outputEvent.ItemType ?? "输出";
    }

    private static string EscapeShellArgument(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "\"\"";

        // 转义双引号和反斜杠
        var escaped = input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");

        return escaped;
    }

    #endregion
}
