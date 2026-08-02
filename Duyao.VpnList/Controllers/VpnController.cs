using System.Net.Sockets;
using Duyao.ApiBase;
using Duyao.TelegramFile.Entity;
using Duyao.VpnList.Data;
using Duyao.VpnList.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Duyao.VpnList.Controllers;

[ApiController]
[Route("[controller]")]
public class VpnController : CustomBaseController
{
    private readonly VpnDbContext _db;
    private readonly ILogger<VpnController> _logger;
    private static readonly Random _rng = new();
    private static readonly string _uiHtml = LoadEmbeddedUiHtml();

    public VpnController(VpnDbContext db, ILogger<VpnController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("")]
    public Task<IActionResult> VpnVersion()
    {
        return GetVersion("VpnList");
    }

    /// <summary>
    /// 内嵌的查询 UI 页面 (Resources/vpn-ui.html)
    /// GET /vpn/ui
    /// </summary>
    [HttpGet("ui")]
    public IActionResult Ui()
    {
        return Content(_uiHtml, "text/html; charset=utf-8");
    }

    private static string LoadEmbeddedUiHtml()
    {
        var asm = typeof(VpnController).Assembly;
        const string name = "Duyao.VpnList.Resources.vpn-ui.html";
        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null)
        {
            // 兜底：列出实际可用的资源名便于排错
            var available = string.Join(", ",
                asm.GetManifestResourceNames());
            throw new InvalidOperationException(
                $"找不到内嵌资源 {name}，可用资源: {available}");
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 查询
    /// a. host_name 模糊包含 / 模糊不包含
    /// b. asn_org 模糊包含
    /// c. country 模糊包含（多个，逗号分隔）
    /// d. is_valid 默认 1
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] VpnInfoQuery? query)
    {
        query ??= new VpnInfoQuery();
        return await ExecuteQueryAsync(query);
    }

    /// <summary>
    /// GET 版查询，多 country 用英文逗号分隔：?country_contains=Japan,US,Singapore
    /// </summary>
    [HttpGet("query")]
    public async Task<IActionResult> QueryGet(
        [FromQuery(Name = "ip_contains")] string? ipContains,
        [FromQuery(Name = "host_name_contains")] string? hostNameContains,
        [FromQuery(Name = "host_name_not_contains")] string? hostNameNotContains,
        [FromQuery(Name = "asn_org_contains")] string? asnOrgContains,
        [FromQuery(Name = "country_contains")] string? countryContains,
        [FromQuery(Name = "country_not_contains")] string? countryNotContains,
        [FromQuery(Name = "is_valid")] int? isValid,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "size")] int size = 50)
    {
        var q = BindFromQuery(ipContains, hostNameContains, hostNameNotContains,
            asnOrgContains, countryContains, countryNotContains, isValid);
        q.Page = page;
        q.Size = size;
        return await ExecuteQueryAsync(q);
    }

    private async Task<IActionResult> ExecuteQueryAsync(VpnInfoQuery query)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var size = query.Size <= 0 ? 50 : Math.Min(query.Size, 500);

        var all = await BuildQueryable(query).ToListAsync();
        var total = all.Count;

        var list = all
            .OrderByDescending(v => v.UpdateAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToList();

        return Ok(new
        {
            ok = true,
            total,
            page,
            size,
            data = list
        });
    }

    /// <summary>
    /// 随机返回一条：先按 query 条件过滤到全集，再随机抽 1 条，不分页。
    /// </summary>
    [HttpGet("rnd")]
    public async Task<IActionResult> RndGet(
        [FromQuery(Name = "ip_contains")] string? ipContains,
        [FromQuery(Name = "host_name_contains")] string? hostNameContains,
        [FromQuery(Name = "host_name_not_contains")] string? hostNameNotContains,
        [FromQuery(Name = "asn_org_contains")] string? asnOrgContains,
        [FromQuery(Name = "country_contains")] string? countryContains,
        [FromQuery(Name = "country_not_contains")] string? countryNotContains,
        [FromQuery(Name = "is_valid")] int? isValid)
    {
        var q = BindFromQuery(ipContains, hostNameContains, hostNameNotContains,
            asnOrgContains, countryContains, countryNotContains, isValid);

        var pool = await BuildQueryable(q).ToListAsync();

        if (pool.Count == 0)
        {
            return Ok(new
            {
                ok = true,
                total = 0,
                data = (VpnInfo?)null
            });
        }

        var picked = pool[_rng.Next(pool.Count)];
        return Ok(new
        {
            ok = true,
            total = pool.Count,
            data = picked
        });
    }

    /// <summary>
    /// 返回所有去重的 country 列表，不分页。
    /// 默认只返回 valid=1；is_valid=-1 不过滤。
    /// </summary>
    [HttpGet("iso")]
    public async Task<IActionResult> Iso(
        [FromQuery(Name = "is_valid")] int? isValid)
    {
        var q = new VpnInfoQuery { IsValid = isValid ?? 1 };
        var list = await BuildQueryable(q)
            .Where(v => v.Country != null && v.Country != "")
            .Select(v => v.Country!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Ok(new
        {
            ok = true,
            total = list.Count,
            data = list
        });
    }

    /// <summary>
    /// 用 VpnInfoQuery 构造 IQueryable，过滤可在 DB 端下推的条件
    /// （country 多值模糊因 EF 不能直接表达嵌套 Contains，留在内存过滤）
    /// </summary>
    private IQueryable<VpnInfo> BuildQueryable(VpnInfoQuery query)
    {
        IQueryable<VpnInfo> q = _db.VpnInfos.AsNoTracking();

        if (query.IsValid != -1)
            q = q.Where(v => v.IsValid == query.IsValid);

        if (!string.IsNullOrEmpty(query.IpContains))
            q = q.Where(v => v.Ip != null && v.Ip.Contains(query.IpContains));

        if (!string.IsNullOrEmpty(query.HostNameContains))
            q = q.Where(v => v.HostName != null && v.HostName.Contains(query.HostNameContains));

        if (!string.IsNullOrEmpty(query.HostNameNotContains))
            q = q.Where(v => v.HostName == null || !v.HostName.Contains(query.HostNameNotContains));

        // asn_org 多值模糊：任一关键字命中即匹配 (OR 语义)，大小写不敏感
        var lowerAsnOrgs = (query.AsnOrgContains ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToLower())
            .ToList();
        if (lowerAsnOrgs.Count > 0)
        {
            q = q.Where(v => v.AsnOrg != null
                && lowerAsnOrgs.Any(c => v.AsnOrg!.ToLower().Contains(c)));
        }

        // country 多值模糊：任一 country 关键字命中即匹配 (OR 语义)
        var lowerCountries = (query.CountryContains ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToLower())
            .ToList();
        if (lowerCountries.Count > 0)
        {
            q = q.Where(v => v.Country != null
                && lowerCountries.Any(c => v.Country!.ToLower().Contains(c)));
        }

        // country 多值模糊不包含：任一关键字命中即排除 (NOT Any)
        var lowerCountriesNot = (query.CountryNotContains ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToLower())
            .ToList();
        if (lowerCountriesNot.Count > 0)
        {
            q = q.Where(v => v.Country == null
                || !lowerCountriesNot.Any(c => v.Country!.ToLower().Contains(c)));
        }

        return q;
    }

    /// <summary>
    /// GET 接口参数绑定：country_contains / country_not_contains 用逗号分隔字符串
    /// is_valid: 不传 → 1（默认），传 -1 表示不过滤
    /// </summary>
    private static VpnInfoQuery BindFromQuery(
        string? ipContains,
        string? hostNameContains,
        string? hostNameNotContains,
        string? asnOrgContains,
        string? countryContains,
        string? countryNotContains,
        int? isValid)
    {
        var q = new VpnInfoQuery
        {
            IpContains = ipContains,
            HostNameContains = hostNameContains,
            HostNameNotContains = hostNameNotContains,
            IsValid = isValid ?? 1,
        };
        if (!string.IsNullOrWhiteSpace(asnOrgContains))
        {
            q.AsnOrgContains = asnOrgContains
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        if (!string.IsNullOrWhiteSpace(countryContains))
        {
            q.CountryContains = countryContains
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        if (!string.IsNullOrWhiteSpace(countryNotContains))
        {
            q.CountryNotContains = countryNotContains
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        return q;
    }

    /// <summary>
    /// 单条新增（必填 ip port country）；如果 ip+port 已存在则更新其他字段
    /// </summary>
    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] JObject? obj)
    {
        if (obj == null) return BadRequest("body NULL");
        var upsert = obj.ToObject<VpnInfoUpsert>();
        if (upsert == null) return BadRequest("body parse error");

        if (string.IsNullOrWhiteSpace(upsert.Ip))
            return BadRequest("ip 必填");
        if (string.IsNullOrWhiteSpace(upsert.Port))
            return BadRequest("port 必填");
        if (string.IsNullOrWhiteSpace(upsert.Country))
            return BadRequest("country 必填");

        var existing = await _db.VpnInfos
            .FirstOrDefaultAsync(v => v.Ip == upsert.Ip && v.Port == upsert.Port);

        if (existing != null)
        {
            ApplyFields(existing, upsert);
            existing.UpdateAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogInformation($"VpnList update by ip+port id={existing.Id} ip={existing.Ip}:{existing.Port}");
            return Ok(new { ok = true, action = "updated", id = existing.Id, data = existing });
        }

        var entity = new VpnInfo
        {
            Ip = upsert.Ip!,
            Port = upsert.Port!,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow
        };
        ApplyFields(entity, upsert);

        _db.VpnInfos.Add(entity);
        await _db.SaveChangesAsync();
        _logger.LogInformation($"VpnList insert id={entity.Id} ip={entity.Ip}:{entity.Port}");
        return Ok(new { ok = true, action = "inserted", id = entity.Id, data = entity });
    }

    /// <summary>
    /// 批量新增（必填 ip port country）；ip+port 已存在的会更新其他字段
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> BatchAdd([FromBody] JArray? arr)
    {
        if (arr == null || arr.Count == 0) return BadRequest("body empty");
        var list = arr.ToObject<List<VpnInfoUpsert>>();
        if (list == null) return BadRequest("body parse error");

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<object>();

        foreach (var upsert in list)
        {
            try
            {
                if (upsert == null
                    || string.IsNullOrWhiteSpace(upsert.Ip)
                    || string.IsNullOrWhiteSpace(upsert.Port)
                    || string.IsNullOrWhiteSpace(upsert.Country))
                {
                    skipped++;
                    errors.Add(new { item = upsert, reason = "ip/port/country 必填" });
                    continue;
                }

                var existing = await _db.VpnInfos
                    .FirstOrDefaultAsync(v => v.Ip == upsert.Ip && v.Port == upsert.Port);

                if (existing != null)
                {
                    ApplyFields(existing, upsert);
                    existing.UpdateAt = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    var entity = new VpnInfo
                    {
                        Ip = upsert.Ip!,
                        Port = upsert.Port!,
                        CreateAt = DateTime.UtcNow,
                        UpdateAt = DateTime.UtcNow
                    };
                    ApplyFields(entity, upsert);
                    _db.VpnInfos.Add(entity);
                    inserted++;
                }
            }
            catch (Exception ex)
            {
                skipped++;
                errors.Add(new { item = upsert, reason = ex.Message });
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation($"VpnList batch: inserted={inserted}, updated={updated}, skipped={skipped}");

        return Ok(new
        {
            ok = true,
            inserted,
            updated,
            skipped,
            errors
        });
    }

    /// <summary>
    /// 根据 id 更新所有字段
    /// </summary>
    [HttpPost("update")]
    public async Task<IActionResult> Update([FromBody] JObject? obj)
    {
        if (obj == null) return BadRequest("body NULL");
        var upsert = obj.ToObject<VpnInfoUpsert>();
        if (upsert == null || upsert.Id == null) return BadRequest("id 必填");

        var entity = await _db.VpnInfos.FirstOrDefaultAsync(v => v.Id == upsert.Id);
        if (entity == null) return Ok(new ApiResponse { Ok = false, Message = "not found" });

        // 更新所有字段；不传则保留原值
        if (!string.IsNullOrWhiteSpace(upsert.Ip)) entity.Ip = upsert.Ip!;
        if (!string.IsNullOrWhiteSpace(upsert.Port)) entity.Port = upsert.Port!;
        entity.HostName = upsert.HostName;
        entity.Country = upsert.Country;
        entity.AsnOrg = upsert.AsnOrg;
        entity.Ext1 = upsert.Ext1;
        entity.Ext2 = upsert.Ext2;
        if (upsert.IsValid != null) entity.IsValid = upsert.IsValid.Value;

        entity.UpdateAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation($"VpnList update id={entity.Id}");
        return Ok(new { ok = true, id = entity.Id, data = entity });
    }

    /// <summary>
    /// 根据 id 设置 is_valid
    /// PUT /vpn/setvalid/{id}/{isValid}
    /// isValid: 0 或 1
    /// </summary>
    [HttpPut("setvalid/{id}/{isValid}")]
    public async Task<IActionResult> SetValid(long id, int isValid)
    {
        if (isValid != 0 && isValid != 1)
            return BadRequest("isValid 只能为 0 或 1");

        var entity = await _db.VpnInfos.FirstOrDefaultAsync(v => v.Id == id);
        if (entity == null) return Ok(new ApiResponse { Ok = false, Message = "not found" });

        entity.IsValid = isValid;
        entity.UpdateAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation($"VpnList setvalid id={id} is_valid={isValid}");
        return Ok(new { ok = true, id = entity.Id, is_valid = entity.IsValid });
    }

    /// <summary>
    /// 根据 id 删除（支持多个，英文逗号分隔）
    /// DELETE /vpn/delete/{id}         — 单个
    /// DELETE /vpn/delete/1,2,3        — 多个
    /// </summary>
    [HttpDelete("delete/{ids}")]
    public async Task<IActionResult> Delete(string ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return BadRequest("ids 不能为空");

        var idList = ids
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var v) ? (long?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        if (idList.Count == 0)
            return BadRequest("ids 解析失败");

        var deleted = await _db.VpnInfos
            .Where(v => idList.Contains(v.Id))
            .ExecuteDeleteAsync();

        _logger.LogInformation($"VpnList delete count={deleted}");
        return Ok(new { ok = true, deleted });
    }

    private static void ApplyFields(VpnInfo entity, VpnInfoUpsert upsert)
    {
        if (!string.IsNullOrWhiteSpace(upsert.HostName)) entity.HostName = upsert.HostName;
        if (!string.IsNullOrWhiteSpace(upsert.Country)) entity.Country = upsert.Country;
        if (!string.IsNullOrWhiteSpace(upsert.AsnOrg)) entity.AsnOrg = upsert.AsnOrg;
        if (!string.IsNullOrWhiteSpace(upsert.Ext1)) entity.Ext1 = upsert.Ext1;
        if (!string.IsNullOrWhiteSpace(upsert.Ext2)) entity.Ext2 = upsert.Ext2;
        if (upsert.IsValid != null) entity.IsValid = upsert.IsValid.Value;
    }

    /// <summary>
    /// 根据 id 设置 asn_org
    /// PUT /vpn/setAsnOrg/{id}
    /// body: { "data": "Cloudflare, Inc." }
    /// 空字符串视为清空
    /// </summary>
    [HttpPut("setAsnOrg/{id}")]
    public async Task<IActionResult> SetAsnOrg(long id, [FromBody] JObject? body)
    {
        if (body == null) return BadRequest("body 不能为空");

        var asnOrg = body.Value<string?>("data");
        if (asnOrg == null) return BadRequest("data 必填");
        if (asnOrg.Length > 256) return BadRequest("data 长度不能超过 256");

        var entity = await _db.VpnInfos.FirstOrDefaultAsync(v => v.Id == id);
        if (entity == null) return Ok(new ApiResponse { Ok = false, Message = "not found" });

        entity.AsnOrg = string.IsNullOrEmpty(asnOrg) ? null : asnOrg;
        entity.UpdateAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation($"VpnList setAsnOrg id={id} asn_org={entity.AsnOrg}");
        return Ok(new { ok = true, id = entity.Id, asn_org = entity.AsnOrg });
    }

    [HttpGet("probe/{id}")]
    public async Task<IActionResult> Probe(long id)
    {
        var entity = await _db.VpnInfos.FirstOrDefaultAsync(v => v.Id == id);
        if (entity == null)
        {
            return Ok(
                new ApiResponse
                {
                    Ok = false,
                    Message = "not found"
                });
        }

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(entity.Ip, short.Parse(entity.Port))
                .WaitAsync(TimeSpan.FromSeconds(3));
            return Ok(new { ok = true, id = entity.Id, data = "connection successful" });
        }
        catch
        {
            return Ok(new { ok = false, id = entity.Id, data = "connection failed" });
        }
    }
}
