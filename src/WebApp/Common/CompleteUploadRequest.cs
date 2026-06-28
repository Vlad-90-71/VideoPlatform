namespace WebApp.Common
{
    public class CompleteUploadRequest
    {
        public Guid VideoId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int TotalChunks { get; set; }
    }
}
