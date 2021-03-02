using System;

namespace JsonAuditor
{
    public class AuditRequest
    {
        public string UniqueIdentifier { get; set; }
        public DateTime TransactionDateTime { get; set; }
        public string EntityId { get; set; }
        public string Entity { get; set; }
    }
}