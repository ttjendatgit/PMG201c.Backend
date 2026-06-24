using System.ComponentModel.DataAnnotations;

namespace PMG201c.Backend.DTOs.Grading;

public class ManualGradingRequest
{
    public string? TeacherOverallComment { get; set; }

    [Required]
    public List<ManualGradingItemRequest> Items { get; set; } = new();
}
