using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientListController : ControllerBase
    {
        private readonly QueryFactory db;

        private readonly ILog log = LogManager.GetLogger("mylog");

        public ClientListController(QueryFactory db)
        {
            this.db = db;
                    }


        [HttpPost]
        public IActionResult ClientList(JsonElement parameters)
        {
            dynamic json = JsonConvert.DeserializeObject(parameters.ToString());
 
            int pageNumber = (json.pageNumber != null) ? json.pageNumber : 1;
            int  rowsPerPage = (json.rowsPerPage != null) ? json.rowsPerPage : 10;
            string sortBy = (json.sortBy != null) ? json.sortBy : "name";
            bool  sortDesc = (json.sortDesc != null) ? json.sortDesc : false;

            string searchFromJson = json.search;
            int offset = pageNumber * rowsPerPage - rowsPerPage;
            string searchLike = "%" + searchFromJson + "%";

            searchLike = searchLike.Replace(";", "");

            int totalClient = this.FetchNumberOfClients(searchLike);

            IEnumerable<Object> sortedClients = CheckNullValue( this.FetchSortedClientsList(searchLike, sortBy, sortDesc, rowsPerPage, offset));
            object returnedList = new { sortedClients = sortedClients, totalClient = totalClient };
            return Ok(returnedList);
        }


        private int FetchNumberOfClients(string searchLike)
        {
            SqlKata.Query queryCount = this.db.Query().From(
               subQuery => subQuery.From("common.client")
                .Select("name")
                .SelectRaw(@"info#>>'\{company,address\}'AS Adres")
                .SelectRaw(@"info#>>'\{company,city\}'AS Miasto")
                .SelectRaw(@"info#>>'\{company,nip\}'AS NIP")
                .SelectRaw(@"info#>>'\{contact,phone\}'AS Telefosn")
                .SelectRaw(@"info#>>'\{contact,email\}'AS Email")
                .SelectRaw(@"info#>>'\{contact,contact_name\}'AS Kontakt")
                .WhereRaw(@"common.client.info#>'\{company,address\} like '" + @searchLike+"'")
                 .As("sub")).Where(q =>
                            q.WhereLike("adres", @searchLike)
                             .OrWhereLike("miasto", @searchLike)
                             .OrWhereLike("nip", @searchLike)
                             .OrWhereLike("email", @searchLike)
                             .OrWhereLike("kontakt", @searchLike)
                             .OrWhereLike("name", @searchLike)
                                ).AsCount();

            Console.WriteLine(db.Compiler.Compile(queryCount).Sql);
            int totalClient = Convert.ToInt32(queryCount.First().count);

            return totalClient;
        }

        private IEnumerable<Object> FetchSortedClientsList(string searchLike, string sortBy, bool sortDesc, int rowsPerPage, int offset)
        {
            SqlKata.Query sortedClientsQuery = this.db.Query().From(
               subQuery => subQuery.From("common.client")
                .Select("name")
                .SelectRaw(@"info#>>'\{company,address\}'AS Adres")
                .SelectRaw(@"info#>>'\{company,city\}'AS Miasto")
                .SelectRaw(@"info#>>'\{company,nip\}'AS NIP")
                .SelectRaw(@"info#>>'\{contact,phone\}'AS Telefon")
                .SelectRaw(@"info#>>'\{contact,email\}'AS Email")
                .SelectRaw(@"info#>>'\{contact,contact_name\}'AS Kontakt")
                .As("sub"))
                .Where(q =>
                            q.WhereLike("adres", @searchLike)
                             .OrWhereLike("miasto", @searchLike)
                             .OrWhereLike("nip", @searchLike)
                             .OrWhereLike("email", @searchLike)
                             .OrWhereLike("kontakt", @searchLike)
                             .OrWhereLike("name", @searchLike)
                                )
               .Limit(rowsPerPage)
              .Offset(offset); 

            if (sortDesc == false) { sortedClientsQuery.OrderBy(sortBy); }
            else { sortedClientsQuery.OrderByDesc(sortBy); };
            //Console.WriteLine(db.Compiler.Compile(sortedDevicesQuery).Sql);

            IEnumerable<Object> sortedClients = new List<object>();

            try
            {
                sortedClients = sortedClientsQuery.Get();
            }
            catch (Exception e)
            {
                log.Error(e);
            }


            return sortedClients;
        }

        //przenies do zapytania sql -> coalesce
        private IEnumerable<Object> CheckNullValue(IEnumerable<Object> sortedDevices)
        {
            string[] clientData = { "name", "miasto", "adres", "nip", "telefon", "email", "kontakt" };

            foreach (IDictionary<string, object> row in sortedDevices)
            {
                foreach (string i in clientData)
                {
                    if (row[i] is null) {
                        row[i] = "Brak danych";
                        continue;
                    }
                };
            };

            return sortedDevices;
        }


    }
}
