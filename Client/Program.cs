using IdentityModel.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Client.Models;
using Newtonsoft.Json;

namespace Client
{
    public class Program
    {
        private static string IdpUri => "######";
        private static string ApiUri  => "#####";

            
        
        private static async Task Main()
        {
            // discover endpoints from metadata
            var client = new HttpClient();

            var disco = await client.GetDiscoveryDocumentAsync(IdpUri); // IDP
            if (disco.IsError)
            {
                Console.WriteLine(disco.Error);
                return;
            }

            // request token
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = "######",
                ClientSecret = "#####",

                Scope = "######"
            });
            
            if (tokenResponse.IsError)
            {
                Console.WriteLine(tokenResponse.Error);
                return;
            }

            Console.WriteLine(tokenResponse.Json);
            Console.WriteLine("\n\n");

            var token = tokenResponse.AccessToken;
            
            var apiClient = new HttpClient();
            apiClient.SetBearerToken(token);

            // GET Location lockers
            var response = await apiClient.GetAsync($"{ApiUri}/api/e-commerce/location/locker"); // API
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(response.StatusCode);
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                var lockers = JsonConvert.DeserializeObject<IEnumerable<ECommerceLocationLockersModel>>(content);
                var locker = lockers.FirstOrDefault();
                Console.WriteLine(locker?.LockerNumber);

                var order = await LoadLocker(locker?.QrCode, "SAMPle Order No", "Sample User Id", token);
                if (order == null)
                {
                    return;
                }
                
                Console.WriteLine(order.OtpCode, order.OrderId);

                var confirm = await Confirm(order.OrderId, token);

                if (confirm == null)
                {
                    return;
                }
                
                Console.WriteLine(confirm.ConfirmLink, confirm.DeliveryNumber, confirm.LockerNumber, token);

            }
        }

        private static async Task<ClientConfirmResult> Confirm(int orderId, string token)
        {
            var apiClient = new HttpClient();
            apiClient.SetBearerToken(token);
            var confirmResult = await apiClient.PutAsync($"{ApiUri}/api/e-commerce/client/load/{orderId}/confirm", null);

            if (!confirmResult.IsSuccessStatusCode)
            {
                return null;
            }

            var result =
                JsonConvert.DeserializeObject<ClientConfirmResult>(await confirmResult.Content.ReadAsStringAsync());
            return result;

        }
        

        private static async Task<LoadResult> LoadLocker(string qrCode, string orderNo, string userId, string token)
        {
            var apiClient = new HttpClient();
            apiClient.SetBearerToken(token);
            apiClient.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var model = new ClientLoadModel {OrderNo = orderNo, QrCode = qrCode, UserId = userId};
            
            // Load Locker
            var loadResponse = await apiClient.PostAsync($"{ApiUri}/api/e-commerce/client/load",
                new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json"));

            if (!loadResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var order = JsonConvert.DeserializeObject<LoadResult>(await loadResponse.Content.ReadAsStringAsync());
            return order;

        }
    }
}