using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ui.Services;

public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    public JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public Uri? BaseAddress => _httpClient.BaseAddress;

    public void ConfigureBaseAddress(int port)
    {
        if (_httpClient.BaseAddress is not null)
        {
            throw new InvalidOperationException("The API base address has already been configured.");
        }

        _httpClient.BaseAddress = new Uri($"http://127.0.0.1:{port}/");
    }

    public async Task<T> GetFromJsonAsync<T>(
        string route,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var response = await _httpClient.GetAsync(
            NormalizeRoute(route),
            cancellationToken);
        await EnsureSuccessAsync(response, route, cancellationToken);

        return await response.Content.ReadFromJsonAsync<T>(
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidOperationException($"{route} returned an empty response.");
    }

    public async Task PostAsync(
        string route,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            NormalizeRoute(route));
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, route, cancellationToken);
    }

    public async Task PostAsJsonAsync<T>(
        string route,
        T payload,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var response = await _httpClient.PostAsJsonAsync(
            NormalizeRoute(route),
            payload,
            JsonOptions,
            cancellationToken);
        await EnsureSuccessAsync(response, route, cancellationToken);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        try
        {
            using var response = await _httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static string NormalizeRoute(string route)
    {
        return route.TrimStart('/');
    }

    private void EnsureConfigured()
    {
        if (_httpClient.BaseAddress is null)
        {
            throw new InvalidOperationException("The local backend is not ready.");
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string route,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"{route} returned {(int)response.StatusCode} ({response.ReasonPhrase}): {body}");
    }
}
