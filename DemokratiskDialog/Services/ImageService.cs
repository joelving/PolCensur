using Microsoft.AspNetCore.Hosting;
using NodaTime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public class ImageService
    {
        private readonly IHostingEnvironment _environment;
        private readonly IClock _clock;
        private readonly HttpClient _httpClient;

        public ImageService(
            IHostingEnvironment environment,
            IClock clock,
            IHttpClientFactory httpClientFactory)
        {
            _environment = environment;
            _clock = clock;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<string> GetProfileImage(string screenname, string size)
        {
            if (screenname is null)
                return null;

            var imageDirectory = Path.Combine(_environment.WebRootPath, "profile-images");
            if (!Directory.Exists(imageDirectory))
                Directory.CreateDirectory(imageDirectory);

            var profileFile = Directory.EnumerateFiles(imageDirectory).Select(Path.GetFileName).FirstOrDefault(f => f.StartsWith($"{screenname}.{size}"));
            if (!(profileFile is null))
                return $"/profile-images/{profileFile}";

            var response = await _httpClient.GetAsync($"https://twitter.com/{screenname}/profile_image?size={size}");
            if (!response.IsSuccessStatusCode)
                return null;

            var extension = Path.GetExtension(response.RequestMessage.RequestUri.PathAndQuery);
            var fileName = $"{screenname}.{size}.{_clock.GetCurrentInstant().ToUnixTimeTicks()}{extension}";

            using (var stream = File.OpenWrite(Path.Combine(imageDirectory, fileName)))
            {
                await response.Content.CopyToAsync(stream);
            }

            return $"/profile-images/{fileName}";
        }
    }
}
