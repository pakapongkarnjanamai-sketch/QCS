using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace QCS.Web.User.Security;

public sealed class UserClaimsTransformation : IClaimsTransformation
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public UserClaimsTransformation(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        if (principal.HasClaim(c => c.Type == "qcs.user.loaded"))
            return principal;

        var nid = ExtractNId(principal.Identity.Name);
        if (string.IsNullOrWhiteSpace(nid))
            return principal;

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("qcs.user.loaded", "true"));
        identity.AddClaim(new Claim("qcs.nid", nid));

        var displayName = await ResolveDisplayNameAsync(nid);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            identity.AddClaim(new Claim("FullName", displayName));
        }

        principal.AddIdentity(identity);
        return principal;
    }

    private async Task<string?> ResolveDisplayNameAsync(string nid)
    {
        var cacheKey = $"user:display:{nid}";
        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        string? displayName = null;
        try
        {
            var client = _httpClientFactory.CreateClient("DocTrackerAPI");
            var baseUrl = client.BaseAddress?.ToString().TrimEnd('/');
            using var response = await client.GetAsync($"{baseUrl}/EmployeeLookup/FullName/{Uri.EscapeDataString(nid)}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("fullName", out var fn))
                    displayName = fn.GetString();
            }
        }
        catch
        {
            // API unavailable — fall back to NID in navbar
        }

        _cache.Set(cacheKey, displayName, TimeSpan.FromMinutes(30));
        return displayName;
    }

    private static string ExtractNId(string? identityName)
    {
        if (string.IsNullOrWhiteSpace(identityName)) return string.Empty;
        var parts = identityName.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return (parts.Length > 0 ? parts[^1] : identityName).Trim().ToUpperInvariant();
    }
}
