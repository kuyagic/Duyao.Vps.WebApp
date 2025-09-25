namespace Duyao.ApiBase; 
public static class BuildInfo
{
    public static readonly string BuildTime;
    static BuildInfo()
    {
        BuildTime = "♏ {0} {1}";
    }
}