using Microsoft.Identity.Client; // Microsoft Authentication Library (MSAL)
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace PowerApps.Samples
{
    /// <summary>
    /// Demonstrates Azure authentication and execution of a Dataverse Web API function.
    /// </summary>
    class Program
    {
        static async Task Main()
        {
            // Dataverse environment URL (replace with your environment's URL)
            string resource = "https://pas-poc-dev.api.crm.dynamics.com";

            // Azure Active Directory App Registration credentials
            string clientId = "ac51513c-5916-4039-b4c8-1aa2685637bb";
            string clientSecret = Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_SECRET") 
                                  ?? throw new InvalidOperationException("Client secret not found in environment variables.");
            string tenantId = "bd804b61-4e2b-4ca6-b036-a79cb2f80e31"; // Replace with your Azure AD tenant ID

            // Authority URL for Azure AD
            string authority = $"https://login.microsoftonline.com/{tenantId}";

            try
            {
                #region Authentication

                // Build MSAL client
                var authBuilder = ConfidentialClientApplicationBuilder.Create(clientId)
                                    .WithClientSecret(clientSecret)
                                    .WithAuthority(new Uri(authority))
                                    .Build();

                // Set scope for Dataverse API
                string[] scopes = { $"{resource}/.default" };

                // Acquire token for client
                AuthenticationResult token = await authBuilder.AcquireTokenForClient(scopes).ExecuteAsync();
                Console.WriteLine("Successfully authenticated!");

                #endregion Authentication

                #region Client Configuration

                // Configure HttpClient for Dataverse API
                using var client = new HttpClient
                {
                    BaseAddress = new Uri($"{resource}/api/data/v9.2/"),
                    Timeout = TimeSpan.FromMinutes(2) // Standard 2-minute timeout
                };

                // Add default headers
                HttpRequestHeaders headers = client.DefaultRequestHeaders;
                headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                headers.Add("OData-MaxVersion", "4.0");
                headers.Add("OData-Version", "4.0");
                headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                #endregion Client Configuration

                #region Web API Call

                // Invoke the Web API 'WhoAmI' unbound function
                HttpResponseMessage response = await client.GetAsync("WhoAmI");

                if (response.IsSuccessStatusCode)
                {
                    // Parse the JSON formatted service response to obtain the user ID value
                    string jsonContent = await response.Content.ReadAsStringAsync();

                    using JsonDocument doc = JsonDocument.Parse(jsonContent);
                    Guid userId = doc.RootElement.GetProperty("UserId").GetGuid();

                    Console.WriteLine($"Your user ID is {userId}");
                }
                else
                {
                    Console.WriteLine($"Web API call failed: {response.StatusCode} - {response.ReasonPhrase}");
                }

                #endregion Web API Call
            }
            catch (MsalServiceException ex)
            {
                Console.WriteLine($"Authentication error: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// WhoAmIResponse class definition 
    /// </summary>
    /// <remarks>To be used for JSON deserialization.</remarks>
    /// <see cref="https://learn.microsoft.com/power-apps/developer/data-platform/webapi/reference/whoamiresponse"/>
    public class WhoAmIResponse
    {
        public Guid BusinessUnitId { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
    }
}
