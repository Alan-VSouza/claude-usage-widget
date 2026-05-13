using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClaudeUsageWidget;

public record UsageData(int Percent, TimeSpan? ResetsIn, string RawText, bool ParseSuccess);

public static class UsageService
{
    private static readonly HttpClient Http = new();
    private static string? _cachedToken;

    private static string? GetOAuthToken()
    {
        if (_cachedToken is not null) return _cachedToken;
        try
        {
            var credFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", ".credentials.json");

            if (!File.Exists(credFile)) return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(credFile));
            var token = doc.RootElement
                .GetProperty("claudeAiOauth")
                .GetProperty("accessToken")
                .GetString();
            return _cachedToken = token;
        }
        catch { return null; }
    }

    public static async Task<UsageData> FetchAsync(CancellationToken ct = default)
    {
        var token = GetOAuthToken();
        if (token is null)
            return new UsageData(0, null, "Token OAuth não encontrado em ~/.claude/.credentials.json", false);

        try
        {
            // Chamada mínima (1 token) para obter os headers de rate limit
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var body = """{"model":"claude-haiku-4-5-20251001","max_tokens":1,"messages":[{"role":"user","content":"."}]}""";
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var response = await Http.SendAsync(request, cts.Token);

            // O que importa estão nos headers, mesmo se o body falhar
            var utilHeader  = GetHeader(response, "anthropic-ratelimit-unified-5h-utilization");
            var resetHeader = GetHeader(response, "anthropic-ratelimit-unified-5h-reset");

            if (utilHeader is null)
                return new UsageData(0, null, $"Headers de rate limit não encontrados (status {(int)response.StatusCode})", false);

            var utilization = double.Parse(utilHeader, System.Globalization.CultureInfo.InvariantCulture);
            var percent = (int)Math.Round(utilization * 100);
            percent = Math.Clamp(percent, 0, 100);

            TimeSpan? resetsIn = null;
            if (resetHeader is not null && long.TryParse(resetHeader, out var unixTs))
            {
                var resetAt  = DateTimeOffset.FromUnixTimeSeconds(unixTs).UtcDateTime;
                var remaining = resetAt - DateTime.UtcNow;
                resetsIn = remaining.TotalSeconds > 0 ? remaining : TimeSpan.Zero;
            }

            return new UsageData(percent, resetsIn, $"{percent}% (5h window)", true);
        }
        catch (OperationCanceledException)
        {
            return new UsageData(0, null, "Timeout ao chamar API Anthropic", false);
        }
        catch (Exception ex)
        {
            return new UsageData(0, null, $"Erro: {ex.Message}", false);
        }
    }

    private static string? GetHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var vals))
            return vals.FirstOrDefault();
        return null;
    }

    // Mantido para os testes unitários de parsing de texto
    public static UsageData Parse(string raw)
    {
        var clean = Regex.Replace(raw, @"\x1b\[[0-9;]*[a-zA-Z]", "");
        var pctMatch = Regex.Match(clean, @"(\d{1,3})\s*%");
        if (!pctMatch.Success)
            return new UsageData(0, null, raw, false);

        var percent = Math.Clamp(int.Parse(pctMatch.Groups[1].Value), 0, 100);

        TimeSpan? resetsIn = null;
        var tMatch = Regex.Match(clean,
            @"(\d+)\s*h\s*(\d+)\s*m|(\d+)\s*h\b|(\d+)\s*m\b", RegexOptions.IgnoreCase);

        if (tMatch.Success)
        {
            if (tMatch.Groups[1].Success)
                resetsIn = new TimeSpan(int.Parse(tMatch.Groups[1].Value), int.Parse(tMatch.Groups[2].Value), 0);
            else if (tMatch.Groups[3].Success)
                resetsIn = TimeSpan.FromHours(int.Parse(tMatch.Groups[3].Value));
            else if (tMatch.Groups[4].Success)
                resetsIn = TimeSpan.FromMinutes(int.Parse(tMatch.Groups[4].Value));
        }

        return new UsageData(percent, resetsIn, raw, true);
    }
}
