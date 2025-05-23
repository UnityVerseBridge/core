using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityVerseBridge.Core
{
    public class AuthenticationService
    {
        private static readonly HttpClient httpClient = new HttpClient();
        
        public async Task<string> AuthenticateAsync(string serverUrl, string clientId, string clientType, string authKey)
        {
            try
            {
                var authUrl = serverUrl.Replace("ws://", "http://").Replace("wss://", "https://") + "/auth";
                
                var authData = new
                {
                    clientId = clientId,
                    clientType = clientType,
                    authKey = authKey
                };
                
                var json = JsonUtility.ToJson(authData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync(authUrl, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Authentication failed: {response.StatusCode} - {error}");
                }
                
                var responseJson = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonUtility.FromJson<TokenResponse>(responseJson);
                
                return tokenResponse.token;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthenticationService] Failed to authenticate: {ex.Message}");
                throw;
            }
        }
        
        [Serializable]
        private class TokenResponse
        {
            public string token;
        }
    }
}
