namespace Duyao.NsTunnel;

internal abstract class CommandLineParser
{
    public static Dictionary<string, object?> ParseCommandLineArgs(string[] args
        , string license = ""
        , int logLevel = 1
        , string? netns = null
        , string? godstring = null
    )
    {
        var parameters = new Dictionary<string, object?>
        {
            { "license", license },
            { "logLevel", logLevel },
            { "netns", null },
            { "godstring", "" }
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
            else if (arg == "--netns" && i + 1 < args.Length)
            {
                parameters["netns"] = args[++i];
            }
            else if (arg == "--godstring" && i + 1 < args.Length)
            {
                parameters["godstring"] = args[++i];
            }
        }

        return parameters;
    }
}