using DemokratiskDialog.Exceptions;
using DemokratiskDialog.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public class TwitterService
    {
        private const string _twitterApiBaseUrl = "https://api.twitter.com/1.1";
        private readonly TwitterApiOptions _options;
        private readonly HttpClient _httpClient;
        private readonly ILogger<TwitterService> _logger;

        public TwitterService(IOptionsSnapshot<TwitterApiOptions> options, HttpClient httpClient, ILogger<TwitterService> logger)
        {
            _options = options.Value;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> IsBlocked(string userAccessToken, string screenName)
        {
            var response = await SendRequestAsAppUser(_options, userAccessToken, HttpMethod.Post, $"{_twitterApiBaseUrl}/statuses/user_timeline.json?screen_name={screenName}&count=1");

            var parsed = JToken.Parse(response);
            if (parsed.Type != JTokenType.Object)
                return false;

            var obj = parsed as JObject;
            if (!obj.ContainsKey("errors"))
                return false;

            if (!(obj["errors"] is JArray errors))
                return false;

            return errors.Any(e =>
            {
                var error = e as JObject;
                if (!error.ContainsKey("code"))
                    return false;

                if (error["code"].Value<int>() == 136)
                    return true;

                return false;
            });
        }

        private async Task<string> SendRequestAsAppUser(TwitterApiOptions options, string userAccessToken, HttpMethod method, string url)
        {
            var authClient = new OAuthRequest
            {
                Method = method.Method,
                Type = OAuthRequestType.AccessToken,
                SignatureMethod = OAuthSignatureMethod.HmacSha1,
                ConsumerKey = options.ConsumerKey,
                ConsumerSecret = options.ConsumerSecret,
                TokenSecret = userAccessToken,
                RequestUrl = url,
                Realm = "twitter.com",
                Version = "1.0"
            };
            var authHeader = authClient.GetAuthorizationHeader().Substring(6);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", authHeader);
            var response = await _httpClient.SendAsync(new HttpRequestMessage { Method = method, RequestUri = new Uri(url) });

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    throw new TwitterUnauthorizedException();

                if ((int)response.StatusCode >= 500)
                    throw new TwitterNetworkException();

                response.EnsureSuccessStatusCode();
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}
