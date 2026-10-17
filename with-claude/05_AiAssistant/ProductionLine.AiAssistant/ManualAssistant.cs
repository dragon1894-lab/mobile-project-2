using ProductionLine.Shared;

namespace ProductionLine.AiAssistant;

/// <summary>
/// 5번 상담 AI 담당자의 교체 지점입니다.
/// 현재는 로컬 매뉴얼 키워드 검색으로 동작하며, 이후 RAG/LLM 호출로 바꿀 수 있습니다.
/// 검사 결과를 생성하거나 변경하지 않습니다.
/// </summary>
public sealed class ManualAssistant
{
    private readonly ManualEntry[] _manuals =
    [
        new("카메라 점검 매뉴얼 2장", ["카메라", "화면", "영상", "끊김", "검정"],
            "1) 카메라 전원과 이더넷 케이블을 확인하세요. 2) 카메라 IP와 서버 PC의 대역을 확인하세요. 3) 렌즈 오염과 조명을 확인한 뒤 영상을 다시 획득하세요."),
        new("센서 점검 매뉴얼 3장", ["센서", "감지", "신호"],
            "1) 센서 표시등을 확인하세요. 2) 연결 단자와 전원을 확인하세요. 3) 기준 물체를 이용해 감지 거리를 다시 확인하세요."),
        new("검사 프로그램 운영 매뉴얼 4장", ["검사", "프로그램", "오류", "응답", "연결"],
            "1) 서버 연결 상태와 마지막 갱신 시각을 확인하세요. 2) 검사 프로그램 로그를 확인하세요. 3) 재시도 후에도 실패하면 오류 메시지와 함께 관리자에게 문의하세요."),
        new("설비 기본 점검표", ["설비", "정지", "동작", "모터"],
            "1) 비상 정지와 안전 상태를 먼저 확인하세요. 2) 전원과 센서 상태를 확인하세요. 3) 임의로 인터록을 해제하지 말고 관리자 지시를 요청하세요.")
    ];

    public AiConsultResponse Consult(IssueDto issue, string question)
    {
        var text = $"{issue.Title} {issue.Description} {question}";
        var match = _manuals
            .Select(entry => new { Entry = entry, Score = entry.Keywords.Count(text.Contains) })
            .OrderByDescending(x => x.Score)
            .First();

        if (match.Score == 0)
        {
            return new AiConsultResponse(
                "현재 등록된 매뉴얼에서 근거를 찾지 못했습니다. 문제 내용과 이미지를 첨부해 관리자에게 문의하세요.",
                "근거 없음",
                true);
        }

        return new AiConsultResponse(
            $"가능한 원인을 확인하기 위해 다음 순서로 점검하세요.\r\n{match.Entry.Guidance}\r\n점검 결과를 문제 조치 내용에 기록하세요.",
            match.Entry.Source,
            false);
    }

    private sealed record ManualEntry(string Source, string[] Keywords, string Guidance);
}
