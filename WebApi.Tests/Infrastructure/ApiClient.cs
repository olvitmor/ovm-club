using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WebApi.Controllers;

namespace WebApi.Tests.Infrastructure;

public static class ApiClient
{
    public static Task<HttpResponseMessage> PostJsonAsync(
        this HttpClient client,
        string url,
        object? body,
        string? idempotencyKey = null)
    {
        return client.SendAsync(Post(url, body is string raw ? raw : JsonSerializer.Serialize(body), idempotencyKey));
    }

    public static HttpRequestMessage Post(string url, string? json, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        if (json != null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (idempotencyKey != null)
        {
            request.Headers.TryAddWithoutValidation(EnrollmentsController.IdempotencyKeyHeader, idempotencyKey);
        }

        return request;
    }

    public static string NewKey()
    {
        return Guid.NewGuid().ToString("N");
    }

    public static async Task<string?> ReadReasonAsync(this HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        return problem.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
    }
}
