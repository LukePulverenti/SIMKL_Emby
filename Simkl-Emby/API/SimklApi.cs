﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
// using System.Threading;
using System.Threading.Tasks;

using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

using Simkl.Api.Objects;
using Simkl.Api.Responses;
using MediaBrowser.Model.Dto;

namespace Simkl.Api
{
    public class SimklApi
    {
        /* INTERFACES */
        private readonly IJsonSerializer _json;
        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;

        /* BASIC API THINGS */
        public const string BASE_URL = @"https://api.simkl.com";
        // public const string BASE_URL = @"http://private-9c39b-simkl.apiary-proxy.com";

        public const string REDIRECT_URI = @"https://ddavo.me/redirected?from=EmbySimkl";
        public const string APIKEY = @"27dd5d6adc24aa1ad9f95ef913244cbaf6df5696036af577ed41670473dc97d0";
        public const string SECRET = @"d7b9feb9d48bbaa69dbabaca21ba4671acaa89198637e9e136a4d69ec97ab68b";

        private HttpRequestOptions GetOptions(string userToken = null)
        {
            HttpRequestOptions options = new HttpRequestOptions
            { 
                RequestContentType = "application/json",
                LogRequest = true,
                LogRequestAsDebug = true,
                LogResponse = true,
                LogResponseHeaders = true,
                LogErrorResponseBody = true,
                EnableDefaultUserAgent = true
            };
            options.RequestHeaders.Add("simkl-api-key", APIKEY);
            // options.RequestHeaders.Add("Content-Type", "application/json");
            if ( !string.IsNullOrEmpty(userToken) )
                options.RequestHeaders.Add("Authorization", "Bearer " + userToken);

            return options;
        }

        public SimklApi(IJsonSerializer json, ILogger logger, IHttpClient httpClient)
        {
            _json = json;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<CodeResponse> getCode()
        {
            string uri = String.Format("/oauth/pin?client_id={0}&redirect={1}", APIKEY, REDIRECT_URI);
            return _json.DeserializeFromStream<CodeResponse>(await _get(uri));
        }

        public async Task<CodeStatusResponse> getCodeStatus(string user_code)
        {
            string uri = String.Format("/oauth/pin/{0}?client_id={1}", user_code, APIKEY);
            return _json.DeserializeFromStream<CodeStatusResponse>(await _get(uri));
        }

        public async Task<UserSettings> getUserSettings()
        {
            string uri = String.Format("/users/settings");
            string user_code = Plugin.Instance.Configuration.userToken;
            return _json.DeserializeFromStream<UserSettings>(await _post(uri, user_code));
        }

        /* NOW EVERYTHING RELATED TO SCROBBLING */
        public async void markAsWatched(BaseItemDto MediaInfo, string userToken)
        {
            SimklHistory history = new SimklHistory();
            _logger.Info("Scrobbling mediainfo: " + _json.SerializeToString(MediaInfo));
            if (MediaInfo.IsMovie == true || MediaInfo.Type == "Movie")
            {
                history.movies.Add(new SimklMovie(MediaInfo));
            }
            else if (MediaInfo.IsSeries == true || MediaInfo.Type == "Episode")
            {
                // TODO: TV Shows scrobbling (WIP)
            }
            else
            {
                throw new NotImplementedException("Method not implemented for MediaInfo type " + MediaInfo.Type);
            }

            _logger.Info("Scrobbling " + _json.SerializeToString(history));
            await SyncHistoryAsync(history, userToken);
        }

        /// <summary>
        /// Implements /sync/history method from simkl
        /// </summary>
        /// <param name="history">History object</param>
        /// <param name="userToken">User token</param>
        /// <returns></returns>
        public async Task<Stream> SyncHistoryAsync(SimklHistory history, string userToken)
        {
            return await _post("/sync/history", userToken, history);

            // using (var r = await _get("/sync/history"))
        }

        /// <summary>
        /// API's private get method, given RELATIVE url and headers
        /// </summary>
        /// <param name="url">Relative url</param>
        /// <param name="userToken">Authentication token</param>
        /// <returns>HTTP(s) Stream to be used</returns>
        private async Task<Stream> _get(string url, string userToken = null)
        {
            // Todo: If string is not null neither empty
            HttpRequestOptions options = GetOptions(userToken);
            options.Url = BASE_URL + url;

            return await _httpClient.Get(options).ConfigureAwait(false);
        }

        /// <summary>
        /// API's private post method
        /// </summary>
        /// <param name="url">Relative post url</param>
        /// <param name="data">Object to serialize</param>
        /// <param name="userToken">Authentication token</param>
        /// <returns></returns>
        private async Task<Stream> _post(string url, string userToken = null, object data = null)
        {
            HttpRequestOptions options = GetOptions(userToken);
            options.Url = BASE_URL + url;
            if (data != null) options.RequestContent = _json.SerializeToString(data);

            return (await _httpClient.Post(options).ConfigureAwait(false)).Content;
        }
    }
}
