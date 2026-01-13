using TL;

namespace DY.Telegram;

public abstract class CommandLineParser
{
    public static Dictionary<string, string> ParseCommandLineArgs(string[] args)
    {
        var longParamNameList = new List<string>()
        {
            "config-file",
            "host"
        };
        var parameters = new Dictionary<string, string>
        {
            { "config-file", "config.json" },
        };
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var key = arg.Substring(2);
            if (arg.StartsWith("--") && longParamNameList.Contains(key))
            {
                parameters[key] = args[++i];
            }
        }

        return parameters;
    }
}