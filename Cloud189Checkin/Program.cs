using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// ═══ Constants ═══
const string WebUrl = "https://cloud.189.cn";
const string AuthUrl = "https://open.e.189.cn";
const string ApiUrl = "https://api.cloud.189.cn";
const string AppId = "8025431004";
const string AccountType = "02";
const string ClientType = "10020";
const string ReturnUrl = "https://m.cloud.189.cn/zhuanti/2020/loginErrorPc/index.html";
const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.88 Safari/537.36";
const string TokenDir = ".token";
const string ConfigVersion = "9.0.6";
const string ConfigModel = "KB2000";

var accounts = LoadAccounts();
if (accounts.Count == 0)
{
    Console.WriteLine("[ERROR] No accounts configured. Set TY_ACCOUNTS JSON or TY_USERNAME_1/TY_PASSWORD_1 env vars.");
    return 1;
}

var verbose = Environment.GetEnvironmentVariable("CLOUD189_VERBOSE") == "1";
Directory.CreateDirectory(TokenDir);
Console.WriteLine($"[INFO] Created token directory: {Path.GetFullPath(TokenDir)}");

using var handler = new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    AllowAutoRedirect = true
};
using var httpClient = new HttpClient(new HttpLoggingHandler(handler, verbose));
httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json;charset=UTF-8");
httpClient.Timeout = TimeSpan.FromSeconds(30);

if (verbose) Console.WriteLine("[DEBUG] HttpClient configured | AllowAutoRedirect=true");

var userSizeInfoMap = new Dictionary<string, UserSizeSnapshot>();

foreach (var (userName, password) in accounts)
{
    var maskedUser = Mask(userName, 3, 7);
    var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    try
    {
        Console.WriteLine($"[INFO] [{maskedUser}] ========== Starting ==========");
        var tokenPath = Path.Combine(TokenDir, $"{userName}.json");
        var tokenStore = new FileTokenStore(tokenPath, verbose);

        Console.WriteLine($"[INFO] [{maskedUser}] Step 1: Get or create session...");
        var session = await GetOrCreateSession(httpClient, userName, password, tokenStore, verbose);

        Console.WriteLine($"[INFO] [{maskedUser}] Step 2: Perform user sign-in...");
        var signResult = await UserSign(httpClient, session.SessionKey, verbose);

        if (signResult.IsSign)
            Console.WriteLine($"[INFO] [{maskedUser}] Already signed in today, no bonus.");
        else
            Console.WriteLine($"[INFO] [{maskedUser}] Sign-in success! +{signResult.NetdiskBonus}M space");

        Console.WriteLine($"[INFO] [{maskedUser}] Step 3: Query capacity (before)...");
        var beforeSize = await GetUserSizeInfo(httpClient, session.SessionKey, verbose);
        userSizeInfoMap[userName] = new UserSizeSnapshot { SessionKey = session.SessionKey, BeforeSize = beforeSize };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] [{maskedUser}] {ex.Message}");
        if (verbose) Console.WriteLine($"[DEBUG] [{maskedUser}] {ex}");
    }

    var elapsed = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - before) / 1000.0;
    Console.WriteLine($"[INFO] [{maskedUser}] ========== Done in {elapsed:F2}s ==========");
}

// ═══ Summary ═══
Console.WriteLine();
Console.WriteLine("[INFO] ========== Capacity Summary ==========");
foreach (var (userName, snapshot) in userSizeInfoMap)
{
    try
    {
        var afterSize = await GetUserSizeInfo(httpClient, snapshot.SessionKey, verbose);
        var cloudDelta = (afterSize.CloudCapacityInfo.TotalSize - snapshot.BeforeSize.CloudCapacityInfo.TotalSize) / 1024.0 / 1024.0;
        var cloudTotal = afterSize.CloudCapacityInfo.TotalSize / 1024.0 / 1024.0 / 1024.0;
        var familyDelta = (afterSize.FamilyCapacityInfo.TotalSize - snapshot.BeforeSize.FamilyCapacityInfo.TotalSize) / 1024.0 / 1024.0;
        var familyTotal = afterSize.FamilyCapacityInfo.TotalSize / 1024.0 / 1024.0 / 1024.0;
        Console.WriteLine($"[INFO] [{Mask(userName, 3, 7)}] Personal: \u2191{cloudDelta:F2}M / {cloudTotal:F2}G  Family: \u2191{familyDelta:F2}M / {familyTotal:F2}G");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] [{Mask(userName, 3, 7)}] Capacity check: {ex.Message}");
    }
}

Console.WriteLine("[INFO] All accounts processed.");
return 0;

// ═══════════════════════════════════════════════════════
// Local functions
// ═══════════════════════════════════════════════════════

List<Account> LoadAccounts()
{
    var result = new List<Account>();
    var tyAccounts = Environment.GetEnvironmentVariable("TY_ACCOUNTS");
    if (!string.IsNullOrEmpty(tyAccounts))
    {
        try
        {
            using var doc = JsonDocument.Parse(tyAccounts);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var u = JsonHelper.GetString(item, "userName");
                var p = JsonHelper.GetString(item, "password");
                if (!string.IsNullOrEmpty(u) && !string.IsNullOrEmpty(p))
                    result.Add(new Account(u, p));
            }
            Console.WriteLine($"[INFO] Loaded {result.Count} account(s) from TY_ACCOUNTS env var.");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Failed to parse TY_ACCOUNTS JSON: {ex.Message}");
        }
    }

    for (int i = 1; ; i++)
    {
        var u = Environment.GetEnvironmentVariable($"TY_USERNAME_{i}");
        var p = Environment.GetEnvironmentVariable($"TY_PASSWORD_{i}");
        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p)) break;
        result.Add(new Account(u, p));
    }
    Console.WriteLine($"[INFO] Loaded {result.Count} account(s) from TY_USERNAME_N/TY_PASSWORD_N env vars.");
    return result;
}

async Task<SessionInfo> GetOrCreateSession(HttpClient client, string userName, string password,
    FileTokenStore tokenStore, bool verbose)
{
    var cached = tokenStore.Get();
    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // Strategy 1: cached access token
    if (cached is { AccessToken: not "" } t && t.ExpiresIn > now)
    {
        if (verbose) Console.WriteLine($"[DEBUG] Trying cached access token (expires in {t.ExpiresIn - now}ms)...");
        try { return await LoginByAccessToken(client, t.AccessToken); }
        catch (Exception ex)
        {
            if (verbose) Console.WriteLine($"[DEBUG] Access token reuse failed: {ex.Message}");
        }
    }

    // Strategy 2: refresh token
    if (cached is { RefreshToken: not "" } rt)
    {
        if (verbose) Console.WriteLine("[DEBUG] Trying refresh token...");
        try
        {
            var (newAt, newRt, expires) = await RefreshToken(client, rt.RefreshToken);
            tokenStore.Update(newAt, newRt, now + expires * 1000);
            return await LoginByAccessToken(client, newAt);
        }
        catch (Exception ex)
        {
            if (verbose) Console.WriteLine($"[DEBUG] Refresh token failed: {ex.Message}");
        }
    }

    // Strategy 3: full password login
    if (verbose) Console.WriteLine("[DEBUG] Full password login...");
    return await LoginByPassword(client, userName, password, tokenStore, verbose);
}

async Task<SessionInfo> LoginByAccessToken(HttpClient client, string accessToken)
{
    var rand = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var url = $"{ApiUrl}/getSessionForPC.action?appId={AppId}&clientType=TELEPC&version=6.2&channelId=web_cloud.189.cn&rand={rand}&accessToken={Uri.EscapeDataString(accessToken)}";
    using var resp = await client.PostAsync(url, null);
    await CheckResponseAsync(resp, url);
    using var doc = await JsonDocFromResp(resp);
    var root = doc.RootElement;
    if (JsonHelper.GetInt(root, "res_code") != 0)
        throw new Exception($"getSessionForPC failed: {JsonHelper.GetString(root, "res_message")}");
    return new SessionInfo(
        JsonHelper.GetString(root, "sessionKey"),
        JsonHelper.GetString(root, "accessToken"),
        JsonHelper.GetString(root, "refreshToken"));
}

async Task<(string AccessToken, string RefreshToken, int ExpiresIn)> RefreshToken(HttpClient client, string refreshToken)
{
    Console.WriteLine("[INFO] Refreshing access token...");
    var content = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["clientId"] = AppId, ["refreshToken"] = refreshToken,
        ["grantType"] = "refresh_token", ["format"] = "json"
    });
    var url = $"{AuthUrl}/api/oauth2/refreshToken.do";
    using var resp = await client.PostAsync(url, content);
    await CheckResponseAsync(resp, url);
    using var doc = await JsonDocFromResp(resp);
    var r = doc.RootElement;
    var accessToken = JsonHelper.GetString(r, "accessToken");
    var newRefreshToken = JsonHelper.GetString(r, "refreshToken");
    var expiresIn = JsonHelper.GetInt(r, "expiresIn");
    Console.WriteLine($"[INFO] Token refreshed, expires in {expiresIn}s");
    return (accessToken, newRefreshToken, expiresIn);
}

async Task<SessionInfo> LoginByPassword(HttpClient client, string userName, string password,
    FileTokenStore tokenStore, bool verbose)
{
    // Step 1: encryptConf
    var encUrl = $"{AuthUrl}/api/logbox/config/encryptConf.do";
    Console.WriteLine($"[INFO] 1/5  Fetching RSA public key...");
    using var er = await client.PostAsync(encUrl, null);
    await CheckResponseAsync(er, encUrl);
    using var ed = await JsonDocFromResp(er);
    var data = ed.RootElement.TryGetProperty("data", out var d) ? d : ed.RootElement;
    var pubKey = JsonHelper.GetString(data, "pubKey");
    var pre = JsonHelper.GetString(data, "pre");
    if (string.IsNullOrEmpty(pubKey)) throw new Exception("No RSA public key in encryptConf response");
    if (verbose) Console.WriteLine($"[DEBUG] Got pubKey (len={pubKey.Length}) pre='{pre}'");

    // Step 2: login form
    Console.WriteLine($"[INFO] 2/5  Fetching login form params...");
    var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var formUrl = $"{WebUrl}/api/portal/unifyLoginForPC.action?appId={AppId}&clientType={ClientType}&returnURL={Uri.EscapeDataString(ReturnUrl)}&timeStamp={ts}";
    using var fr = await client.GetAsync(formUrl);
    await CheckResponseAsync(fr, formUrl);
    var html = await fr.Content.ReadAsStringAsync();

    var captchaToken = Regex.Match(html, "'captchaToken' value='([^']+)'").Groups[1].Value;
    var lt = Regex.Match(html, "lt = \"([^\"]+)\"").Groups[1].Value;
    var paramId = Regex.Match(html, "paramId = \"([^\"]+)\"").Groups[1].Value;
    var reqId = Regex.Match(html, "reqId = \"([^\"]+)\"").Groups[1].Value;
    if (string.IsNullOrEmpty(captchaToken)) throw new Exception("Failed to parse login form: captchaToken not found");
    if (verbose) Console.WriteLine($"[DEBUG] captchaToken={captchaToken[..Math.Min(20, captchaToken.Length)]}... lt={lt[..Math.Min(10, lt.Length)]}...");

    // Step 3: RSA encrypt
    Console.WriteLine($"[INFO] 3/5  Encrypting credentials...");
    var encUser = RsaEncrypt(pubKey, userName);
    var encPass = RsaEncrypt(pubKey, password);

    // Step 4: login submit
    Console.WriteLine($"[INFO] 4/5  Submitting login...");
    using var lr = new HttpRequestMessage(HttpMethod.Post, $"{AuthUrl}/api/logbox/oauth2/loginSubmit.do");
    lr.Headers.TryAddWithoutValidation("Referer", AuthUrl);
    lr.Headers.TryAddWithoutValidation("lt", lt);
    lr.Headers.TryAddWithoutValidation("REQID", reqId);
    lr.Content = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["appKey"] = AppId, ["accountType"] = AccountType, ["validateCode"] = "",
        ["captchaToken"] = captchaToken, ["dynamicCheck"] = "FALSE", ["clientType"] = "1",
        ["cb_SaveName"] = "3", ["isOauth2"] = "false", ["returnUrl"] = ReturnUrl,
        ["paramId"] = paramId, ["userName"] = $"{pre}{encUser}", ["password"] = $"{pre}{encPass}"
    });
    using var lresp = await client.SendAsync(lr);
    await CheckResponseAsync(lresp, lr.RequestUri!.ToString());
    var lbody = await lresp.Content.ReadAsStringAsync();
    if (verbose) Console.WriteLine($"[DEBUG] loginSubmit response: {lbody[..Math.Min(500, lbody.Length)]}");
    using var ldoc = JsonDocument.Parse(lbody);
    var toUrl = JsonHelper.GetString(ldoc.RootElement, "toUrl");
    if (string.IsNullOrEmpty(toUrl))
    {
        // Login failed — dump full response for debugging
        Console.WriteLine($"[ERROR] loginSubmit rejected. Full response:");
        Console.WriteLine(lbody);
        // Check for known error patterns
        var resCode = JsonHelper.GetString(ldoc.RootElement, "res_code");
        var resMsg = JsonHelper.GetString(ldoc.RootElement, "res_message");
        var msg = JsonHelper.GetString(ldoc.RootElement, "msg");
        var errorMsg = JsonHelper.GetString(ldoc.RootElement, "errorMsg");
        if (!string.IsNullOrEmpty(resCode)) Console.WriteLine($"[ERROR] res_code={resCode}");
        if (!string.IsNullOrEmpty(resMsg)) Console.WriteLine($"[ERROR] res_message={resMsg}");
        if (!string.IsNullOrEmpty(msg)) Console.WriteLine($"[ERROR] msg={msg}");
        if (!string.IsNullOrEmpty(errorMsg)) Console.WriteLine($"[ERROR] errorMsg={errorMsg}");
        throw new Exception("Login failed: no redirect URL (toUrl is empty). See response above.");
    }
    if (verbose) Console.WriteLine($"[DEBUG] Login submit OK, toUrl={toUrl[..Math.Min(80, toUrl.Length)]}...");

    // Step 5: getSessionForPC
    Console.WriteLine($"[INFO] 5/5  Getting session...");
    var sUrl = $"{ApiUrl}/getSessionForPC.action?appId={AppId}&clientType=TELEPC&version=6.2&channelId=web_cloud.189.cn&rand={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}&redirectURL={Uri.EscapeDataString(toUrl)}";
    using var sr = await client.PostAsync(sUrl, null);
    await CheckResponseAsync(sr, sUrl);
    using var sd = await JsonDocFromResp(sr);
    var r = sd.RootElement;
    if (JsonHelper.GetInt(r, "res_code") != 0)
        throw new Exception($"getSessionForPC failed: res_code={JsonHelper.GetInt(r, "res_code")} {JsonHelper.GetString(r, "res_message")}");

    var sessionKey = JsonHelper.GetString(r, "sessionKey");
    var accessToken = JsonHelper.GetString(r, "accessToken");
    var refreshToken = JsonHelper.GetString(r, "refreshToken");
    if (string.IsNullOrEmpty(sessionKey)) throw new Exception("No sessionKey in response");

    tokenStore.Update(accessToken, refreshToken,
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 6L * 24 * 3600 * 1000);

    Console.WriteLine($"[INFO] Login successful, session obtained.");
    if (verbose) Console.WriteLine($"[DEBUG] sessionKey={sessionKey[..Math.Min(16, sessionKey.Length)]}...");
    return new SessionInfo(sessionKey, accessToken, refreshToken);
}

async Task<SignResult> UserSign(HttpClient client, string sessionKey, bool verbose)
{
    var rand = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var url = $"{WebUrl}/mkt/userSign.action?rand={rand}&clientType=TELEANDROID&version={ConfigVersion}&model={ConfigModel}&sessionKey={Uri.EscapeDataString(sessionKey)}";
    using var resp = await client.GetAsync(url);
    await CheckResponseAsync(resp, url);
    using var doc = await JsonDocFromResp(resp);
    var root = doc.RootElement;
    var isSign = JsonHelper.GetBool(root, "isSign");
    var bonus = JsonHelper.GetInt(root, "netdiskBonus");
    if (verbose) Console.WriteLine($"[DEBUG] userSign response -> isSign={isSign} netdiskBonus={bonus}");
    return new SignResult(isSign, bonus);
}

async Task<UserSizeInfo> GetUserSizeInfo(HttpClient client, string sessionKey, bool verbose)
{
    var url = $"{WebUrl}/api/portal/getUserSizeInfo.action?sessionKey={Uri.EscapeDataString(sessionKey)}";
    using var resp = await client.GetAsync(url);
    await CheckResponseAsync(resp, url);
    using var doc = await JsonDocFromResp(resp);
    var root = doc.RootElement;
    var result = new UserSizeInfo(
        root.TryGetProperty("cloudCapacityInfo", out var cc) ? ParseCapacity(cc) : default,
        root.TryGetProperty("familyCapacityInfo", out var fc) ? ParseCapacity(fc) : default);
    if (verbose)
    {
        Console.WriteLine($"[DEBUG] Cloud: total={result.CloudCapacityInfo.TotalSize/1024/1024:F0}MB used={result.CloudCapacityInfo.UsedSize/1024/1024:F0}MB");
        Console.WriteLine($"[DEBUG] Family: total={result.FamilyCapacityInfo.TotalSize/1024/1024:F0}MB used={result.FamilyCapacityInfo.UsedSize/1024/1024:F0}MB");
    }
    return result;
}

static CapacityInfo ParseCapacity(JsonElement el) => new(
    JsonHelper.GetLong(el, "totalSize"),
    JsonHelper.GetLong(el, "usedSize"),
    JsonHelper.GetLong(el, "freeSize"));

string RsaEncrypt(string pubKey, string data)
{
    using var rsa = RSA.Create();
    rsa.ImportFromPem($"-----BEGIN PUBLIC KEY-----\n{pubKey}\n-----END PUBLIC KEY-----");
    var enc = rsa.Encrypt(Encoding.UTF8.GetBytes(data), RSAEncryptionPadding.Pkcs1);
    return Convert.ToHexStringLower(enc);
}

async Task<JsonDocument> JsonDocFromResp(HttpResponseMessage resp)
{
    var body = await resp.Content.ReadAsStringAsync();
    return JsonDocument.Parse(body);
}

async Task CheckResponseAsync(HttpResponseMessage resp, string url)
{
    if (!resp.IsSuccessStatusCode)
    {
        var body = await resp.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} from {url}\nResponse: {body[..Math.Min(500, body.Length)]}");
    }
}

string Mask(string s, int start, int end)
{
    if (s.Length <= end) return s;
    var chars = s.ToCharArray();
    for (int i = start; i < end && i < chars.Length; i++) chars[i] = '*';
    return new string(chars);
}

// ═══════════════════════════════════════════════════════
//   HTTP Logging DelegatingHandler
// ═══════════════════════════════════════════════════════

class HttpLoggingHandler : DelegatingHandler
{
    private readonly bool _verbose;

    public HttpLoggingHandler(HttpMessageHandler innerHandler, bool verbose) : base(innerHandler)
    {
        _verbose = verbose;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_verbose)
        {
            Console.WriteLine($"[HTTP] -> {request.Method} {request.RequestUri}");
            if (request.Content is { } c)
            {
                var body = await c.ReadAsStringAsync(ct);
                if (!string.IsNullOrEmpty(body) && body.Length < 300)
                    Console.WriteLine($"[HTTP]    Body: {body}");
            }
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await base.SendAsync(request, ct);
        sw.Stop();

        if (_verbose)
        {
            var cl = response.Content.Headers.ContentLength;
            Console.WriteLine($"[HTTP] <- {(int)response.StatusCode} {response.ReasonPhrase} ({sw.ElapsedMilliseconds}ms, {cl} bytes)");
        }
        else
        {
            Console.WriteLine($"[HTTP] {request.Method} {request.RequestUri!.AbsolutePath} -> {(int)response.StatusCode} ({sw.ElapsedMilliseconds}ms)");
        }

        return response;
    }
}

// ═══════════════════════════════════════════════════════
// Type declarations
// ═══════════════════════════════════════════════════════

record struct Account(string UserName, string Password);
record struct TokenData(string AccessToken, long ExpiresIn, string RefreshToken);
record struct SessionInfo(string SessionKey, string AccessToken, string RefreshToken);
record struct SignResult(bool IsSign, int NetdiskBonus);
record struct CapacityInfo(long TotalSize, long UsedSize, long FreeSize);
record struct UserSizeInfo(CapacityInfo CloudCapacityInfo, CapacityInfo FamilyCapacityInfo);
record struct UserSizeSnapshot { public string SessionKey; public UserSizeInfo BeforeSize; }

static class JsonHelper
{
    public static string GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() ?? "" : "";

    public static int GetInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetInt32(out var n) ? n : 0,
            JsonValueKind.String => int.TryParse(v.GetString(), out var sn) ? sn : 0,
            _ => 0
        };
    }

    public static long GetLong(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetInt64(out var n) ? n : 0,
            JsonValueKind.String => long.TryParse(v.GetString(), out var sn) ? sn : 0,
            _ => 0
        };
    }

    public static bool GetBool(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}

class FileTokenStore
{
    private readonly string _path;
    private readonly bool _verbose;

    public FileTokenStore(string path, bool verbose) => (_path, _verbose) = (path, verbose);

    public TokenData? Get()
    {
        if (!File.Exists(_path))
        {
            if (_verbose) Console.WriteLine($"[DEBUG] No token file at {_path}");
            return null;
        }
        try
        {
            var json = File.ReadAllText(_path);
            if (_verbose) Console.WriteLine($"[DEBUG] Loaded token from {_path} ({json.Length} bytes)");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var expiresIn = JsonHelper.GetLong(root, "expiresIn");
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_verbose)
            {
                var remaining = expiresIn - now;
                Console.WriteLine($"[DEBUG] Token expiresIn={expiresIn}, now={now}, remaining={(remaining > 0 ? $"{remaining}ms (valid)" : $"EXPIRED ({remaining}ms)")}");
            }
            return new TokenData(
                JsonHelper.GetString(root, "accessToken"),
                expiresIn,
                JsonHelper.GetString(root, "refreshToken"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Failed to read token file: {ex.Message}");
            return null;
        }
    }

    public void Update(string accessToken, string refreshToken, long expiresIn)
    {
        var data = $$"""{"accessToken":"{{accessToken}}","refreshToken":"{{refreshToken}}","expiresIn":{{expiresIn}}}""";
        File.WriteAllText(_path, data);
        if (_verbose) Console.WriteLine($"[DEBUG] Token saved to {_path} (expiresIn={expiresIn})");
    }
}
