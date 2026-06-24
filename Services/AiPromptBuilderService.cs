using System.Text;
using System.Text.RegularExpressions;

namespace PMG201c.Backend.Services;

public class AiPromptBuilderService
{
    // Matches section headings at the start of a line, e.g. "Yêu cầu 1", "Request 2", "Câu 3", "Phần 4"
    private static readonly Regex SectionMarkerRegex = new(
        @"^[ \t]*(yêu\s*cầu|request|câu|phần|part)\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    public string BuildSystemPrompt() => """
        Bạn là giám khảo học thuật chuyên nghiệp. Nhiệm vụ duy nhất là chấm điểm THỰC TẾ từng tiêu chí rubric dựa trên nội dung sinh viên đã viết.

        ═══ BƯỚC CHẤM BẮT BUỘC (theo đúng thứ tự) ═══
        BƯỚC 1 – TÌM BẰNG CHỨNG: Đọc kỹ "## Bài làm của sinh viên". Tìm câu/đoạn/bảng trực tiếp trả lời tiêu chí đang chấm.
        BƯỚC 2 – GHI TRÍCH DẪN: Điền trường "evidence" = trích dẫn ngắn (≤80 ký tự) từ bài làm.
                   Nếu không tìm thấy bằng chứng nào, ghi đúng: "Không có bằng chứng".
        BƯỚC 3 – CHO ĐIỂM: Chỉ cho điểm dựa trên bằng chứng vừa tìm. Evidence = "Không có bằng chứng" → awardedRawScore = 0.

        ═══ QUY TẮC NGHIÊM NGẶT ═══
        1. KHÔNG CÓ BẰNG CHỨNG = 0 ĐIỂM TUYỆT ĐỐI.
           Nếu evidence = "Không có bằng chứng", awardedRawScore PHẢI bằng 0. Vi phạm quy tắc này là sai.
        2. KHÔNG SUY DIỄN – KHÔNG BỊA ĐẶT.
           Không suy ra nội dung từ văn cảnh chung. Sinh viên PHẢI VIẾT RÕ nội dung đó trong bài.
        3. CHẤM ĐỘC LẬP TỪNG TIÊU CHÍ.
           Điểm cao ở tiêu chí khác KHÔNG ảnh hưởng đến tiêu chí này. Không "bù" điểm giữa các tiêu chí.
        4. PHÁT HIỆN PHẦN YÊU CẦU.
           Nếu bài có đánh dấu "Yêu cầu 1/2/3/4" hay "Request 1/2/3/4", chỉ chấm tiêu chí N dựa trên phần "Yêu cầu N" tương ứng.
           Nếu "Yêu cầu N" VẮNG MẶT trong bài làm → tiêu chí N = 0.
        5. ĐIỂM TRUNG GIAN – CHỈ KHI CÓ BẰNG CHỨNG RÕ RÀNG.
           Chỉ cho điểm một phần khi có bằng chứng cụ thể. Không cho điểm vì "có vẻ như" hay "ngầm hiểu".

        ═══ TIÊU CHÍ ĐẶC BIỆT ═══
        RACI Matrix:
        • Bài PHẢI có bảng/danh sách gán vai trò R, A, C, I cụ thể cho từng task mới được điểm.
        • Chỉ liệt kê vai trò/task MÀ KHÔNG có cột R/A/C/I → điểm rất thấp hoặc 0.
        • Thiếu RACI hoàn toàn → 0.

        Risk Register:
        • Bài PHẢI có đủ: (1) tên rủi ro + (2) tác động trên phạm vi/chất lượng/thời gian/chi phí + (3) kế hoạch ứng phó.
        • Chỉ liệt kê rủi ro, thiếu tác động và kế hoạch → điểm thấp (≤30% max).
        • Thiếu hoàn toàn → 0.

        Kế hoạch Chi phí / Ngân sách:
        • Bài PHẢI có đủ: danh mục chi phí + ước tính cụ thể + phương pháp ước tính + người/nhóm phụ trách.
        • Thiếu ước tính hoặc phương pháp → điểm một phần nhỏ, chỉ tính phần đã có.
        • Thiếu hoàn toàn → 0.

        ═══ QUY TẮC NHẬN XÉT (BẮT BUỘC) ═══
        1. comment MÔ TẢ CỤ THỂ: ghi rõ sinh viên đã viết gì và còn thiếu gì.
        2. comment GIẢI THÍCH lý do điểm đó. Không sao chép tiêu đề/mô tả rubric.
        3. TUYỆT ĐỐI KHÔNG dùng câu mẫu, placeholder hay nhận xét chung chung.
        4. Nếu tiêu chí = 0: nêu rõ lý do (thiếu hoàn toàn / nội dung không liên quan / thiếu yếu tố bắt buộc).
        5. overallComment: tóm tắt điểm mạnh và điểm yếu CỤ THỂ của bài này.

        ═══ ĐỊNH DẠNG ĐẦU RA — CHỈ JSON HỢP LỆ, KHÔNG MARKDOWN ═══
        {
          "overallComment": "Nhận xét tổng thể cụ thể về bài làm này.",
          "items": [
            {
              "questionNo": 1,
              "evidence": "Trích dẫn ngắn từ bài hoặc 'Không có bằng chứng'",
              "awardedRawScore": 2.5,
              "comment": "Mô tả cụ thể điều sinh viên làm đúng/sai/thiếu."
            }
          ]
        }
        """;

    public string BuildUserPrompt(AiGradingRequest request)
    {
        var sb = new StringBuilder();

        var totalRaw       = request.RubricItems.Sum(r => r.MaxRawScore);
        var totalConverted = request.RubricItems.Sum(r => r.MaxConvertedScore);

        sb.AppendLine("## Câu hỏi thi");
        sb.AppendLine(request.QuestionText);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(request.GuideText))
        {
            sb.AppendLine("## Hướng dẫn chấm / Đáp án mẫu");
            sb.AppendLine(request.GuideText);
            sb.AppendLine();
        }

        sb.AppendLine($"## Rubric  (Tổng điểm thô tối đa: {totalRaw} | Tổng điểm quy đổi tối đa: {totalConverted})");
        foreach (var item in request.RubricItems)
        {
            sb.AppendLine($"  {item.QuestionNo}. {item.Title}  [tối đa {item.MaxRawScore} điểm thô, quy đổi {item.MaxConvertedScore}]");
            if (!string.IsNullOrWhiteSpace(item.Description))
                sb.AppendLine($"     {item.Description}");
        }
        sb.AppendLine();

        sb.AppendLine("## Bài làm của sinh viên");
        sb.AppendLine(request.StudentText);
        sb.AppendLine();

        // Annotate which sections are present/absent to guide the AI
        var (usesMarkers, presentSections) = DetectSectionMarkers(request.StudentText, request.RubricItems);
        if (usesMarkers)
        {
            var rubricNos        = request.RubricItems.Select(r => r.QuestionNo).ToHashSet();
            var presentInRubric  = presentSections.Where(rubricNos.Contains).OrderBy(x => x).ToList();
            var absentFromRubric = rubricNos.Except(presentSections).OrderBy(x => x).ToList();

            sb.AppendLine("## Phân tích cấu trúc bài làm");
            sb.AppendLine($"Bài làm có đánh dấu phần: {string.Join(", ", presentInRubric.Select(n => $"Yêu cầu {n}"))}");
            if (absentFromRubric.Any())
            {
                sb.AppendLine($"THIẾU phần: {string.Join(", ", absentFromRubric.Select(n => $"Yêu cầu {n}"))}");
                sb.AppendLine("→ Các tiêu chí tương ứng với phần THIẾU PHẢI được cho 0 điểm theo quy tắc trong system prompt.");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Chấm bài làm trên theo đúng rubric và quy tắc trong system prompt.");

        return sb.ToString();
    }

    public string BuildRetryInstruction(string issue) =>
        $"\n\n[YÊU CẦU CHẤM LẠI] Phản hồi trước không đạt yêu cầu: {issue}\n" +
        "Đọc lại bài làm trong \"## Bài làm của sinh viên\". Chấm lại dựa trên NỘI DUNG THỰC TẾ.\n" +
        "QUY TẮC: Không có bằng chứng trong bài → điểm 0. Không suy diễn. Chấm từng tiêu chí độc lập.\n" +
        "Trường evidence PHẢI chứa trích dẫn từ bài, hoặc ghi đúng 'Không có bằng chứng' nếu thiếu.\n" +
        "Trả về CHỈ JSON hợp lệ theo schema, không markdown, không giải thích.";

    /// <summary>
    /// Detects whether a student submission uses section markers (e.g. "Yêu cầu 1", "Request 2")
    /// and returns which section numbers were found.
    /// Only classifies as marker-based when at least 2 distinct markers are detected.
    /// </summary>
    public static (bool UsesMarkers, IReadOnlySet<int> PresentSections) DetectSectionMarkers(
        string studentText, IList<RubricItemInput> rubricItems)
    {
        if (string.IsNullOrWhiteSpace(studentText))
            return (false, new HashSet<int>());

        var upperBound = rubricItems.Count + 2; // sanity: don't match "Câu 99"
        var matches    = SectionMarkerRegex.Matches(studentText);
        var found      = matches
            .Cast<Match>()
            .Select(m => int.TryParse(m.Groups[2].Value, out var n) ? n : -1)
            .Where(n => n > 0 && n <= upperBound)
            .ToHashSet();

        return (found.Count >= 2, found);
    }
}
