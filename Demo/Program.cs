using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using Dapper.Contrib.Extensions.TZ;
using Microsoft.Data.Sqlite;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            TestSqlServerBulk();
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



        private static void TestSqlServerBulk()
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
                    var success = conn.BulkInsert(countryList, tran);

                    var country2 = new CountryEntity();
                    country2.CountryId = 101;
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
