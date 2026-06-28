namespace WebApp.Common;

public static class AppConstants
{
    /// <summary>
    /// Максимальный размер загружаемого файла (в MB)
    /// </summary>
    public const int MaxFileSizeMB = 2000;
    
    /// <summary>
    /// Максимальный размер загружаемого файла (в байтах)
    /// </summary>
    public const long MaxFileSizeBytes = MaxFileSizeMB * 1024 * 1024;

    /// <summary>
    /// Размер чанка для загрузки (в MB)
    /// </summary>
    public const int ChunkSizeMB = 5;
    
    /// <summary>
    /// Размер чанка для загрузки (в байтах)
    /// </summary>
    public const long ChunkSizeBytes = ChunkSizeMB * 1024 * 1024; 
}
