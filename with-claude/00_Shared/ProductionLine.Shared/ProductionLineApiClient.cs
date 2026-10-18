using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProductionLine.Shared;

// [앱용 서버 호출 함수 모음] 관리자 앱·사용자 앱은 이 클래스의 함수로만 서버와 통신합니다.
// - 함수 하나 = 서버 창구(Program.cs의 주소) 하나. 예: CreateIssueAsync → POST /api/issues
// - 서버 주소 정하는 순서: 생성자에 넘긴 주소 → 환경변수 PRODUCTIONLINE_SERVER_URL → http://127.0.0.1:5050 (같은 PC)
// - LoginAsync에 성공하면 출입증(Token)을 기억했다가 이후 요청마다 자동으로 붙입니다.
// - 서버가 오류(400·401·403·404·409·500)를 돌려주면 서버의 안내 문구로 예외(InvalidOperationException)를 던집니다.
//   앱에서는 try/catch로 잡아서 ex.Message를 화면에 보여 주면 됩니다.
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

    public async Task<UserDto> UpdateUserAsync(string userId, UpdateUserRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/users/{Uri.EscapeDataString(userId)}", request);
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

    // afterId를 넘기면 그 번호보다 새 메시지만 받습니다. 주기적으로 새 메시지를 확인할 때 사용합니다.
    public Task<List<IssueMessageDto>> GetIssueMessagesAsync(Guid issueId, long? afterId = null) =>
        GetAsync<List<IssueMessageDto>>(afterId is null
            ? $"api/issues/{issueId}/messages"
            : $"api/issues/{issueId}/messages?afterId={afterId}");

    public async Task<IssueMessageDto> SendIssueMessageAsync(Guid issueId, string message)
    {
        var response = await _http.PostAsJsonAsync($"api/issues/{issueId}/messages",
            new CreateIssueMessageRequest(message));
        return await ReadAsync<IssueMessageDto>(response);
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
