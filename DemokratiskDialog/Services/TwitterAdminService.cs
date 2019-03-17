using DemokratiskDialog.Exceptions;
using DemokratiskDialog.Models;
using DemokratiskDialog.Models.Twitter;
using DemokratiskDialog.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public class TwitterAdminService
    {
        private const string _twitterApiBaseUrl = "https://api.twitter.com/1.1";
        private readonly TwitterApiAdminOptions _options;
        private readonly HttpClient _httpClient;
        private readonly TwitterRateLimits _rateLimits;
        private readonly ILogger<TwitterService> _logger;

        public TwitterAdminService(
            IOptionsSnapshot<TwitterApiAdminOptions> options,
            IHttpClientFactory httpClientFactory,
            TwitterRateLimits rateLimits,
            ILogger<TwitterService> logger
            )
        {
            _options = options.Value;
            _httpClient = httpClientFactory.CreateClient();
            _rateLimits = rateLimits;
            _logger = logger;
        }

        public async Task AddProfilesToList(string slug, IEnumerable<string> batch, CancellationToken cancellationToken)
        {
            await _rateLimits.User.List.WaitToProceed("DemokratiskD", cancellationToken);

            var response = await SendRequestAsUser(
                HttpMethod.Post,
                $"{_twitterApiBaseUrl}/lists/members/create_all.json?owner_screen_name=DemokratiskD&slug={slug}",
                new[] { new KeyValuePair<string, string>("screen_name", string.Join(",", batch)) },
                cancellationToken
            );

            response.EnsureSuccessStatusCode();
        }

        public async Task AddProfilesToListById(string slug, IEnumerable<string> batch, CancellationToken cancellationToken)
        {
            await _rateLimits.User.List.WaitToProceed("DemokratiskD", cancellationToken);

            var response = await SendRequestAsUser(
                HttpMethod.Post,
                $"{_twitterApiBaseUrl}/lists/members/create_all.json?owner_screen_name=DemokratiskD&slug={slug}",
                new[] { new KeyValuePair<string, string>("user_id", string.Join(",", batch)) },
                cancellationToken
            );

            response.EnsureSuccessStatusCode();
        }

        public async Task<TwitterUser[]> ListMembers(string ownerScreenName, string slug, CancellationToken cancellationToken = default)
        {
            await _rateLimits.User.List.WaitToProceed("DemokratiskD", cancellationToken);

            var response = await SendRequestAsUser(
                HttpMethod.Get,
                $"{_twitterApiBaseUrl}/lists/members.json?owner_screen_name={ownerScreenName}&slug={slug}&count=5000",
                cancellationToken: cancellationToken
            );

            response.EnsureSuccessStatusCode();

            return JsonConvert.DeserializeObject<ListMembersResponse>(await response.Content.ReadAsStringAsync()).Users;
        }

        private async Task<HttpResponseMessage> SendRequestAsUser(HttpMethod method, string url, IEnumerable<KeyValuePair<string, string>> payload = null, CancellationToken cancellationToken = default)
        {
            var authClient = new OAuthRequest
            {
                Method = method.Method,
                Type = OAuthRequestType.AccessToken,
                SignatureMethod = OAuthSignatureMethod.HmacSha1,
                ConsumerKey = _options.ConsumerKey,
                ConsumerSecret = _options.ConsumerSecret,
                Token = _options.AccessToken,
                TokenSecret = _options.AccessTokenSecret,
                RequestUrl = url,
                Realm = "twitter.com",
                Version = "1.0"
            };
            var authHeader = authClient.GetAuthorizationHeader().Substring(6);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", authHeader);
            var message = new HttpRequestMessage { Method = method, RequestUri = new Uri(url) };
            if (payload != null)
            {
                var content = new MultipartFormDataContent("X-POLCENSUR-BOUNDARY");
                foreach (var kvp in payload) {
                    content.Add(new StringContent(kvp.Value), kvp.Key);
                }
                message.Content = content;
            }

            return await _httpClient.SendAsync(message, cancellationToken);
        }

        private async Task<HttpResponseMessage> SendRequestAsApp(HttpMethod method, string url, IEnumerable<KeyValuePair<string, string>> payload = null, CancellationToken cancellationToken = default)
        {
            var message = new HttpRequestMessage {
                Method = method,
                RequestUri = new Uri(url),
                Headers = { { "Authorization", $"Bearer {_options.BearerToken}" } }
            };
            if (payload != null)
                message.Content = new FormUrlEncodedContent(payload);

            return await _httpClient.SendAsync(message, cancellationToken);
        }
    }
}
