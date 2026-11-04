using Duyao.ApiBase;
using Duyao.HlsList.Data;
using Duyao.HlsList.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Duyao.HlsList.Controllers;

[ApiController]
[Route("api")]
public class HlsController : CustomBaseController
{
    private readonly HlsDbContext _db;
    private readonly ILogger<HlsController> _logger;
    private static readonly string _playerHtml = LoadPlayerHtml();

    public HlsController(HlsDbContext db, ILogger<HlsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private static string LoadPlayerHtml()
    {
        var asm = typeof(HlsController).Assembly;
        const string name = "Duyao.HlsList.v.html";
        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"找不到内嵌资源 {name}，可用资源: {string.Join(", ", asm.GetManifestResourceNames())}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 版本信息
    /// </summary>
    [HttpGet("")]
    public Task<IActionResult> Version()
    {
        return GetVersion("HlsList");
    }

    /// <summary>
    /// 新增（或按 id 更新）记录，返回 guid（用于 /player/{guid}）
    /// POST /api/add
    /// </summary>
    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] HlsInfoUpsert? upsert)
    {
        if (upsert == null || string.IsNullOrWhiteSpace(upsert.Title))
        {
            return Ok(new ApiResponse { Ok = false, Message = "title 不能为空" });
        }

        HlsInfo? entity;
        if (upsert.Id is > 0)
        {
            entity = await _db.HlsInfos.FirstOrDefaultAsync(h => h.Id == upsert.Id.Value);
            if (entity == null)
            {
                return Ok(new ApiResponse { Ok = false, Message = $"id={upsert.Id} 不存在" });
            }

            entity.UpdateAt = DateTime.Now;
        }
        else
        {
            entity = new HlsInfo
            {
                Guid = Guid.NewGuid().ToString("n")
                , CreateAt = DateTime.Now
            };
            _db.HlsInfos.Add(entity);
        }

        entity.Title = upsert.Title;
        entity.Desc = upsert.Desc;
        entity.Img = upsert.Img;
        entity.KeyData = upsert.KeyData;
        entity.EntryHost = upsert.EntryHost;
        entity.EntryPath = upsert.EntryPath;
        entity.IsValid = upsert.IsValid ?? 1;

        await _db.SaveChangesAsync();

        return Ok(new ApiResponseWithData<string> { Data = entity.Guid, Message = "保存成功" });
    }

    /// <summary>
    /// 分页查询
    /// GET /api/list?page=1&size=20&title=xxx
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int size = 20,
        [FromQuery] string? title = null)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);

        var query = _db.HlsInfos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(title))
        {
            query = query.Where(h => h.Title.Contains(title));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(h => h.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();
        foreach (var i in items)
        {
            i.KeyData = null;
        }
        return Ok(new ApiResponseWithData<object>
        {
            Data = new
            {
                total,
                page,
                pageSize = size,
                items
            }
        });
    }

    /// <summary>
    /// 删除记录（id 支持 guid 或自增数字 id）
    /// DELETE /api/delete/{id}
    /// </summary>
    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> Delete(string? id)
    {
        var key = (id ?? string.Empty).Trim();
        var entity = long.TryParse(key, out var longId)
            ? await _db.HlsInfos.FirstOrDefaultAsync(h => h.Id == longId)
            : await _db.HlsInfos.FirstOrDefaultAsync(h => h.Guid == key.ToLowerInvariant());
        if (entity == null)
        {
            return Ok(new ApiResponse { Ok = false, Message = $"id={id} 不存在" });
        }

        _db.HlsInfos.Remove(entity);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse { Message = $"id={id} 已删除" });
    }

    /// <summary>
    /// 播放页：根据 guid 读取记录，返回内嵌 v.html 并注入播放信息（自动播放）
    /// GET /player/{guid}
    /// </summary>
    [HttpGet("/player/{id}")]
    public async Task<IActionResult> Player(string? id)
    {
        var guid = (id ?? "").Trim().ToLowerInvariant();
        var entity = await _db.HlsInfos.AsNoTracking()
            .FirstOrDefaultAsync(h => h.Guid == guid);
        if (entity == null || string.IsNullOrEmpty(entity.EntryHost) || string.IsNullOrEmpty(entity.EntryPath))
        {
            return NotFound(new ApiResponse { Ok = false, Message = $"id={id} 不存在或缺少播放地址" });
        }

        var entryUrl = $"https://{entity.EntryHost}{entity.EntryPath}";

        var meta = new
        {
            id = entity.Id,
            title = entity.Title,
            desc = entity.Desc,
            img = entity.Img,
            keyData = entity.KeyData,
            entryUrl
        };

        var json = JsonConvert.SerializeObject(meta, new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            StringEscapeHandling = StringEscapeHandling.EscapeHtml
        });

        var inject = $"<script>window.__HLS_META__={json};window.__HLS_AUTOPLAY__=true;</script>";

        var html = _playerHtml.Replace("</head>", inject + "</head>", StringComparison.OrdinalIgnoreCase);

        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        return Content(html, "text/html; charset=utf-8");
    }
}
