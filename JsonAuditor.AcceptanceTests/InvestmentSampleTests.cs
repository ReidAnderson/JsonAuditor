using System;
using System.Collections.Generic;
using Xunit;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;

namespace JsonAuditor.AcceptanceTests
{
    public class InvestmentSampleTests
    {
        private readonly TestServer _server;
        private readonly HttpClient _client;
        private Fixture _fixture;

        public InvestmentSampleTests()
        {
            _server = new TestServer(new WebHostBuilder()
                .UseStartup<Startup>());
            _client = _server.CreateClient();
            _fixture = new Fixture();
        }

        private async Task UpdateAndCheckOutput(AuditRequest newRecord)
        {
            var postResponse = await _client.PostAsJsonAsync("/Audit", newRecord);

            Assert.True(postResponse.IsSuccessStatusCode);

            var response = await _client.GetAsync($"/Audit?entityId={newRecord.EntityId}&entityType={newRecord.EntityType}");

            Assert.True(response.IsSuccessStatusCode);

            string output = await response.Content.ReadAsStringAsync();

            string expected = JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(newRecord.Entity));
            string actual = JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(output));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async void CheckInvestments()
        {
            string identifier = Guid.NewGuid().ToString();

            for (int i = 0; i < 10; i++)
            {
                Investment investment = _fixture.Create<Investment>();
                investment.InvestmentGuid = identifier;

                AuditRequest request = new AuditRequest()
                {
                    UniqueIdentifier = new Guid().ToString(),
                    TransactionDateTime = DateTime.UtcNow,
                    EntityId = investment.InvestmentGuid,
                    EntityType = EntityType.Generic,
                    Entity = JsonSerializer.Serialize<Investment>(investment)
                };

                await UpdateAndCheckOutput(request);
            };

            var response = await _client.GetAsync($"/Audit/all?entityId={identifier}&entityType={EntityType.Generic}");

            Assert.True(response.IsSuccessStatusCode);

            List<AuditRecord> output = await response.Content.ReadAsAsync<List<AuditRecord>>();

            Assert.Equal(10, output.Count);
        }

        [Fact]
        public async void EnsureCompression()
        {
            string identifier = Guid.NewGuid().ToString();

            int totalObjectSize = 0;
            int totalStoredSize = 0;

            // We hold components static, so the overall size footprint should be lower for the patch records
            List<InvestmentComponents> components = _fixture.Create<List<InvestmentComponents>>();

            for (int i = 0; i < 10; i++)
            {
                Investment investment = _fixture.Create<Investment>();
                investment.InvestmentGuid = identifier;
                investment.InvestmentComponents = components;

                string content = JsonSerializer.Serialize<Investment>(investment);
                AuditRequest request = new AuditRequest()
                {
                    UniqueIdentifier = new Guid().ToString(),
                    TransactionDateTime = DateTime.UtcNow,
                    EntityId = investment.InvestmentGuid,
                    EntityType = EntityType.Generic,
                    Entity = content
                };

                await UpdateAndCheckOutput(request);

                totalObjectSize += content.Length * sizeof(Char);
            };

            var response = await _client.GetAsync($"/Audit/all?entityId={identifier}&entityType={EntityType.Generic}");

            Assert.True(response.IsSuccessStatusCode);

            List<AuditRecord> output = await response.Content.ReadAsAsync<List<AuditRecord>>();

            foreach (AuditRecord record in output)
            {
                totalStoredSize += record.Record.Length * sizeof(Char);
            }

            Assert.Equal(10, output.Count);
            Assert.True(totalStoredSize < totalObjectSize);
        }
    }

    class Investment
    {
        public string InvestmentGuid { get; set; }
        public string InvestmentTypeCode { get; set; }
        public string InvestmentProvider { get; set; }
        public DateTimeOffset? PurchasedDateTime { get; set; }
        public DateTimeOffset? PositionEndDateTime { get; set; }
        public double? Odds { get; set; }
        public double Cost { get; set; }
        public string CurrencyCode { get; set; }
        public double? Revenue { get; set; }
        public string Status { get; set; }
        public bool? Paid { get; set; }
        public ICollection<InvestmentComponents> InvestmentComponents { get; set; }
        public ICollection<AttributeObj> InvestmentAttributes { get; set; }
        public ICollection<string> Notes { get; set; }
    }

    class InvestmentComponents
    {
        public string InvestmentComponentGuid { get; set; }
        public string InvestmentMarketCode { get; set; }
        public string InvestmentStyleCode { get; set; }
        public string InvestmentEntityTypeCode { get; set; }
        public string InvestmentEntity { get; set; }
        public string InvestmentEntityId { get; set; }
        public string InvestmentStyleValue { get; set; }
        public long? Quantity { get; set; }
        public double? Odds { get; set; }
        public double? Result { get; set; }
        public DateTimeOffset? Date { get; set; }
        public ICollection<string> Notes { get; set; }
    }

    class AttributeObj
    {
        public string AttributeCode { get; set; }
        public DateTime Beginning { get; set; }
        public DateTime? End { get; set; }
        public string AttributeValue { get; set; }
    }
}