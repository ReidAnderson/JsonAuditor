using System;

namespace JsonAuditor
{
    public enum EntityType
    {
        Generic = 0
    }

    public class AuditRequest
    {
        public string UniqueIdentifier { get; set; }
        public DateTime TransactionDateTime { get; set; }
        public string EntityId { get; set; }
        public EntityType? EntityType { get; set; }
        public string Entity { get; set; }
    }
}