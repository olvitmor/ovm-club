using System.Text.Json;

namespace WebApi.Tests.Infrastructure;

public static class JsonAssert
{
    public static void HasExactlyProperties(JsonElement element, params string[] expected)
    {
        var actual = element.EnumerateObject().Select(x => x.Name).OrderBy(x => x).ToArray();

        CollectionAssert.AreEqual(
            expected.OrderBy(x => x).ToArray(),
            actual,
            $"expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
    }

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.Clone();
    }
}
