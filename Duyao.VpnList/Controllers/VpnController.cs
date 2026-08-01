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
    /// 查询
    /// a. host_name 模糊包含 / 模糊不包含
    /// b. isp_name 模糊包含
    /// c. country 模糊包含（多个）
    /// d. is_valid 默认 1
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] VpnInfoQuery? query)
    {
        query ??= new VpnInfoQuery();
        if (query.IsValid == null) query.IsValid = 1;

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 50 : Math.Min(query.PageSize, 500);

        IQueryable<VpnInfo> q = _db.VpnInfos.AsNoTracking();

        if (query.IsValid != null)
            q = q.Where(v => v.IsValid == query.IsValid);

        if (!string.IsNullOrEmpty(query.HostNameContains))
            q = q.Where(v => v.HostName != null && v.HostName.Contains(query.HostNameContains));

        if (!string.IsNullOrEmpty(query.HostNameNotContains))
            q = q.Where(v => v.HostName == null || !v.HostName.Contains(query.HostNameNotContains));

        if (!string.IsNullOrEmpty(query.IspNameContains))
            q = q.Where(v => v.IspName != null && v.IspName.Contains(query.IspNameContains));

        if (query.CountryContains != null && query.CountryContains.Count > 0)
        {
            var countries = query.CountryContains
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();
            if (countries.Count > 0)
            {
                // 多 country 模糊包含：任一 country 模糊命中即匹配
                var lowerCountries = countries
                    .Select(c => c.Trim().ToLower())
                    .ToList();

                var all = await q.ToListAsync();
                var filtered = all
                    .Where(v => v.Country != null
                                && lowerCountries.Any(c => v.Country.ToLower().Contains(c)))
                    .ToList();
                var totalFiltered = filtered.Count;
                var paged = filtered
                    .OrderByDescending(v => v.UpdateAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
                return Ok(new
                {
                    ok = true,
                    total = totalFiltered,
                    page,
                    page_size = pageSize,
                    data = paged
                });
            }
        }

        var total = await q.CountAsync();

        var list = await q
            .OrderByDescending(v => v.UpdateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            ok = true,
            total,
            page,
            page_size = pageSize,
            data = list
        });
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
        entity.IspName = upsert.IspName;
        entity.Ext1 = upsert.Ext1;
        entity.Ext2 = upsert.Ext2;
        if (upsert.IsValid != null) entity.IsValid = upsert.IsValid.Value;

        entity.UpdateAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation($"VpnList update id={entity.Id}");
        return Ok(new { ok = true, id = entity.Id, data = entity });
    }

    /// <summary>
    /// 根据 id（支持多个）或 ip+port（支持多个）删除
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromBody] VpnInfoDeleteRequest? req)
    {
        if (req == null) return BadRequest("body NULL");
        var deleted = 0;

        if (req.Ids != null && req.Ids.Count > 0)
        {
            deleted += await _db.VpnInfos
                .Where(v => req.Ids.Contains(v.Id))
                .ExecuteDeleteAsync();
        }

        if (req.Items != null && req.Items.Count > 0)
        {
            foreach (var it in req.Items)
            {
                if (it == null || string.IsNullOrWhiteSpace(it.Ip) || string.IsNullOrWhiteSpace(it.Port))
                    continue;
                deleted += await _db.VpnInfos
                    .Where(v => v.Ip == it.Ip && v.Port == it.Port)
                    .ExecuteDeleteAsync();
            }
        }

        _logger.LogInformation($"VpnList delete count={deleted}");
        return Ok(new { ok = true, deleted });
    }

    private static void ApplyFields(VpnInfo entity, VpnInfoUpsert upsert)
    {
        if (!string.IsNullOrWhiteSpace(upsert.HostName)) entity.HostName = upsert.HostName;
        if (!string.IsNullOrWhiteSpace(upsert.Country)) entity.Country = upsert.Country;
        if (!string.IsNullOrWhiteSpace(upsert.IspName)) entity.IspName = upsert.IspName;
        if (!string.IsNullOrWhiteSpace(upsert.Ext1)) entity.Ext1 = upsert.Ext1;
        if (!string.IsNullOrWhiteSpace(upsert.Ext2)) entity.Ext2 = upsert.Ext2;
        if (upsert.IsValid != null) entity.IsValid = upsert.IsValid.Value;
    }
}
