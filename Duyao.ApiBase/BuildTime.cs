namespace Duyao.ApiBase;

public static class BuildInfo
{
    public static readonly string BuildTime;

    static BuildInfo()
    {
        BuildTime = $"♏ {{0}} {{1}} git {GIT_VERSION}";
    }

    public const string GIT_NONE = "none";
    public const string GIT_VERSION = "__commit_id__";
}