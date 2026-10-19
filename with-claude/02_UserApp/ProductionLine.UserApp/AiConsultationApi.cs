using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ProductionLine.Shared;

namespace ProductionLine.UserApp;

/// <summary>기존 인증과 DTO를 사용하고 AI 상담 요청의 대기 시간만 늘린다.</summary>
internal static class AiConsultationApi
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(35) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static async Task<AiConsultResponse> ConsultAsync(ProductionLineApiClient api, AiConsultRequest question,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(api.BaseAddress, "api/ai/consult"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.Token);
        request.Content = JsonContent.Create(question, options: Json);
        using var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ApiMessage? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ApiMessage>(Json, cancellationToken); }
            catch (JsonException) { }
            throw new InvalidOperationException(error?.Message ?? "AI 상담 요청에 실패했습니다.");
        }
        return await response.Content.ReadFromJsonAsync<AiConsultResponse>(Json, cancellationToken)
            ?? throw new InvalidOperationException("AI 상담 응답을 읽을 수 없습니다.");
    }
}
