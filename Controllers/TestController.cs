using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;
using System;
using System.Collections.Generic;

namespace HMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly QueryFactory db;
        public TestController(QueryFactory db)
        {
            this.db = db;
        }


        [HttpGet]
        public IActionResult Index() {
            IEnumerable<Object> clients = db.Query("common.client").Select("client_id", "name").Get();

            return Ok(clients);
        }
    }
}
