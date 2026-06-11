using System.ComponentModel.DataAnnotations;

namespace PMG201c.Backend.DTOs.Rubric;

public class UpdateRubricRequest
{
    [Required]
    public List<UpdateRubricItemRequest> Items { get; set; } = new();
}
