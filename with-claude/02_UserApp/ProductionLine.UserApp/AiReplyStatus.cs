using ProductionLine.Shared;

namespace ProductionLine.UserApp;

internal enum AiReplyKind { Success, Fallback, Safety, EvidenceMissing, Input, Reset, Error, Manual }

internal sealed record AiReplyStatus(AiReplyKind Kind, string Title, string Detail)
{
    internal static AiReplyStatus FromResponse(AiConsultResponse reply)
    {
        if (reply.Answer.StartsWith("[AI 생성 안내]", StringComparison.Ordinal))
            return new(AiReplyKind.Success, "성공", "AI 답변을 수신하고 형식·출처를 확인했습니다.");
        if (reply.ManualSource.StartsWith("근거 없음", StringComparison.Ordinal))
            return new(AiReplyKind.EvidenceMissing, "근거 부족", "관련 매뉴얼이 부족해 관리자 확인이 필요합니다.");
        if (reply.ManualSource.StartsWith("입력 확인", StringComparison.Ordinal))
            return new(AiReplyKind.Input, "입력 확인", "질문 내용을 확인해 주세요.");
        if (reply.ManualSource.StartsWith("상담 흐름", StringComparison.Ordinal))
            return new(AiReplyKind.Reset, "상담 초기화", "이전 상담 문맥을 초기화했습니다.");
        var labelEnd = reply.Answer.IndexOf(']');
        var label = reply.Answer.StartsWith('[') && labelEnd >= 0 ? reply.Answer[1..labelEnd] : string.Empty;
        if (label.StartsWith("기본 안전", StringComparison.Ordinal))
            return new(AiReplyKind.Safety, "안전·권한 안내", "현장 대응을 우선하도록 매뉴얼 안내를 즉시 제공했습니다.");
        if (label.Contains("기본 매뉴얼 안내", StringComparison.Ordinal))
            return new(AiReplyKind.Fallback, "매뉴얼 대체", label);
        return new(AiReplyKind.Manual, "매뉴얼 안내", "등록된 매뉴얼의 점검 안내입니다.");
    }
}
