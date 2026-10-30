using Serilog.Core;
using Serilog.Events;

namespace Duyao.ApiBase.LoggerMisc;

/// <summary>
/// 简化 ActionName 的 Enricher - 移除命名空间前缀
/// </summary>
public class SimplifiedActionNameEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!logEvent.Properties.TryGetValue("ActionName", out var actionNameProperty))
            return;

        if (actionNameProperty is ScalarValue scalarValue && scalarValue.Value is string actionName)
        {
            // 简化格式：ScApi.Web.CoreApi.Controllers.PublicController.QueryRegion 
            // → PublicController.QueryRegion
            var simplifiedName = SimplifyActionName(actionName);

            var simplifiedProperty = propertyFactory.CreateProperty("ActionName", simplifiedName);
            logEvent.RemovePropertyIfPresent("ActionName");
            logEvent.AddPropertyIfAbsent(simplifiedProperty);
        }
    }

    /// <summary>
    /// 简化 ActionName，只保留控制器名和方法名
    /// </summary>
    private static string SimplifyActionName(string fullActionName)
    {
        if (string.IsNullOrEmpty(fullActionName))
            return fullActionName;
        fullActionName = RemoveTrailingParentheses(fullActionName);
        // 格式: ScApi.Web.CoreApi.Controllers.PublicController.QueryRegion
        var parts = fullActionName.Split('.');

        // 查找 "Controller" 关键字的位置
        var controllerIndex = -1;
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (parts[i].EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            {
                controllerIndex = i;
                break;
            }
        }

        // 如果找到 Controller，则取最后两部分 (ControllerName.MethodName)
        if (controllerIndex >= 0 && controllerIndex < parts.Length - 1)
        {
            return $"{parts[controllerIndex]}.{parts[controllerIndex + 1]}";
        }

        // 如果没有找到 Controller，则返回最后两部分
        if (parts.Length >= 2)
        {
            return $"{parts[parts.Length - 2]}.{parts[parts.Length - 1]}";
        }

        return fullActionName;
    }

    /// <summary>
    /// 去掉末尾的括号部分
    /// 例如：PublicController.QueryRegion (ScApi.Web.CoreApi) → PublicController.QueryRegion
    /// </summary>
    private static string RemoveTrailingParentheses(string input)
    {
        // 查找最后一个 ( 的位置
        var lastParenIndex = input.LastIndexOf('(');

        if (lastParenIndex > 0)
        {
            // 去掉 ( 之后的所有内容，并去掉末尾空格
            return input.Substring(0, lastParenIndex).Trim();
        }

        return input;
    }
}