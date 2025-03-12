using Azure.Data.Tables;
using NETPhotoGallery.Models;

namespace NETPhotoGallery.Services
{
    public interface IImageLikeService
    {
        Task<int> GetLikesAsync(string imageId);
        Task<Dictionary<string, int>> GetAllLikesAsync();
        Task AddLikeAsync(string imageId);
    }

    public class ImageLikeService : IImageLikeService
    {
        private readonly TableClient _tableClient;
        private const string TableName = "imagelikes";
        private readonly ILogger<ImageLikeService> _logger;

        public ImageLikeService(IConfiguration configuration, ILogger<ImageLikeService> logger)
        {
            _logger = logger;
            var connectionString = configuration.GetValue<string>("StorageConnectionString");
            var tableServiceClient = new TableServiceClient(connectionString);
            
            // Ensure the table exists
            try
            {
                tableServiceClient.CreateTableIfNotExists(TableName);
                _logger.LogInformation("Table {TableName} created or already exists", TableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create table {TableName}", TableName);
            }
            
            _tableClient = tableServiceClient.GetTableClient(TableName);
        }

        public async Task<int> GetLikesAsync(string imageId)
        {
            if (string.IsNullOrEmpty(imageId))
            {
                _logger.LogWarning("GetLikesAsync called with null or empty imageId");
                return 0;
            }

            try
            {
                // Try to get the entity
                var response = await _tableClient.GetEntityAsync<ImageLike>("images", imageId);
                _logger.LogInformation("Found likes for image {ImageId}: {LikeCount}", imageId, response.Value.LikeCount);
                return response.Value.LikeCount;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404 || ex.ErrorCode == "ResourceNotFound")
            {
                // Entity doesn't exist yet, which is normal for new images
                _logger.LogInformation("No likes found for image {ImageId}", imageId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get likes for image {ImageId}: {ErrorMessage}", imageId, ex.Message);
                return 0;
            }
        }

        public async Task<Dictionary<string, int>> GetAllLikesAsync()
        {
            var results = new Dictionary<string, int>();
            
            try
            {
                var queryResults = _tableClient.QueryAsync<ImageLike>(filter: $"PartitionKey eq 'images'");

                await foreach (var like in queryResults)
                {
                    results[like.RowKey] = like.LikeCount;
                }
                
                _logger.LogInformation("Retrieved {Count} like records from table", results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query likes from table");
            }

            return results;
        }

        public async Task AddLikeAsync(string imageId)
        {
            if (string.IsNullOrEmpty(imageId))
            {
                _logger.LogWarning("AddLikeAsync called with null or empty imageId");
                return;
            }

            var like = new ImageLike
            {
                PartitionKey = "images",
                RowKey = imageId,
                LikeCount = 1
            };

            try
            {
                // Try to get existing entity
                var existingLike = await _tableClient.GetEntityAsync<ImageLike>("images", imageId);
                like.LikeCount = existingLike.Value.LikeCount + 1;
                _logger.LogInformation("Updating like count for image {ImageId} to {LikeCount}", imageId, like.LikeCount);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404 || ex.ErrorCode == "ResourceNotFound")
            {
                // Entity doesn't exist, use default count of 1
                _logger.LogInformation("Creating new like entry for image {ImageId}", imageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existing likes for image {ImageId}: {ErrorMessage}", imageId, ex.Message);
                // Continue with default count of 1
            }

            try
            {
                await _tableClient.UpsertEntityAsync(like);
                _logger.LogInformation("Successfully saved like for image {ImageId}", imageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save like for image {ImageId}: {ErrorMessage}", imageId, ex.Message);
            }
        }
    }
}
