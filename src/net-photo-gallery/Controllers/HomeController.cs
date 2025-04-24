using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NETPhotoGallery.Models;
using NETPhotoGallery.Services;
using System.Diagnostics;

namespace NETPhotoGallery.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAzureBlobService _azureBlobService;
        private readonly IImageLikeService _imageLikeService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IAzureBlobService azureBlobService, IImageLikeService imageLikeService, ILogger<HomeController> logger)
        {
            _azureBlobService = azureBlobService;
            _imageLikeService = imageLikeService;
            _logger = logger;
        }

        public async Task<ActionResult> Index()
        {
            try
            {
                var allBlobsTask = _azureBlobService.ListAsync();
                var allLikesTask = _imageLikeService.GetAllLikesAsync();

                await Task.WhenAll(allBlobsTask, allLikesTask);

                var allBlobs = await allBlobsTask;
                var likesMap = await allLikesTask;

                var blobViewModels = allBlobs.Select(blob =>
                {
                    var imageId = blob.Segments.Last();
                    return new BlobViewModel
                    {
                        Uri = blob,
                        Likes = likesMap.GetValueOrDefault(imageId, 0)
                    };
                }).ToList();

                return View(blobViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Index method");
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                Response.StatusCode = 500;
                return View("Error");
            }
        }

        [HttpPost]
        [Route("Home/UploadAsync")]
        public async Task<ActionResult> UploadAsync()
        {
            try
            {
                var request = await HttpContext.Request.ReadFormAsync();
                if (request.Files == null)
                {
                    return BadRequest("Could not upload files");
                }

                var files = request.Files;
                if (files.Count == 0)
                {
                    return BadRequest("Could not upload empty files");
                }

                await _azureBlobService.UploadAsync(files);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadAsync method");
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                Response.StatusCode = 500; // set status code to 500
                return View("Error");
            }
        }

        [HttpPost]
        public async Task<ActionResult> DeleteImage(string fileUri)
        {
            try
            {
                await _azureBlobService.DeleteAsync(fileUri);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteImage method");
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                Response.StatusCode = 500; // set status code to 500
                return View("Error");
            }
        }

        [HttpPost]
        public async Task<ActionResult> DeleteAll()
        {
            try
            {
                await _azureBlobService.DeleteAllAsync();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteAll method");
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                Response.StatusCode = 500; // set status code to 500
                return View("Error");
            }
        }

        [HttpPost]
        public async Task<ActionResult> LikeImage(string imageId)
        {
            try
            {
                await _imageLikeService.AddLikeAsync(imageId);
                var likes = await _imageLikeService.GetLikesAsync(imageId);
                return Json(new { success = true, likes = likes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LikeImage method");
                return Json(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> Statistics()
        {
            try
            {
                var blobs = await _azureBlobService.ListBlobInfoAsync();
                var likesMap = await _imageLikeService.GetAllLikesAsync();
                int totalImages = blobs.Count;
                long totalDiskSpace = blobs.Sum(b => b.Size);
                int totalLikes = likesMap.Values.Sum();
                double averageImageSize = blobs.Average(b => (double)b.Size);

                // Calculate uploads per day for last 30 days
                var today = DateTime.UtcNow.Date;
                var uploadsPerDay = Enumerable.Range(0, 30)
                    .Select(offset => today.AddDays(-offset))
                    .Select(date => new UploadCountPerDay
                    {
                        Date = date,
                        Count = blobs.Count(b => b.CreatedOn.HasValue && b.CreatedOn.Value.UtcDateTime.Date == date)
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                var model = new StatisticsViewModel
                {
                    TotalImages = totalImages,
                    TotalDiskSpace = totalDiskSpace,
                    TotalLikes = totalLikes,
                    UploadsPerDay = uploadsPerDay,
                    AverageImageSize = averageImageSize // Pass average image size to the view model
                };
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Statistics method");
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                Response.StatusCode = 500;
                return View("Error");
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
