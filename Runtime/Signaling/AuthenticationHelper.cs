using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityVerseBridge.Core.Signaling
{
    /// <summary>
    /// Static authentication helper for Signaling Server
    /// </summary>
    public static class AuthenticationHelper
    {
        [System.Serializable]
        public class AuthRequest
        {
            public string clientId;
            public string clientType;
            public string authKey;
        }

        [System.Serializable]
        public class AuthResponse
        {
            public string token;
            public string error;
        }

        private static string currentToken;
        
        public static string CurrentToken => currentToken;
        public static bool IsAuthenticated => !string.IsNullOrEmpty(currentToken);

        /// <summary>
        /// Authenticate with the signaling server
        /// </summary>
        public static async Task<bool> AuthenticateAsync(string serverUrl, string clientId, string clientType, string authKey = null)
        {
            // WebSocket URL to HTTP URL
            string httpUrl = serverUrl.Replace("ws://", "http://").Replace("wss://", "https://");
            if (!httpUrl.EndsWith("/"))
                httpUrl += "/";
            string authEndpointUrl = httpUrl + "auth";

            // Default authKey for development
            if (string.IsNullOrEmpty(authKey))
            {
                authKey = "development-key";
            }

            var authRequest = new AuthRequest
            {
                clientId = clientId,
                clientType = clientType,
                authKey = authKey
            };

            string jsonRequest = JsonUtility.ToJson(authRequest);
            Debug.Log($"[AuthenticationHelper] Sending auth request to {authEndpointUrl}");

            try
            {
                using (UnityWebRequest request = UnityWebRequest.Post(authEndpointUrl, jsonRequest, "application/json"))
                {
                    var operation = request.SendWebRequest();
                    
                    while (!operation.isDone)
                    {
                        await Task.Delay(10);
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = request.downloadHandler.text;
                        Debug.Log($"[AuthenticationHelper] Auth response: {responseText}");
                        
                        AuthResponse response = JsonUtility.FromJson<AuthResponse>(responseText);
                        
                        if (!string.IsNullOrEmpty(response.token))
                        {
                            currentToken = response.token;
                            Debug.Log("[AuthenticationHelper] Authentication successful");
                            return true;
                        }
                        else
                        {
                            Debug.LogError($"[AuthenticationHelper] Auth failed: {response.error}");
                            return false;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[AuthenticationHelper] Auth request failed: {request.error}");
                        Debug.LogError($"Response Code: {request.responseCode}");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthenticationHelper] Exception during authentication: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear the current token
        /// </summary>
        public static void ClearToken()
        {
            currentToken = null;
            Debug.Log("[AuthenticationHelper] Token cleared");
        }

        /// <summary>
        /// Append token to WebSocket URL
        /// </summary>
        public static string AppendTokenToUrl(string wsUrl)
        {
            if (string.IsNullOrEmpty(currentToken))
            {
                Debug.LogWarning("[AuthenticationHelper] No token available");
                return wsUrl;
            }

            string separator = wsUrl.Contains("?") ? "&" : "?";
            return $"{wsUrl}{separator}token={currentToken}";
        }
    }
}
