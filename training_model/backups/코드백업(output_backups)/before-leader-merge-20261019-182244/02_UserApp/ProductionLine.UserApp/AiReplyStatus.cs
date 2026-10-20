using ProductionLine.Shared;

namespace ProductionLine.UserApp;

public enum AiReplyKind { Success, Fallback, Safety, EvidenceMissing, Input, Reset, Manual }

public sealed record AiReplyStatus(AiReplyKind Kind, string Label)
{
    public static AiReplyStatus FromResponse(AiConsultResponse response)
    {
        if (response.ManualSource.StartsWith("입력 확인")) return new(AiReplyKind.Input, "질문을 확인해 주세요");
        if (response.ManualSource.StartsWith("상담 흐름")) return new(AiReplyKind.Reset, "새 상담 시작");
        if (response.Answer.StartsWith("[기본 안전") || response.Answer.StartsWith("[기본 권한"))
            return new(AiReplyKind.Safety, "안전·권한 안내");
        if (response.ManualSource.StartsWith("근거 없음")) return new(AiReplyKind.EvidenceMissing, "매뉴얼 근거 부족 · 관리자 문의");
        if (response.Answer.StartsWith("[AI 생성 안내]")) return new(AiReplyKind.Success, "AI 답변 수신 완료");
        if (response.Answer.StartsWith("[AI ") || response.Answer.StartsWith("[현재 AI "))
            return new(AiReplyKind.Fallback, "기본 매뉴얼로 답변 · AI 연결 상태는 답변 첫 줄 확인");
        return new(AiReplyKind.Manual, "기본 매뉴얼 안내");
    }
}
