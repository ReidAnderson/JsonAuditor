using System;
using Xunit;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.Diagnostics;

namespace JsonAuditor.AcceptanceTests
{
    public class AcceptanceTests
    {

        private readonly TestServer _server;
        private readonly HttpClient _client;

        public AcceptanceTests() {
            _server = new TestServer(new WebHostBuilder()
                .UseStartup<Startup>());
            _client = _server.CreateClient();
        }

        [Fact]
        public async void RoundTrip()
        {
            string entityId = Guid.NewGuid().ToString();

            AuditRequest request = new AuditRequest() {
                UniqueIdentifier = new Guid().ToString(),
                TransactionDateTime = DateTime.UtcNow,
                EntityId = entityId,
                EntityType = EntityType.Generic,
                Entity = "{\"something\":\"here\"}"
            };

            await _client.PostAsJsonAsync("/Audit", request);

            var response = await _client.GetAsync($"/Audit?entityId={entityId}&entityType=0");
            Debug.WriteLine(JsonSerializer.Serialize(response));
            
            Assert.True(response.IsSuccessStatusCode);

            string output = await response.Content.ReadAsStringAsync();
            Assert.True(request.Entity == output);

            request = new AuditRequest() {
                UniqueIdentifier = new Guid().ToString(),
                TransactionDateTime = DateTime.UtcNow,
                EntityId = entityId,
                EntityType = EntityType.Generic,
                Entity = "{\"something\":\"more\",\"anything\":\"else\"}"
            };

            await _client.PostAsJsonAsync("/Audit", request);

            response = await _client.GetAsync($"/Audit?entityId={entityId}&entityType=0");
            Debug.WriteLine(JsonSerializer.Serialize(response));
            
            Assert.True(response.IsSuccessStatusCode);

            output = await response.Content.ReadAsStringAsync();
            Assert.True(request.Entity == output);
        }

        [Fact]
        public async void GetRecordAtTime()
        {
            string entityId = Guid.NewGuid().ToString();

            AuditRequest request = new AuditRequest() {
                UniqueIdentifier = new Guid().ToString(),
                TransactionDateTime = DateTime.UtcNow,
                EntityId = entityId,
                EntityType = EntityType.Generic,
                Entity = "{\"something\":\"here\"}"
            };

            await _client.PostAsJsonAsync("/Audit", request);

            var response = await _client.GetAsync($"/Audit?entityId={entityId}&entityType=0");
            Debug.WriteLine(JsonSerializer.Serialize(response));
            
            Assert.True(response.IsSuccessStatusCode);

            string output = await response.Content.ReadAsStringAsync();
            Assert.True(request.Entity == output);

            request = new AuditRequest() {
                UniqueIdentifier = new Guid().ToString(),
                TransactionDateTime = DateTime.UtcNow.AddHours(5),
                EntityId = entityId,
                EntityType = EntityType.Generic,
                Entity = "{\"something\":\"more\",\"anything\":\"else\"}"
            };

            await _client.PostAsJsonAsync("/Audit", request);

            response = await _client.GetAsync($"/Audit?entityId={entityId}&entityType=0");
            Debug.WriteLine(JsonSerializer.Serialize(response));
            
            Assert.True(response.IsSuccessStatusCode);

            output = await response.Content.ReadAsStringAsync();
            Assert.True(request.Entity == output);

            response = await _client.GetAsync($"/Audit?entityId={entityId}&entityType=0&auditTime={request.TransactionDateTime.AddHours(-2)}");
            output = await response.Content.ReadAsStringAsync();
            Assert.True(output == "{\"something\":\"here\"}");
        }
    }
}