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
            tableServiceClient.CreateTableIfNotExists(TableName);
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
                // Use string-based filter syntax instead of LINQ expression
                string filter = $"PartitionKey eq 'images'";
                var queryResults = _tableClient.QueryAsync<ImageLike>(filter);

                await foreach (var like in queryResults)
                {
                    results[like.RowKey] = like.LikeCount;
                }
            }
            catch (Azure.RequestFailedException ex)
            {
                _logger.LogError(ex, "Failed to query likes from table storage");
                // Return empty dictionary rather than failing completely
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
