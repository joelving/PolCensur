﻿using DemokratiskDialog.Exceptions;
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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public class TwitterService
    {
        private const string _twitterApiBaseUrl = "https://api.twitter.com/1.1";
        private readonly TwitterApiOptions _options;
        private readonly HttpClient _httpClient;
        private readonly IDataProtectionProvider _protectionProvider;
        private readonly TwitterRateLimits _rateLimits;
        private readonly ILogger<TwitterService> _logger;

        public TwitterService(
            IOptionsSnapshot<TwitterApiOptions> options,
            IHttpClientFactory httpClientFactory,
            IDataProtectionProvider protectionProvider,
            TwitterRateLimits rateLimits,
            ILogger<TwitterService> logger
            )
        {
            _options = options.Value;
            _httpClient = httpClientFactory.CreateClient();
            _protectionProvider = protectionProvider;
            _rateLimits = rateLimits;
            _logger = logger;
        }

        public async Task<bool> IsBlocked(string checkingForUserId, string protectedUserAccessToken, string protectedUserAccessTokenSecret, string userId, CancellationToken cancellationToken = default)
        {
            await _rateLimits.User.Timeline.WaitToProceed(checkingForUserId, cancellationToken);

            var protector = _protectionProvider.CreateProtector(checkingForUserId);
            var userAccessToken = protector.Unprotect(protectedUserAccessToken);
            var userAccessTokenSecret = protector.Unprotect(protectedUserAccessTokenSecret);

            var response = await SendRequestAsUser(
                _options,
                userAccessToken,
                userAccessTokenSecret,
                HttpMethod.Get, $"{_twitterApiBaseUrl}/statuses/user_timeline.json?user_id={userId}&count=1",
                cancellationToken: cancellationToken
            );

            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return false;

            var obj = JToken.Parse(await response.Content.ReadAsStringAsync()) as JObject;
            if (obj == null || !obj.ContainsKey("errors"))
                return false;

            if (!(obj["errors"] is JArray errors))
                return false;

            bool hasErrorCode(JToken e, int code)
            {
                var error = e as JObject;
                if (!error.ContainsKey("code"))
                    return false;

                if (error["code"].Value<int>() == code)
                    return true;

                return false;
            };

            if (errors.Any(e => hasErrorCode(e, 32)))
                throw new TwitterUnauthorizedException();

            //if (errors.Any(e => hasErrorCode(e, 88)))
            //{
            //    // TODO: Rate limited - check for next reset and wait.
            //}

            return errors.Any(e => hasErrorCode(e, 136));
        }

        public async Task<TwitterUser[]> LookupByScreenNames(IEnumerable<string> batch, CancellationToken cancellationToken = default)
        {
            await _rateLimits.App.Lookup.WaitToProceed();

            var response = await SendRequestAsApp(
                _options,
                HttpMethod.Post,
                $"{_twitterApiBaseUrl}/users/lookup.json",
                new[] { new KeyValuePair<string, string>("screen_name", string.Join(",", batch)) },
                cancellationToken
            );

            EnsureTwitterSuccess(response);

            return JsonConvert.DeserializeObject<TwitterUser[]>(await response.Content.ReadAsStringAsync());
        }

        public async Task<TwitterUser[]> LookupByScreenNamesAsUser(string checkingForUserId, string protectedUserAccessToken, string protectedUserAccessTokenSecret, IEnumerable<string> batch, CancellationToken cancellationToken = default)
        {
            await _rateLimits.User.Lookup.WaitToProceed(checkingForUserId, cancellationToken);

            var protector = _protectionProvider.CreateProtector(checkingForUserId);
            var userAccessToken = protector.Unprotect(protectedUserAccessToken);
            var userAccessTokenSecret = protector.Unprotect(protectedUserAccessTokenSecret);

            var response = await SendRequestAsUser(
                _options,
                userAccessToken,
                userAccessTokenSecret,
                HttpMethod.Get,
                $"{_twitterApiBaseUrl}/users/lookup.json?screen_name={string.Join(",", batch)}",
                cancellationToken: cancellationToken
            );

            EnsureTwitterSuccess(response);

            return JsonConvert.DeserializeObject<TwitterUser[]>(await response.Content.ReadAsStringAsync());
        }

        public async Task AddProfilesToList(string userAccessToken, string userAccessTokenSecret, string owner, string slug, IEnumerable<string> batch, CancellationToken cancellationToken)
        {
            await _rateLimits.User.List.WaitToProceed(owner, cancellationToken);

            var response = await SendRequestAsUser(
                _options,
                userAccessToken,
                userAccessTokenSecret,
                HttpMethod.Get,
                $"{_twitterApiBaseUrl}/users/lookup.json?screen_name={string.Join(",", batch)}",
                cancellationToken: cancellationToken
            );

            EnsureTwitterSuccess(response);
        }

        public async Task<TwitterUser[]> ListMembers(string ownerScreenName, string slug, CancellationToken cancellationToken = default)
        {
            await _rateLimits.App.ListMembers.WaitToProceed();

            var response = await SendRequestAsApp(
                _options,
                HttpMethod.Get,
                $"{_twitterApiBaseUrl}/lists/members.json?owner_screen_name={ownerScreenName}&slug={slug}&count=5000",
                cancellationToken: cancellationToken
            );

            EnsureTwitterSuccess(response);

            return JsonConvert.DeserializeObject<ListMembersResponse>(await response.Content.ReadAsStringAsync()).Users;
        }

        public void EnsureTwitterSuccess(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                throw new TwitterUnauthorizedException();

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new TwitterTransientException();

            var statusCodeNumber = (int)response.StatusCode;
            if (400 <= statusCodeNumber && statusCodeNumber < 500)
                throw new TwitterPermanentException();

            throw new TwitterTransientException();
        }

        public async Task<TwitterUser[]> ListMembersAsUser(string checkingForUserId, string protectedUserAccessToken, string protectedUserAccessTokenSecret, string ownerScreenName, string slug, CancellationToken cancellationToken = default)
        {
            await _rateLimits.User.List.WaitToProceed(checkingForUserId, cancellationToken);

            var protector = _protectionProvider.CreateProtector(checkingForUserId);
            var userAccessToken = protector.Unprotect(protectedUserAccessToken);
            var userAccessTokenSecret = protector.Unprotect(protectedUserAccessTokenSecret);

            var response = await SendRequestAsUser(
                _options,
                userAccessToken,
                userAccessTokenSecret,
                HttpMethod.Get,
                $"{_twitterApiBaseUrl}/lists/members.json?owner_screen_name={ownerScreenName}&slug={slug}&count=5000&include_entities=0",
                cancellationToken: cancellationToken
            );

            EnsureTwitterSuccess(response);

            return JsonConvert.DeserializeObject<ListMembersResponse>(await response.Content.ReadAsStringAsync()).Users;
        }

        public async Task<TwitterUser> ShowByScreenName(string screenName, CancellationToken cancellationToken = default)
        {
            await _rateLimits.App.Lookup.WaitToProceed();

            var response = await SendRequestAsApp(
                _options,
                HttpMethod.Get,
                $"{_twitterApiBaseUrl}/users/show.json?screen_name={screenName}",
                cancellationToken: cancellationToken
            );

            EnsureTwitterSuccess(response);

            return JsonConvert.DeserializeObject<TwitterUser>(await response.Content.ReadAsStringAsync());
        }

        private async Task<HttpResponseMessage> SendRequestAsUser(TwitterApiOptions options, string userAccessToken, string userAccessTokenSecret, HttpMethod method, string url, IEnumerable<KeyValuePair<string, string>> payload = null, CancellationToken cancellationToken = default)
        {
            var authClient = new OAuthRequest
            {
                Method = method.Method,
                Type = OAuthRequestType.AccessToken,
                SignatureMethod = OAuthSignatureMethod.HmacSha1,
                ConsumerKey = options.ConsumerKey,
                ConsumerSecret = options.ConsumerSecret,
                Token = userAccessToken,
                TokenSecret = userAccessTokenSecret,
                RequestUrl = url,
                Realm = "twitter.com",
                Version = "1.0"
            };
            var authHeader = authClient.GetAuthorizationHeader().Substring(6);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", authHeader);
            var message = new HttpRequestMessage { Method = method, RequestUri = new Uri(url) };
            if (payload != null)
                message.Content = new FormUrlEncodedContent(payload);

            try
            {
                return await _httpClient.SendAsync(message, cancellationToken);
            }
            catch (Exception)
            {
                // Special case to ensure that network errors don't break everything.
                throw new TwitterTransientException();
            }
        }

        private async Task<HttpResponseMessage> SendRequestAsApp(TwitterApiOptions options, HttpMethod method, string url, IEnumerable<KeyValuePair<string, string>> payload = null, CancellationToken cancellationToken = default)
        {
            var message = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(url),
                Headers = { { "Authorization", $"Bearer {options.BearerToken}" } }
            };
            if (payload != null)
                message.Content = new FormUrlEncodedContent(payload);

            try
            {
                return await _httpClient.SendAsync(message, cancellationToken);
            }
            catch (Exception)
            {
                // Special case to ensure that network errors don't break everything.
                throw new TwitterTransientException();
            }
        }
    }
}
