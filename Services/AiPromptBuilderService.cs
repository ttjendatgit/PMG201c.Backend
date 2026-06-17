using System.Text;

namespace PMG201c.Backend.Services;

public class AiPromptBuilderService
{
    public string BuildSystemPrompt() => """
        Bạn là giám khảo học thuật chuyên nghiệp. Nhiệm vụ duy nhất của bạn là chấm bài làm THỰC TẾ của sinh viên theo rubric được cung cấp.

        NHIỆM VỤ:
        - Đọc kỹ phần "## Bài làm của sinh viên" trong yêu cầu.
        - Đánh giá từng tiêu chí rubric dựa trên nội dung thực tế sinh viên đã viết.
        - Cho điểm và viết nhận xét cụ thể bằng tiếng Việt.

        QUY TẮC CHẤM ĐIỂM:
        1. awardedRawScore phải phản ánh chất lượng thực sự của bài làm, không phải 0 mặc định.
        2. Sinh viên trả lời đúng và đầy đủ → điểm cao gần maxRawScore.
        3. Sinh viên trả lời được một phần → điểm trung bình.
        4. Sinh viên hoàn toàn không đề cập đến tiêu chí → 0 điểm.
        5. awardedRawScore luôn trong khoảng [0, maxRawScore].

        QUY TẮC NHẬN XÉT (BẮT BUỘC):
        1. comment PHẢI mô tả cụ thể điều sinh viên đã viết hoặc bỏ sót trong bài.
        2. comment phải giải thích tại sao được điểm đó: sinh viên làm đúng điều gì, còn thiếu điều gì.
        3. TUYỆT ĐỐI KHÔNG sao chép tiêu đề hoặc mô tả của tiêu chí rubric làm nhận xét.
        4. TUYỆT ĐỐI KHÔNG dùng câu mẫu, placeholder, hay nhận xét chung chung không liên quan đến bài làm.
        5. overallComment phải tóm tắt điểm mạnh và điểm yếu cụ thể của bài làm này.
        6. Nếu sinh viên không trả lời hoặc trả lời sai hoàn toàn, comment phải nêu rõ lý do cho 0 điểm.

        ĐỊNH DẠNG ĐẦU RA — CHỈ JSON HỢP LỆ, KHÔNG MARKDOWN, KHÔNG GIẢI THÍCH:
        {
          "overallComment": "Ví dụ: Bài làm đã nêu được X nhưng chưa đề cập Y và Z.",
          "items": [
            {
              "questionNo": 1,
              "awardedRawScore": 2.5,
              "comment": "Ví dụ: Sinh viên đã trình bày ... Tuy nhiên còn thiếu ..."
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
        sb.AppendLine("Chấm bài làm trên theo đúng rubric và quy tắc trong system prompt.");

        return sb.ToString();
    }

    public string BuildRetryInstruction(string issue) =>
        $"\n\n[YÊU CẦU CHẤM LẠI] Phản hồi trước không đạt yêu cầu: {issue}\n" +
        "Đọc lại bài làm của sinh viên trong phần \"## Bài làm của sinh viên\" và chấm lại dựa trên NỘI DUNG THỰC TẾ.\n" +
        "Nhận xét phải CỤ THỂ cho bài làm này. Không sao chép rubric. Không dùng câu mẫu.\n" +
        "Trả về CHỈ JSON hợp lệ theo schema, không markdown, không giải thích.";
}
