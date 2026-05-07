using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProceduralSFXCompanion.Services;

public class WebService : IDisposable
{
    bool _disposed = false;
    private readonly HttpClient _httpClient;

    public WebService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ProceduralSFXCompanion");
    }
    
    public HttpRequestMessage RequestMessage(string url)
    {
        return new HttpRequestMessage(HttpMethod.Get, url);
    }

    public async Task<HttpResponseMessage> GetAsync(string url)
    {
        return await _httpClient.GetAsync(url);
    }
    
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        return await _httpClient.SendAsync(request);
    }

    public async Task<T?> GetFromJsonAsync<T>(string url)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        return await _httpClient.GetFromJsonAsync<T>(url, options);
    }

    public void Dispose()
    {
        if(_disposed)
            return;
        
        _httpClient.Dispose();
        _disposed = true;
    }
}