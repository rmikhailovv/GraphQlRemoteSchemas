using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tools.GraphQl.Schemas
{
    public class GraphQlRemoteSchema : GraphQlSchema
    {
        private static readonly TimeSpan DefaultFetchInterval = TimeSpan.FromMilliseconds(0);
        public HttpClient Client { get; }

        private readonly double _fetchInterval;
        private double _lastFetchedAt;

        public GraphQlRemoteSchema(string name, string remoteUrl, TimeSpan? fetchInterval = null) : this(name,
            new HttpClient { BaseAddress = new Uri(remoteUrl) }, fetchInterval)
        {
        }

        public GraphQlRemoteSchema(string name, HttpClient client, TimeSpan? fetchInterval = null) : base(name)
        {
            Client = client;
            _fetchInterval = (fetchInterval ?? DefaultFetchInterval).TotalMilliseconds;
        }

        public async Task LoadFromRemoteAsync(CancellationToken cancellationToken = default)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - _lastFetchedAt < _fetchInterval) return;

            string introspection = await IntrospectionGetAsync(cancellationToken);
            if (string.IsNullOrEmpty(introspection)) return;

            var introspectionContents = JToken.Parse(introspection);
            LoadFromIntrospection(introspectionContents);

            _lastFetchedAt = now;
        }

        private async Task<string> IntrospectionGetAsync(CancellationToken cancellationToken = default)
        {
            var request =
                new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    Content = new StringContent(
                        JsonConvert.SerializeObject(new { query = IntrospectionQuery.Text }), Encoding.UTF8,
                        "application/json")
                };


            HttpResponseMessage response = await Client.SendAsync(request, cancellationToken);

            string content = await response.Content.ReadAsStringAsync();
            return content;
        }

    }
}