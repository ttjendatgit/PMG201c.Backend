namespace PMG201c.Backend.DTOs.Submissions;

public class UploadSubmissionsResponse
{
    public int Uploaded { get; set; }
    public int Failed { get; set; }
    public List<SubmissionResponse> Submissions { get; set; } = new();
    public List<FileUploadError> Errors { get; set; } = new();
}

public class FileUploadError
{
    public string FileName { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
