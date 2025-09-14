using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace BabeMakro.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://localhost:7159";
        private string? _jwtToken;

        public ApiService()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _httpClient = new HttpClient(handler);
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            LoadToken();
        }

        private void LoadToken()
        {
            var savedToken = BabeMakro.Properties.Settings.Default.Token;
            System.Diagnostics.Debug.WriteLine($"LoadToken called - Saved token: {(savedToken != null && savedToken.Length > 10 ? savedToken.Substring(0, 10) + "..." : "null/empty")}");

            if (!string.IsNullOrEmpty(savedToken))
            {
                _jwtToken = savedToken;
                SetAuthorizationHeader(_jwtToken);
                System.Diagnostics.Debug.WriteLine($"Token loaded and authorization header set: {_httpClient.DefaultRequestHeaders.Authorization}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No token found in settings");
            }
        }

        private void SaveToken(string token)
        {
            _jwtToken = token;
            BabeMakro.Properties.Settings.Default.Token = token;
            BabeMakro.Properties.Settings.Default.Save();
            SetAuthorizationHeader(token);

            // Debug information
            System.Diagnostics.Debug.WriteLine($"Token saved: {(token != null ? token.Substring(0, Math.Min(10, token.Length)) + "..." : "null")}");
            System.Diagnostics.Debug.WriteLine($"Authorization header set: {_httpClient.DefaultRequestHeaders.Authorization}");
        }

        private void SetAuthorizationHeader(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("SetAuthorizationHeader: Token is null or empty");
                return;
            }

            // Clean the token (remove any existing "Bearer " prefix if present)
            var cleanToken = token.Trim();
            if (cleanToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                cleanToken = cleanToken.Substring(7).Trim();
                System.Diagnostics.Debug.WriteLine("SetAuthorizationHeader: Removed existing Bearer prefix");
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);
                System.Diagnostics.Debug.WriteLine($"SetAuthorizationHeader: Successfully set Authorization header to: Bearer {cleanToken.Substring(0, Math.Min(20, cleanToken.Length))}...");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetAuthorizationHeader: Error setting header: {ex.Message}");
            }
        }

        public void ClearToken()
        {
            _jwtToken = null;
            BabeMakro.Properties.Settings.Default.Token = "";
            BabeMakro.Properties.Settings.Default.LastLoggedInPassword = "";
            BabeMakro.Properties.Settings.Default.Save();
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        public async Task<ApiResponse<LoginResponse>> LoginAsync(string email, string password)
        {
            try
            {
                var hwid = HardwareInfo.GetHWID();
                var request = new LoginRequest
                {
                    Email = email,
                    Password = password,
                    HWID = hwid
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Debug information
                System.Diagnostics.Debug.WriteLine($"POST URL: {_httpClient.BaseAddress}/api/Auth/login");
                System.Diagnostics.Debug.WriteLine($"Request JSON: {json}");

                var response = await _httpClient.PostAsync("/api/Auth/login", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Debug response
                System.Diagnostics.Debug.WriteLine($"Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Response Content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Login successful, parsing response: {responseContent}");

                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    System.Diagnostics.Debug.WriteLine($"Parsed token from response: {(loginResponse?.AccessToken != null ? loginResponse.AccessToken.Substring(0, Math.Min(20, loginResponse.AccessToken.Length)) + "..." : "null")}");

                    if (!string.IsNullOrEmpty(loginResponse?.AccessToken))
                    {
                        SaveToken(loginResponse.AccessToken);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("WARNING: No token found in login response!");
                    }

                    return new ApiResponse<LoginResponse> { Success = true, Data = loginResponse };
                }

                var errorResponse = TryParseErrorResponse(responseContent);
                var errorMessage = errorResponse ?? responseContent ?? $"Login failed with status: {response.StatusCode}";

                // If response content is empty or whitespace, provide a meaningful message
                if (string.IsNullOrWhiteSpace(errorMessage) || errorMessage.Trim().Length == 0)
                {
                    errorMessage = $"Login failed. Server returned {response.StatusCode} with no error details.";
                }

                // Add detailed error information for debugging
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    errorMessage = $"Login endpoint not found (404). Please check if the API server is running correctly at {_baseUrl}/auth/login\n\nOriginal error: {errorMessage}";
                }

                return new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = errorMessage
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = $"API connection error: {ex.Message}\n\nPlease ensure the API server is running at {_baseUrl}"
                };
            }
        }

        public async Task<ApiResponse<RegisterResponse>> RegisterAsync(string email, string password)
        {
            try
            {
                var hwid = HardwareInfo.GetHWID();
                var request = new RegisterRequest
                {
                    Email = email,
                    Password = password,
                    ConfirmPassword = password,
                    HWID = hwid
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Debug information
                System.Diagnostics.Debug.WriteLine($"POST URL: {_httpClient.BaseAddress}/api/Auth/register");
                System.Diagnostics.Debug.WriteLine($"Request JSON: {json}");

                var response = await _httpClient.PostAsync("/api/Auth/register", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Debug response
                System.Diagnostics.Debug.WriteLine($"Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Response Content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var registerResponse = JsonSerializer.Deserialize<RegisterResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return new ApiResponse<RegisterResponse> { Success = true, Data = registerResponse };
                }

                var errorResponse = TryParseErrorResponse(responseContent);
                var errorMessage = errorResponse ?? $"Registration failed: {response.StatusCode}";

                // Add detailed error information for debugging
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    errorMessage = $"Register endpoint not found (404). Please check if the API server is running correctly at {_baseUrl}/auth/register\n\nOriginal error: {errorMessage}";
                }

                return new ApiResponse<RegisterResponse>
                {
                    Success = false,
                    Message = errorMessage
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<RegisterResponse>
                {
                    Success = false,
                    Message = $"API connection error: {ex.Message}\n\nPlease ensure the API server is running at {_baseUrl}"
                };
            }
        }

        public async Task<ApiResponse<LicenseActivateResponse>> ActivateLicenseTokenlessAsync(string email, string password, string licenseKey)
        {
            try
            {
                var request = new LicenseActivateTokenlessRequest
                {
                    Email = email,
                    Password = password,
                    LicenseKey = licenseKey
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Debug information
                System.Diagnostics.Debug.WriteLine($"POST URL: {_httpClient.BaseAddress}/api/license/activate-tokenless");
                System.Diagnostics.Debug.WriteLine($"Request JSON: {json}");

                var response = await _httpClient.PostAsync("/api/license/activate-tokenless", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Debug response
                System.Diagnostics.Debug.WriteLine($"Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Response Content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var activateResponse = JsonSerializer.Deserialize<LicenseActivateResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return new ApiResponse<LicenseActivateResponse> { Success = true, Data = activateResponse };
                }

                var errorResponse = TryParseErrorResponse(responseContent);
                var errorMessage = errorResponse ?? $"License activation failed: {response.StatusCode}";

                return new ApiResponse<LicenseActivateResponse>
                {
                    Success = false,
                    Message = errorMessage
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<LicenseActivateResponse>
                {
                    Success = false,
                    Message = $"API connection error: {ex.Message}\n\nPlease ensure the API server is running at {_baseUrl}"
                };
            }
        }

        public async Task<ApiResponse<LicenseActivateResponse>> ActivateLicenseAsync(string licenseKey)
        {
            try
            {
                var request = new LicenseActivateRequest
                {
                    LicenseKey = licenseKey
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Debug information
                System.Diagnostics.Debug.WriteLine($"POST URL: {_httpClient.BaseAddress}/api/License/activate");
                System.Diagnostics.Debug.WriteLine($"Authorization Header: {_httpClient.DefaultRequestHeaders.Authorization}");
                System.Diagnostics.Debug.WriteLine($"Raw Token: {_jwtToken}");
                System.Diagnostics.Debug.WriteLine($"Request JSON: {json}");

                // Log all headers being sent
                foreach(var header in _httpClient.DefaultRequestHeaders)
                {
                    System.Diagnostics.Debug.WriteLine($"Header: {header.Key} = {string.Join(", ", header.Value)}");
                }

                // Double-check authorization is set before sending
                if (_httpClient.DefaultRequestHeaders.Authorization == null)
                {
                    System.Diagnostics.Debug.WriteLine("CRITICAL: Authorization header is NULL before sending request!");
                    if (!string.IsNullOrEmpty(_jwtToken))
                    {
                        System.Diagnostics.Debug.WriteLine("Attempting to re-set authorization header...");
                        SetAuthorizationHeader(_jwtToken);
                    }
                }

                var response = await _httpClient.PostAsync("/api/License/activate", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Debug response
                System.Diagnostics.Debug.WriteLine($"Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Response Content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var activateResponse = JsonSerializer.Deserialize<LicenseActivateResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return new ApiResponse<LicenseActivateResponse> { Success = true, Data = activateResponse };
                }

                var errorResponse = TryParseErrorResponse(responseContent);
                var errorMessage = errorResponse ?? $"License activation failed: {response.StatusCode}";

                // Add detailed error information for debugging
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    errorMessage = $"Unauthorized (401). Please login first. Token might be missing or invalid.\n\nOriginal error: {errorMessage}";
                }

                return new ApiResponse<LicenseActivateResponse>
                {
                    Success = false,
                    Message = errorMessage
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<LicenseActivateResponse>
                {
                    Success = false,
                    Message = $"API connection error: {ex.Message}\n\nPlease ensure the API server is running at {_baseUrl}"
                };
            }
        }

        public async Task<ApiResponse<LicenseValidateResponse>> ValidateLicenseAsync()
        {
            try
            {
                // Debug information
                System.Diagnostics.Debug.WriteLine($"GET URL: {_httpClient.BaseAddress}/api/License/validate");
                System.Diagnostics.Debug.WriteLine($"Authorization Header: {_httpClient.DefaultRequestHeaders.Authorization}");

                var response = await _httpClient.GetAsync("/api/License/validate");
                var responseContent = await response.Content.ReadAsStringAsync();

                // Debug response
                System.Diagnostics.Debug.WriteLine($"Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"Response Content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var validateResponse = JsonSerializer.Deserialize<LicenseValidateResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return new ApiResponse<LicenseValidateResponse> { Success = true, Data = validateResponse };
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return new ApiResponse<LicenseValidateResponse>
                    {
                        Success = false,
                        Message = "Session expired. Please login again."
                    };
                }

                var errorResponse = TryParseErrorResponse(responseContent);
                return new ApiResponse<LicenseValidateResponse>
                {
                    Success = false,
                    Message = errorResponse ?? $"License validation failed: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<LicenseValidateResponse>
                {
                    Success = false,
                    Message = $"API connection error: {ex.Message}\n\nPlease ensure the API server is running at {_baseUrl}"
                };
            }
        }

        private string? TryParseErrorResponse(string responseContent)
        {
            if (string.IsNullOrWhiteSpace(responseContent))
                return null;

            try
            {
                var errorObj = JsonSerializer.Deserialize<ErrorResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                var message = errorObj?.Message ?? errorObj?.Error;

                // If we got a valid parsed object but no message, return the raw content
                if (string.IsNullOrWhiteSpace(message))
                    return responseContent.Trim();

                return message;
            }
            catch
            {
                // If JSON parsing fails, return the raw content (might be plain text error)
                return responseContent.Trim();
            }
        }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string HWID { get; set; } = "";
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public UserInfo User { get; set; } = new UserInfo();
    }

    public class UserInfo
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime? TrialEndDate { get; set; }
        public DateTime? LicenseEndDate { get; set; }
        public bool IsTrialUsed { get; set; }
        public bool HasActiveLicense { get; set; }
        public string LicenseStatus { get; set; } = "";
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
        public string HWID { get; set; } = "";
    }

    public class RegisterResponse
    {
        public string Message { get; set; } = "";
        public bool Success { get; set; }
    }

    public class LicenseActivateTokenlessRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string LicenseKey { get; set; } = "";
    }

    public class LicenseActivateRequest
    {
        [JsonPropertyName("licenseKey")]
        public string LicenseKey { get; set; } = "";
    }

    public class LicenseActivateResponse
    {
        public bool Success { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Message { get; set; } = "";
    }

    public class LicenseValidateResponse
    {
        public bool IsValid { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int DaysRemaining { get; set; }
    }

    public class ErrorResponse
    {
        public string? Message { get; set; }
        public string? Error { get; set; }
    }
}