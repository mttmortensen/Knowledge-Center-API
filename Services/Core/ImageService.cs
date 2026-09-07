using Microsoft.AspNetCore.Http;

namespace Knowledge_Center_API.Services.Core
{
    public class ImageService
    {
        private static readonly HashSet<string> AllowedContentTypes = new()
        {
            "image/png", "image/jpeg", "image/gif", "image/webp"
        };

        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        private readonly string _uploadDirectory;

        public ImageService(string uploadDirectory)
        {
            _uploadDirectory = uploadDirectory;
            Directory.CreateDirectory(_uploadDirectory);
        }

        /// <summary>
        /// Validates and saves an uploaded image to disk, returning its stored filename.
        /// </summary>
        public async Task<string> SaveImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file was uploaded.");

            if (file.Length > MaxFileSizeBytes)
                throw new ArgumentException("Image exceeds the 10 MB size limit.");

            if (!AllowedContentTypes.Contains(file.ContentType))
                throw new ArgumentException("Unsupported image type. Allowed: PNG, JPEG, GIF, WEBP.");

            string extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
                extension = file.ContentType switch
                {
                    "image/png" => ".png",
                    "image/jpeg" => ".jpg",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".bin"
                };

            string fileName = $"{Guid.NewGuid()}{extension}";
            string fullPath = Path.Combine(_uploadDirectory, fileName);

            await using var stream = new FileStream(fullPath, FileMode.CreateNew);
            await file.CopyToAsync(stream);

            return fileName;
        }
    }
}
