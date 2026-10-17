using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProductionLine.Shared;

public sealed class ProductionLineApiClient : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string? Token { get; private set; }
    public UserDto? CurrentUser { get; private set; }
    public Uri BaseAddress => _http.BaseAddress!;

    public ProductionLineApiClient(string? baseUrl = null)
    {
        baseUrl ??= Environment.GetEnvironmentVariable("PRODUCTIONLINE_SERVER_URL")
                   ?? "http://127.0.0.1:5050";
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<UserDto> LoginAsync(string userId, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(userId, password));
        var login = await ReadAsync<LoginResponse>(response);
        Token = login.Token;
        CurrentUser = login.User;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return login.User;
    }

    public Task<List<LineStatusDto>> GetLinesAsync() => GetAsync<List<LineStatusDto>>("api/lines");
    public Task<List<UserDto>> GetUsersAsync() => GetAsync<List<UserDto>>("api/users");
    public Task<List<IssueDto>> GetIssuesAsync() => GetAsync<List<IssueDto>>("api/issues");
    public Task<List<InspectionDto>> GetInspectionsAsync(int lineId) =>
        GetAsync<List<InspectionDto>>($"api/inspections?lineId={lineId}");

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/users", request);
        return await ReadAsync<UserDto>(response);
    }

    public async Task<InspectionDto> SimulateInspectionAsync(InspectionRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/inspections/simulate", request);
        return await ReadAsync<InspectionDto>(response);
    }

    public async Task<IssueDto> CreateIssueAsync(CreateIssueRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/issues", request);
        return await ReadAsync<IssueDto>(response);
    }

    public async Task<IssueDto> UpdateIssueAsync(Guid issueId, UpdateIssueStatusRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/issues/{issueId}/status", request);
        return await ReadAsync<IssueDto>(response);
    }

    public async Task<AiConsultResponse> ConsultAiAsync(AiConsultRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/ai/consult", request);
        return await ReadAsync<AiConsultResponse>(response);
    }

    public void Dispose() => _http.Dispose();

    private async Task<T> GetAsync<T>(string path)
    {
        var response = await _http.GetAsync(path);
        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))
                   ?? throw new InvalidOperationException("서버 응답이 비어 있습니다.");
        }

        var body = await response.Content.ReadAsStringAsync();
        try
        {
            var message = JsonSerializer.Deserialize<ApiMessage>(body, JsonOptions)?.Message;
            throw new InvalidOperationException(message ?? $"서버 오류: {(int)response.StatusCode}");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"서버 오류: {(int)response.StatusCode} {body}");
        }
    }
}
