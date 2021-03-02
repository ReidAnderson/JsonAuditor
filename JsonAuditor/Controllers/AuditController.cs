using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JsonAuditor.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuditController : ControllerBase
    {

        private readonly ILogger<AuditController> _logger;

        public AuditController(ILogger<AuditController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public Guid Post(AuditRequest request)
        {
            return Guid.NewGuid();
        }
    }
}
