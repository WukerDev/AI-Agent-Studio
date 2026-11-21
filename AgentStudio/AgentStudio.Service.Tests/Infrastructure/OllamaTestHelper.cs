namespace AgentStudio.Service.Tests.Infrastructure;

public static class OllamaTestHelper
{
    private static bool? isOllamaAvailable;

    public static async Task<bool> IsOllamaGemmaAvailableAsync()
    {
        if (isOllamaAvailable.HasValue)
        {
            return isOllamaAvailable.Value;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var client = new HttpClient();
            var response = await client.GetAsync("http://127.0.0.1:11434/api/tags", cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                isOllamaAvailable = false;
                return false;
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            isOllamaAvailable = json.Contains("gemma3", StringComparison.OrdinalIgnoreCase);
            return isOllamaAvailable.Value;
        }
        catch
        {
            isOllamaAvailable = false;
            return false;
        }
    }
}
