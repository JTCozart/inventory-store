using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using InventoryStore.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace InventoryStore.App.Ai;

public sealed record ChatTurn(string Role, string Content);

// Thrown by ChatModel for anything that should degrade the AI Assistant module gracefully
// (missing API key, network failure, API error) rather than take down the request pipeline.
// IsRetryable distinguishes "this exact call will just fail again" (bad/missing API key,
// malformed request) from "this looked transient" (timeout, network blip, 5xx, rate limit).
public sealed class ChatUnavailableException(string message, Exception? inner = null, bool isRetryable = true)
    : Exception(message, inner)
{
    public bool IsRetryable { get; } = isRetryable;
}

// Calls NVIDIA's hosted, OpenAI-compatible chat/completions API (build.nvidia.com). Typed
// HttpClient (registered in Program.cs with BaseAddress/Timeout from ChatOptions) -- every
// call is just an HTTP request, so this is a plain scoped instance.
public sealed class ChatModel(HttpClient http, ISettingsService settings, IOptions<ChatOptions> options, ILogger<ChatModel> logger)
{
    // Nemotron 3 Ultra is a hybrid reasoning model that, left to its default, spends most (or
    // all) of max_tokens on an internal "thinking" pass before ever emitting the actual
    // {"tool":...}/{"answer":...} envelope ChatOrchestrationService's loop expects. This app
    // drives its own explicit tool loop and has no use for extended reasoning, so thinking is
    // switched off via the documented chat_template_kwargs flag. Harmless to send to a
    // non-Nemotron model too (an OpenAI-compatible server ignores fields it doesn't recognize).
    private static readonly object DisableThinking = new { enable_thinking = false };

    // Defensive backstop for ParseModelOutput below, in case thinking output still leaks
    // through -- strips a well-formed <think>...</think> block, or an unterminated one
    // (truncated by max_tokens) through to the end of the string.
    private static readonly Regex ThinkBlock = new(@"<think>.*?(</think>|$)", RegexOptions.Singleline | RegexOptions.Compiled);

    public async Task<string> GenerateTextAsync(
        string systemPrompt, IReadOnlyList<ChatTurn> history, string userMessage,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        messages.AddRange(history.Select(t => (object)new { role = t.Role, content = t.Content }));
        messages.Add(new { role = "user", content = userMessage });

        return await CompleteAsync(messages, cancellationToken);
    }

    // Retries transient failures (timeout, network blip, 5xx, 429) up to ChatOptions.
    // MaxRetryAttempts times with a short linear backoff. Never retries a failure
    // ChatUnavailableException marked non-retryable (bad API key, malformed request).
    private async Task<string> CompleteAsync(List<object> messages, CancellationToken cancellationToken)
    {
        var apiKey = await settings.GetAsync("module.ai.apiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ChatUnavailableException(
                "No NVIDIA API key is configured -- add one in Settings > Modules > AI Assistant.", isRetryable: false);

        var model = await settings.GetAsync("module.ai.model");
        if (string.IsNullOrWhiteSpace(model))
            model = options.Value.TextModel;

        var maxAttempts = Math.Max(1, options.Value.MaxRetryAttempts);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await CompleteOnceAsync(model, messages, apiKey!, cancellationToken);
            }
            catch (ChatUnavailableException ex) when (ex.IsRetryable && attempt < maxAttempts)
            {
                logger.LogWarning(ex, "NVIDIA chat API call failed (attempt {Attempt}/{MaxAttempts}) -- retrying", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }
    }

    private async Task<string> CompleteOnceAsync(string model, List<object> messages, string apiKey, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model,
            messages,
            max_tokens = options.Value.MaxOutputTokens,
            temperature = options.Value.Temperature,
            chat_template_kwargs = DisableThinking
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ChatUnavailableException("The NVIDIA API took too long to respond.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "NVIDIA chat API request failed");
            throw new ChatUnavailableException("Couldn't reach the NVIDIA API. Check network access and try again.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("NVIDIA chat API returned {Status}: {Body}", response.StatusCode, body);

            var isRetryable = response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest);
            throw new ChatUnavailableException(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? "The NVIDIA API rejected the configured API key -- check it in Settings > Modules > AI Assistant."
                    : $"The NVIDIA API returned an error ({(int)response.StatusCode}).",
                isRetryable: isRetryable);
        }

        var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        var text = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrEmpty(text))
            throw new ChatUnavailableException("The NVIDIA API returned an empty response.");

        return ThinkBlock.Replace(text, "").Trim();
    }

    private sealed record ChatCompletionResponse([property: JsonPropertyName("choices")] List<ChatCompletionChoice>? Choices);
    private sealed record ChatCompletionChoice([property: JsonPropertyName("message")] ChatCompletionMessage? Message);
    private sealed record ChatCompletionMessage([property: JsonPropertyName("content")] string? Content);
}
