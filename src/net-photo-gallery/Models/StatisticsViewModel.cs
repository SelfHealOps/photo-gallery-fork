using System;
using System.Collections.Generic;

namespace NETPhotoGallery.Models
{
    public class BlobInfo
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTimeOffset? CreatedOn { get; set; }
    }

    public class StatisticsViewModel
    {
        public int TotalImages { get; set; }
        public long TotalDiskSpace { get; set; }
        public int TotalLikes { get; set; }
        public List<UploadCountPerDay> UploadsPerDay { get; set; } = new();
        public double AverageImageSize { get; set; }
    }

    public class UploadCountPerDay
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }
}
