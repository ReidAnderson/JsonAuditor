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

        public async void UpdateAndCheckOutput(AuditRequest newRecord) {
            await _client.PostAsJsonAsync("/Audit", newRecord);

            var response = await _client.GetAsync($"/Audit?entityId={newRecord.EntityId}&entityType={newRecord.EntityType}");
            
            Assert.True(response.IsSuccessStatusCode);

            string output = await response.Content.ReadAsStringAsync();
            Assert.True(JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(newRecord.Entity)) == JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(output)));
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

            UpdateAndCheckOutput(request);

            request = new AuditRequest() {
                UniqueIdentifier = new Guid().ToString(),
                TransactionDateTime = DateTime.UtcNow,
                EntityId = entityId,
                EntityType = EntityType.Generic,
                Entity = "{\"something\":\"more\"}"
            };

            UpdateAndCheckOutput(request);
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

            UpdateAndCheckOutput(request);

            request = new AuditRequest() {
                UniqueIdentifier = new Guid().ToString(),
                TransactionDateTime = DateTime.UtcNow.AddHours(5),
                EntityId = entityId,
                EntityType = EntityType.Generic,
                Entity = "{\"something\":\"more\",\"anything\":\"else\"}"
            };

            UpdateAndCheckOutput(request);

            var response = await _client.GetAsync($"/Audit?entityId={entityId}&entityType=0&auditTime={request.TransactionDateTime.AddHours(-2)}");
            var output = await response.Content.ReadAsStringAsync();
            Assert.True(output == "{\"something\":\"here\"}");
        }
    }
}
