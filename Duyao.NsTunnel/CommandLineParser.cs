namespace Duyao.NsTunnel;


internal abstract class CommandLineParser
{
    public static Dictionary<string, object> ParseCommandLineArgs(string[] args
        , string license = ""
        , int logLevel = 1
    )
    {
        var parameters = new Dictionary<string, object>
        {
            { "license", license },
            { "logLevel", logLevel },
        };
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--license" && i + 1 < args.Length)
            {
                parameters["license"] = args[++i];
            }
            else if (arg == "--log-level" && i + 1 < args.Length)
            {
                parameters["logLevel"] = args[++i];
            }
        }

        return parameters;
    }
}