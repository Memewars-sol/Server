using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net.Http;
using System.Threading.Tasks;

namespace WebUtils
{
    public class HttpSender
    {
        private static readonly HttpClient httpClient = new HttpClient();
        public static async Task<string> Patch(string url, Dictionary<string, string> values)
        {
            using (var content = new FormUrlEncodedContent(values))
            {
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = content
                };
                var response = await httpClient.SendAsync(request);

                return await response.Content.ReadAsStringAsync();
            }
        }

        public static async Task<string> Post(string url, Dictionary<string, string> values)
        {
            using (var content = new FormUrlEncodedContent(values))
            {
                var response = await httpClient.PostAsync(url, content);

                return await response.Content.ReadAsStringAsync();
            }
        }
        public static async Task<string> Get(string url, List<string> queryList = null)
        {
            //queryList must be list of strings of var=value
            string query = "";
            if (queryList != null && queryList.Count() > 0)
            {
                query = "?" + string.Join("&", queryList);
            }

            url += query;

            var response = await httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
        public static async Task<string> Get(string url, Dictionary<string, string> values = null)
        {
            //queryList must be list of strings of var=value
            string query = "";
            if(values != null && values.Count() > 0)
            {
                query = "?";

                string queryString = "";
                foreach(var value in values)
                {
                    queryString += value.Key + "=" + value.Value + "&";
                }

                //remove last &
                query += queryString.Substring(0, queryString.Length - 1);
            }

            url += query;

            var response = await httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> PostJson(string url, Dictionary<string, string> values)
        {
            //var content = JsonConvert.SerializeObject(values);

            var response = await httpClient.PostAsJsonAsync(url, values);
            return await response.Content.ReadAsStringAsync();
            
        }
    }
}
