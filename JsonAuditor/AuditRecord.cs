using System;
namespace JsonAuditor
{
    public class AuditRecord
    {
        public string AuditId { get; set; }
        public string ParentAuditId { get; set; }
        public DateTime TransactionTime { get; set; }
        public DateTime AuditTime { get; set; }
        public bool AutoResolved { get; set; }
        public string EntityId { get; set; }
        public EntityType EntityType { get; set; }
        public string Record { get; set; }
    }
}