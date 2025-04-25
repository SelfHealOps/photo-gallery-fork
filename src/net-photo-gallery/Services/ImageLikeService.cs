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
            
            // Add retry logic for table creation with exponential backoff
            int maxRetries = 5;
            int retryDelayMs = 1000; // Start with 1 second
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    tableServiceClient.CreateTableIfNotExists(TableName);
                    break; // Success, exit the retry loop
                }
                catch (Azure.RequestFailedException ex) when (
                    ex.Status == 409 && 
                    ex.ErrorCode == "TableBeingDeleted")
                {
                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "Failed to create table after {Attempts} attempts. Table is being deleted.", maxRetries);
                        throw; // Rethrow after max retries
                    }
                    
                    int delay = retryDelayMs * attempt;
                    _logger.LogWarning("Table {TableName} is being deleted. Retrying in {Delay}ms (Attempt {Attempt}/{MaxRetries})...", 
                        TableName, delay, attempt, maxRetries);
                    
                    // Wait before retrying
                    System.Threading.Thread.Sleep(delay);
                }
            }
            
            _tableClient = tableServiceClient.GetTableClient(TableName);
        }

        public async Task<int> GetLikesAsync(string imageId)
        {
            try
            {
                var response = await _tableClient.GetEntityAsync<ImageLike>(imageId, "images");
                return response.Value.LikeCount;
            }
            catch (Azure.RequestFailedException ex)
            {
                _logger.LogError(ex, "Failed to get likes for image {ImageId}", imageId);
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
            }
            catch (Azure.RequestFailedException ex) when (
                ex.Status == 409 && 
                ex.ErrorCode == "TableBeingDeleted")
            {
                _logger.LogWarning("Table {TableName} is being deleted. Returning empty results.", TableName);
                // Return empty results instead of failing when table is being deleted
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving likes from table {TableName}", TableName);
                throw;
            }

            return results;
        }

        public async Task AddLikeAsync(string imageId)
        {
            var like = new ImageLike
            {
                PartitionKey = "images",
                RowKey = imageId,
                LikeCount = 1
            };

            try
            {
                var existingLike = await _tableClient.GetEntityAsync<ImageLike>("images", imageId);
                like.LikeCount = existingLike.Value.LikeCount + 1;
            }
            catch (Azure.RequestFailedException)
            {
                // Entity doesn't exist, use default count of 1
            }

            await _tableClient.UpsertEntityAsync(like);
        }
    }
}
