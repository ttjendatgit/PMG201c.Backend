using System.ComponentModel.DataAnnotations;

namespace PMG201c.Backend.DTOs.Rubric;

public class UpdateRubricItemRequest
{
    public int QuestionNo { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public double MaxRawScore { get; set; }

    public int OrderIndex { get; set; }
}
