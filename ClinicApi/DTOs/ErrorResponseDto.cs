namespace clinic_api.DTOs;

public class ErrorResponseDto
{
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public Dictionary<string, string> Details { get; set; }
}