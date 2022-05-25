using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions.TZ;
using Microsoft.Data.Sqlite;

namespace Demo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //TestSqlServer();
            //await CreateCountryTable();
            //await TestSqlServerBulk();
            //await TestSqlServerBulkUpdate();
            //await TestSqlServerDelete();
            await TestSqlServerBulkGet();
            Console.ReadKey();
            //Console.WriteLine("Hello World!");
        }

        private static void TestSqlServer()
        {
            //数据库连接字符串
            string ConnectionString = "Server=localhost;Database=Test;Trusted_Connection=True;MultipleActiveResultSets=true";
            var PageSize = 5;
            var PageIndex = 1;
            var whereSql = " where CountryId>@id ";
            var dicParms = new Dictionary<string, object>();
            dicParms.Add("@id", 0);
            var sortBySql = " order by CountryId asc ";
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                var pageResult = conn.Pager<CountryEntity>(PageSize, PageIndex, whereSql, sortBySql, dicParms);
                var a = pageResult.Item1;
                var list = pageResult.list;
                Console.WriteLine(pageResult.Item2);
            }
        }
        private static void TestSqlite()
        {
            //数据库连接字符串
            string ConnectionString = System.AppDomain.CurrentDomain.BaseDirectory + "\\test.db";
            if (!File.Exists(ConnectionString))
            {
                return;
            }
            var PageSize = 5;
            var PageIndex = 0;
            var whereSql = " where Id>@id ";
            var dicParms = new Dictionary<string, object>();
            dicParms.Add("@id", 0);
            var sortBySql = " order by id desc ";
            using (var conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                var pageResult = conn.Pager<UserEntity>(PageSize, PageIndex, whereSql, sortBySql, dicParms);
            }
        }



        private static async Task TestSqlServerBulk()
        {
            //数据库连接字符串
            string ConnectionString = "Server=localhost;Database=Test;Trusted_Connection=True;MultipleActiveResultSets=true";
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                var tran = conn.BeginTransaction();
                try
                {

                    var countryList = new List<CountryEntity>();
                    for (var i = 0; i < 100; i++)
                    {
                        var country = new CountryEntity();
                        country.CountryId = i + 1;
                        country.CnName = "国家测试" + i;
                        country.IsDeleted = 0;
                        country.Latitude = 1.234M;
                        countryList.Add(country);
                    }
                    var success = await conn.BulkInsertAsync(countryList, tran);

                    var country2 = new CountryEntity();
                    country2.CountryId = 101;
                    country2.CnName = "国家测试";
                    var result2 = await conn.InsertAsync(country2, tran);
                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                }
            }
        }


        private static async Task TestSqlServerBulkUpdate()
        {
            //数据库连接字符串
            string ConnectionString = "Server=localhost;Database=Test;Trusted_Connection=True;MultipleActiveResultSets=true";
            List<CountryEntity> countryList = null;
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                var sortBySql = " order by CountryId asc ";
                Dictionary<string, object> dicParms = new Dictionary<string, object>();
                dicParms.Add("@id", 100);
                var countryList1 = await conn.GetAllAsync<CountryEntity>(" where CountryId<=@id ", dicParms, sortBySql);
                countryList = countryList1.ToList();

                for (var i = 0; i < countryList.Count; i++)
                {
                    countryList[i].CnAlias = "国家别名测试" + countryList[i].CountryId;
                    countryList[i].CnName = "国家测试-" + countryList[i].CountryId;
                }
            }
            
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                var tran = conn.BeginTransaction();
                try
                {
                    /*
                    var countryList = new List<CountryEntity>();
                    for (var i = 0; i < 100; i++)
                    {
                        var country = new CountryEntity();
                        country.CountryId = i + 1;
                        country.CnName = "国家测试" + i;
                        country.IsDeleted = 0;
                        country.Latitude = 1.234M;
                        country.CnAlias = "国家别名测试" + i;
                        countryList.Add(country);
                    }
                    */
                    
                    var updateProps = new string[] { nameof(CountryEntity.CnAlias),nameof(CountryEntity.CnName) };
                    var success = await conn.BulkUpdateAsync(countryList, updateProps, tran);

                    var country2 = new CountryEntity();
                    country2.CountryId = 103;
                    country2.CnName = "国家测试";
                    var result2 = conn.Insert(country2, tran);
                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                }
            }
        }


        private static async Task TestSqlServerBulkGet()
        {
            //数据库连接字符串
            string ConnectionString = "Server=localhost;Database=Test;Trusted_Connection=True;MultipleActiveResultSets=true";
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                var dicParams = new Dictionary<string, object>();
                dicParams.Add("@maxId", 10000);

                List<object> idList = new List<object>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,10,10,10 ,-100};
                KeyValuePair<string, List<object>> queryPropList = new KeyValuePair<string, List<object>>("CountryId", idList);
                List<object> nameList = new List<object>() { "国家测试-1","国家测试-2","国家测试-3","国家测试-3","国家测试-A3" };
                KeyValuePair<string, List<object>> queryPropList2 = new KeyValuePair<string, List<object>>("CnName", nameList);

                var entityList1 = await conn.BulkGetAsync<CountryEntity>(" where CountryId < @maxId ", dicParams, queryPropList);
                var entityList2 = await conn.BulkGetAsync<CountryEntity>(" where CountryId < @maxId ", dicParams, queryPropList2);
            }
        }

        private static async Task TestSqlServerDelete()
        {
            //数据库连接字符串
            string ConnectionString = "Server=localhost;Database=Test;Trusted_Connection=True;MultipleActiveResultSets=true";
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                var tran = conn.BeginTransaction();
                await conn.DeleteAsync<CountryEntity>(1, tran);
                tran.Commit();
            }
        }

        private static async Task CreateCountryTable()
        {
            var sql = @"
CREATE TABLE [dbo].[pub_Country](
	[CountryId] [int] NOT NULL,
	[CountryCode] [nvarchar](20) NULL,
	[TrdCode] [nvarchar](10) NULL,
	[CnName] [nvarchar](50) NULL,
	[Pingyin] [nvarchar](200) NULL,
	[IndexCode] [nvarchar](20) NULL,
	[CnAlias] [nvarchar](50) NULL,
	[EnName] [nvarchar](100) NULL,
	[EnAlias] [nvarchar](4000) NULL,
	[Continent] [nvarchar](80) NULL,
	[PhoneCode] [nvarchar](10) NULL,
	[TimeLag] [int] NULL,
	[Longitude] [decimal](15, 4) NULL,
	[Latitude] [decimal](15, 4) NULL,
	[DisplayOrder] [int] NULL,
	[IsHot] [int] NULL,
	[Status] [int] NULL,
	[IsDeleted] [int] NULL,
 CONSTRAINT [PK9_1] PRIMARY KEY NONCLUSTERED 
(
	[CountryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]";

            //数据库连接字符串
            string ConnectionString = "Server=localhost;Database=Test;Trusted_Connection=True;MultipleActiveResultSets=true";
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                var tran = conn.BeginTransaction();
                await conn.ExecuteAsync(sql,null,tran);
                tran.Commit();
            }
        }

    }


    [Table("User")]
    [Serializable]
    public class UserEntity
    {
        [ExplicitKey]
        public long Id { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }
    }


    [Table("pub_Country")]
    [Serializable]
    public partial class CountryEntity
    {
        /// <summary>
        /// CountryId
        /// </summary>
        [ExplicitKey]
        //[Key]
        //[Write(false)]
        public long CountryId { get; set; }

        /// <summary>
        /// CountryCode
        /// </summary>

        public string CountryCode { get; set; }

        /// <summary>
        /// TrdCode
        /// </summary>

        public string TrdCode { get; set; }

        /// <summary>
        /// Pingyin
        /// </summary>

        public string Pingyin { get; set; }

        /// <summary>
        /// IndexCode
        /// </summary>

        public string IndexCode { get; set; }

        /// <summary>
        /// CnAlias
        /// </summary>

        public string CnAlias { get; set; }

        /// <summary>
        /// EnName
        /// </summary>

        public string EnName { get; set; }

        /// <summary>
        /// EnAlias
        /// </summary>

        public string EnAlias { get; set; }

        /// <summary>
        /// Continent
        /// </summary>

        public string Continent { get; set; }

        /// <summary>
        /// PhoneCode
        /// </summary>

        public string PhoneCode { get; set; }

        /// <summary>
        /// TimeLag
        /// </summary>

        public int? TimeLag { get; set; }

        /// <summary>
        /// Longitude
        /// </summary>

        public decimal? Longitude { get; set; }

        /// <summary>
        /// Latitude
        /// </summary>

        public decimal? Latitude { get; set; }

        /// <summary>
        /// DisplayOrder
        /// </summary>

        public int? DisplayOrder { get; set; }

        /// <summary>
        /// IsHot
        /// </summary>

        public int? IsHot { get; set; }

        /// <summary>
        /// Status
        /// </summary>

        public int? Status { get; set; }

        /// <summary>
        /// IsDeleted
        /// </summary>

        public int? IsDeleted { get; set; }


        [Write(false)]
        public string Remark { get; set; }

        [Write(false)]
        public int LogId { get; set; }

        /// <summary>
        /// CnName
        /// </summary>

        public string CnName { get; set; }
    }


}
