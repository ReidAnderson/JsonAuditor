using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Data.Sqlite;
using System.Data;
using NetPatch;
using System.Text.Json;
using newt = Newtonsoft.Json;
using System.Dynamic;

namespace JsonAuditor.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly object sqliteLock = new object();
        private readonly ILogger<AuditController> _logger;

        public AuditController(ILogger<AuditController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public string Get(string entityId, EntityType entityType, DateTime? auditTime)
        {
            if (auditTime.HasValue)
            {
                return GetTimeRecord(entityId, entityType, auditTime.Value);
            }
            else
            {
                return GetLatestRecord(entityId, entityType);
            }
        }

        [HttpPost]
        public Guid Post(AuditRequest request)
        {
            Guid auditId = Guid.NewGuid();

            AuditRecord record = GetLatestAuditRecord(request.EntityId, request.EntityType);

            AuditRecord newRecord = new AuditRecord()
            {
                AuditId = auditId.ToString(),
                ParentAuditId = record?.AuditId,
                EntityId = request.EntityId,
                EntityType = request.EntityType ?? EntityType.Generic,
                TransactionTime = request.TransactionDateTime,
                AuditTime = DateTime.UtcNow,
                AutoResolved = false,
                Record = record == null ? request.Entity : newt.JsonConvert.SerializeObject(PatchHelper.GetPatchForObject(record.Record, request.Entity)) // only patch if we have a previous record
            };

            WriteAuditRecord(newRecord);

            return Guid.NewGuid();
        }

        private string GetTimeRecord(string entityId, EntityType? entityType, DateTime time)
        {
            List<AuditRecord> result = new List<AuditRecord>();

            foreach (AuditRecord record in GetAuditRecords(entityId, entityType))
            {
                if (record.TransactionTime < time)
                {
                    result.Add(record);
                }
            }

            string originalRecord = null;
            foreach (AuditRecord auditRecord in result)
            {
                if (auditRecord.ParentAuditId == null)
                {
                    originalRecord = auditRecord.Record;
                }
                else
                {
                    dynamic obj = newt.JsonConvert.DeserializeObject<ExpandoObject>(originalRecord);
                    JsonPatchDocument patchDoc = newt.JsonConvert.DeserializeObject<JsonPatchDocument>(auditRecord.Record);
                    patchDoc.ApplyTo(obj);
                    originalRecord = newt.JsonConvert.SerializeObject(obj);
                }
            }

            return originalRecord;
        }

        private string GetLatestRecord(string entityId, EntityType? entityType)
        {
            // GetAuditRecords returns in transactionTime order
            List<AuditRecord> auditRecords = GetAuditRecords(entityId, entityType);

            string originalRecord = null;
            foreach (AuditRecord auditRecord in auditRecords)
            {
                if (auditRecord.ParentAuditId == null)
                {
                    originalRecord = auditRecord.Record;
                }
                else
                {
                    dynamic obj = newt.JsonConvert.DeserializeObject<ExpandoObject>(originalRecord);
                    JsonPatchDocument patchDoc = newt.JsonConvert.DeserializeObject<JsonPatchDocument>(auditRecord.Record);
                    patchDoc.ApplyTo(obj);
                    originalRecord = newt.JsonConvert.SerializeObject(obj);
                }
            }

            return originalRecord;
        }

        private void WriteAuditRecord(AuditRecord auditRecord)
        {
            lock (sqliteLock)
            {
                using (var connection = new SqliteConnection("Data Source=Auditor.db"))
                {
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = @"INSERT INTO auditRecords (AuditId, ParentAuditId, EntityId, EntityType, TransactionTime, AuditTime, AutoResolved, Record) 
                                        VALUES ($aid, $paid, $ei, $et, $tt,$at, $ar, $r)";
                    command.Parameters.AddWithValue("$aid", auditRecord.AuditId);

                    if (auditRecord.ParentAuditId != null)
                    {
                        command.Parameters.AddWithValue("$paid", auditRecord.ParentAuditId);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("$paid", DBNull.Value);
                    }

                    command.Parameters.AddWithValue("$ei", auditRecord.EntityId);
                    command.Parameters.AddWithValue("$et", (int)auditRecord.EntityType);
                    command.Parameters.AddWithValue("$tt", convertDateTimeToEpoch(auditRecord.TransactionTime));
                    command.Parameters.AddWithValue("$at", convertDateTimeToEpoch(auditRecord.AuditTime));
                    command.Parameters.AddWithValue("$ar", auditRecord.AutoResolved ? 1 : 0);
                    command.Parameters.AddWithValue("$r", auditRecord.Record);

                    command.ExecuteNonQuery();
                }
            }
        }

        private long convertDateTimeToEpoch(DateTime time)
        {
            DateTime epoch = new DateTime(1970, 1, 1);

            return (long)time.Subtract(epoch).TotalMilliseconds;
        }

        private AuditRecord GetLatestAuditRecord(string entityId, EntityType? entityType)
        {
            AuditRecord result = null;

            using (var connection = new SqliteConnection("Data Source=Auditor.db"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"SELECT AuditId, ParentAuditId, EntityId, EntityType, TransactionTime, AuditTime, AutoResolved, Record 
                                        FROM auditRecords WHERE EntityId=$entity AND EntityType=$type ORDER BY TransactionTime DESC LIMIT 1";
                command.Parameters.AddWithValue("$entity", entityId);
                command.Parameters.AddWithValue("$type", entityType ?? EntityType.Generic);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        result = MapAuditRecord(reader);
                    }
                }
            }

            if (result != null)
            {
                // TODO this is obviously a concurrency issue. If something comes in between the read above and this call we are in trouble
                result.Record = GetLatestRecord(entityId, entityType);
            }

            return result;
        }

        private List<AuditRecord> GetAuditRecords(string entityId, EntityType? entityType)
        {
            List<AuditRecord> result = new List<AuditRecord>();

            using (var connection = new SqliteConnection("Data Source=Auditor.db"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"SELECT AuditId, ParentAuditId, EntityId, EntityType, TransactionTime, AuditTime, AutoResolved, Record 
                                        FROM auditRecords WHERE EntityId=$entity AND EntityType=$type ORDER BY TransactionTime ASC";
                command.Parameters.AddWithValue("$entity", entityId);
                command.Parameters.AddWithValue("$type", (int)(entityType ?? EntityType.Generic));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        AuditRecord audit = MapAuditRecord(reader);

                        result.Add(audit);
                    }
                }
            }

            return result;
        }

        private AuditRecord MapAuditRecord(IDataReader reader)
        {
            return new AuditRecord()
            {
                AuditId = reader.GetString(0),
                ParentAuditId = reader[1] != DBNull.Value ? reader.GetString(1) : null,
                EntityId = reader.GetString(2),
                EntityType = (EntityType)int.Parse(reader.GetString(3)),
                TransactionTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(reader.GetString(4))).UtcDateTime,
                AuditTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(reader.GetString(5))).UtcDateTime,
                AutoResolved = Int32.Parse(reader.GetString(6) ?? "0") != 0,
                Record = reader.GetString(7)
            };
        }


    }
}
