﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dapper.ProviderTools;

namespace Dapper.Contrib.Extensions.TZ
{

    //自定义扩展方法，基于Dapper-1.50.5 ，Dapper.Contrib-1.50.5
    public static partial class SqlMapperExtensions
    {
        #region 更新
        /// <summary>
        /// 更新指定字段
        /// </summary>
        /// <typeparam name="T">Type to be updated</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="entityToUpdate">Entity to be updated</param>
        /// <param name="whereSql">自定义条件（不按照id来更新）</param>
        /// <param name="updateProps">指定更新的字段</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <returns>true if updated, false if not found or not modified (tracked entities)</returns>
        public static bool Update<T>(this IDbConnection connection, T entityToUpdate, string whereSql = null, string[] updateProps = null, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            if (entityToUpdate is IProxy proxy && !proxy.IsDirty)
            {
                return false;
            }

            var type = typeof(T);

            if (type.IsArray)
            {
                type = type.GetElementType();
            }
            else if (type.IsGenericType)
            {
                var typeInfo = type.GetTypeInfo();
                bool implementsGenericIEnumerableOrIsGenericIEnumerable =
                    typeInfo.ImplementedInterfaces.Any(ti => ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
                    typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>);

                if (implementsGenericIEnumerableOrIsGenericIEnumerable)
                {
                    type = type.GetGenericArguments()[0];
                }
            }

            var keyProperties = KeyPropertiesCache(type).ToList();  //added ToList() due to issue #418, must work on a list copy
            var explicitKeyProperties = ExplicitKeyPropertiesCache(type);
            if (keyProperties.Count == 0 && explicitKeyProperties.Count == 0)
                throw new ArgumentException("Entity must have at least one [Key] or [ExplicitKey] property");

            var name = GetTableName(type);

            var sb = new StringBuilder();
            sb.AppendFormat("update {0} set ", name);

            var allProperties = TypePropertiesCache(type);
            keyProperties.AddRange(explicitKeyProperties);
            var computedProperties = ComputedPropertiesCache(type);
            var nonIdProps = allProperties.Except(keyProperties.Union(computedProperties)).ToList();

            var adapter = GetFormatter(connection);

            if (updateProps != null
                && updateProps.Length > 0)
            {
                #region 基于原方法更改的地方
                //SqlServerAdapter : ISqlAdapter此类中实现
                //sb.AppendFormat("[{0}] = @{1}", columnName, columnName); 
                // 只更新指定字段
                for (var i = 0; i < updateProps.Length; i++)
                {
                    var property = updateProps[i];
                    adapter.AppendColumnNameEqualsValue(sb, property);
                    if (i < updateProps.Length - 1)
                        sb.Append(", ");
                }
                #endregion
            }
            else
            {
                for (var i = 0; i < nonIdProps.Count; i++)
                {
                    var property = nonIdProps[i];
                    adapter.AppendColumnNameEqualsValue(sb, property.Name);  //fix for issue #336
                    if (i < nonIdProps.Count - 1)
                        sb.Append(", ");
                }
            }
            /*for (var i = 0; i < nonIdProps.Count; i++)
            {
                var property = nonIdProps[i];
                //排除不在指定更新的属性
                #region 基于原方法更改的地方
                if (updateProps != null
                    && updateProps.Length > 0
                    && !updateProps.Contains(property.Name))
                {
                    continue;
                }
                //SqlServerAdapter : ISqlAdapter此类中实现
                //sb.AppendFormat("[{0}] = @{1}", columnName, columnName); 
                #endregion
                adapter.AppendColumnNameEqualsValue(sb, property.Name);
                if (i < nonIdProps.Count - 1)
                    sb.Append(", ");
            }*/
            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
                sb.Append(whereSql);
            }
            else
            {
                sb.Append(" where ");
                for (var i = 0; i < keyProperties.Count; i++)
                {
                    var property = keyProperties[i];
                    adapter.AppendColumnNameEqualsValue(sb, property.Name); //fix for issue #336
                    if (i < keyProperties.Count - 1)
                        sb.Append(" and ");
                }
            }

            var updated = connection.Execute(sb.ToString(), entityToUpdate, commandTimeout: commandTimeout,
                transaction: transaction);
            return updated > 0;
        }

        /// <summary>
        /// 异步更新指定字段
        /// </summary>
        /// <typeparam name="T">Type to be updated</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="entityToUpdate">Entity to be updated</param>
        /// <param name="whereSql">自定义条件（不按照id来更新）</param>
        /// <param name="updateProps">指定更新的字段</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <returns>true if updated, false if not found or not modified (tracked entities)</returns>
        public static async Task<bool> UpdateAsync<T>(this IDbConnection connection, T entityToUpdate, string whereSql = null, string[] updateProps = null, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            if ((entityToUpdate is IProxy proxy) && !proxy.IsDirty)
            {
                return false;
            }

            var type = typeof(T);

            if (type.IsArray)
            {
                type = type.GetElementType();
            }
            else if (type.IsGenericType)
            {
                var typeInfo = type.GetTypeInfo();
                bool implementsGenericIEnumerableOrIsGenericIEnumerable =
                    typeInfo.ImplementedInterfaces.Any(ti => ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
                    typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>);

                if (implementsGenericIEnumerableOrIsGenericIEnumerable)
                {
                    type = type.GetGenericArguments()[0];
                }
            }

            var keyProperties = KeyPropertiesCache(type);
            var explicitKeyProperties = ExplicitKeyPropertiesCache(type);
            if (keyProperties.Count == 0 && explicitKeyProperties.Count == 0)
                throw new ArgumentException("Entity must have at least one [Key] or [ExplicitKey] property");

            var name = GetTableName(type);

            var sb = new StringBuilder();
            sb.AppendFormat("update {0} set ", name);

            var allProperties = TypePropertiesCache(type);
            keyProperties.AddRange(explicitKeyProperties);
            var computedProperties = ComputedPropertiesCache(type);
            var nonIdProps = allProperties.Except(keyProperties.Union(computedProperties)).ToList();

            var adapter = GetFormatter(connection);

            if (updateProps != null
                && updateProps.Length > 0)
            {
                #region 基于原方法更改的地方
                //SqlServerAdapter : ISqlAdapter此类中实现
                //sb.AppendFormat("[{0}] = @{1}", columnName, columnName); 
                // 只更新指定字段
                for (var i = 0; i < updateProps.Length; i++)
                {
                    var property = updateProps[i];
                    adapter.AppendColumnNameEqualsValue(sb, property);
                    if (i < updateProps.Length - 1)
                        sb.Append(", ");
                }
                #endregion
            }
            else
            {
                for (var i = 0; i < nonIdProps.Count; i++)
                {
                    var property = nonIdProps[i];
                    adapter.AppendColumnNameEqualsValue(sb, property.Name);  //fix for issue #336
                    if (i < nonIdProps.Count - 1)
                        sb.Append(", ");
                }
            }
            /*for (var i = 0; i < nonIdProps.Count; i++)
            {
                var property = nonIdProps[i];
                //排除不在指定更新的属性
                #region 基于原方法更改的地方
                if (updateProps != null
                    && updateProps.Length > 0
                    && !updateProps.Contains(property.Name))
                {
                    continue;
                }
                //SqlServerAdapter : ISqlAdapter此类中实现
                //sb.AppendFormat("[{0}] = @{1}", columnName, columnName); 
                #endregion
                adapter.AppendColumnNameEqualsValue(sb, property.Name);
                if (i < nonIdProps.Count - 1)
                    sb.Append(", ");
            }*/

            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
                sb.Append(whereSql);
            }
            else
            {
                sb.Append(" where ");
                for (var i = 0; i < keyProperties.Count; i++)
                {
                    var property = keyProperties[i];
                    adapter.AppendColumnNameEqualsValue(sb, property.Name);
                    if (i < keyProperties.Count - 1)
                        sb.Append(" and ");
                }
            }
            var updated = await connection.ExecuteAsync(sb.ToString(), entityToUpdate, commandTimeout: commandTimeout, transaction: transaction).ConfigureAwait(false);
            return updated > 0;
        }

        #endregion

        #region 删除

        /// <summary>
        /// 按照条件删除
        /// </summary>
        /// <typeparam name="T">Type of entity</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="entityToDelete">Entity to delete</param>
        /// <param name="whereSql">自定义条件（不按照实体属性）</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <returns>true if deleted, false if not found</returns>
        public static bool Delete<T>(this IDbConnection connection, T entityToDelete, string whereSql = null, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            if (entityToDelete == null)
                throw new ArgumentException("Cannot Delete null Object", nameof(entityToDelete));

            var type = typeof(T);

            if (type.IsArray)
            {
                type = type.GetElementType();
            }
            else if (type.IsGenericType)
            {
                var typeInfo = type.GetTypeInfo();
                bool implementsGenericIEnumerableOrIsGenericIEnumerable =
                    typeInfo.ImplementedInterfaces.Any(ti => ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
                    typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>);

                if (implementsGenericIEnumerableOrIsGenericIEnumerable)
                {
                    type = type.GetGenericArguments()[0];
                }
            }

            var keyProperties = KeyPropertiesCache(type).ToList();  //added ToList() due to issue #418, must work on a list copy
            var explicitKeyProperties = ExplicitKeyPropertiesCache(type);
            if (keyProperties.Count == 0 && explicitKeyProperties.Count == 0)
                throw new ArgumentException("Entity must have at least one [Key] or [ExplicitKey] property");

            var name = GetTableName(type);
            keyProperties.AddRange(explicitKeyProperties);

            var sb = new StringBuilder();
            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                sb.AppendFormat("delete from {0} ", name);
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
                sb.Append(whereSql);
            }
            else
            {
                sb.AppendFormat("delete from {0} where ", name);

                var adapter = GetFormatter(connection);

                for (var i = 0; i < keyProperties.Count; i++)
                {
                    var property = keyProperties[i];
                    adapter.AppendColumnNameEqualsValue(sb, property.Name);  //fix for issue #336
                    if (i < keyProperties.Count - 1)
                        sb.Append(" and ");
                }
            }

            var deleted = connection.Execute(sb.ToString(), entityToDelete, transaction, commandTimeout);
            return deleted > 0;
        }

        /// <summary>
        /// 异步按照条件删除
        /// </summary>
        /// <typeparam name="T">Type of entity</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="entityToDelete">Entity to delete</param>
        /// <param name="whereSql">自定义条件（不按照实体属性）</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <returns>true if deleted, false if not found</returns>
        public static async Task<bool> DeleteAsync<T>(this IDbConnection connection, T entityToDelete, string whereSql = null, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            if (entityToDelete == null)
                throw new ArgumentException("Cannot Delete null Object", nameof(entityToDelete));

            var type = typeof(T);

            if (type.IsArray)
            {
                type = type.GetElementType();
            }
            else if (type.IsGenericType)
            {
                var typeInfo = type.GetTypeInfo();
                bool implementsGenericIEnumerableOrIsGenericIEnumerable =
                    typeInfo.ImplementedInterfaces.Any(ti => ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
                    typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>);

                if (implementsGenericIEnumerableOrIsGenericIEnumerable)
                {
                    type = type.GetGenericArguments()[0];
                }
            }

            var keyProperties = KeyPropertiesCache(type);
            var explicitKeyProperties = ExplicitKeyPropertiesCache(type);
            if (keyProperties.Count == 0 && explicitKeyProperties.Count == 0)
                throw new ArgumentException("Entity must have at least one [Key] or [ExplicitKey] property");

            var name = GetTableName(type);
            var allKeyProperties = keyProperties.Concat(explicitKeyProperties).ToList();

            var sb = new StringBuilder();
            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                sb.AppendFormat("delete from {0} ", name);
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
                sb.Append(whereSql);
            }
            else
            {
                sb.AppendFormat("DELETE FROM {0} WHERE ", name);

                var adapter = GetFormatter(connection);

                for (var i = 0; i < allKeyProperties.Count; i++)
                {
                    var property = allKeyProperties[i];
                    adapter.AppendColumnNameEqualsValue(sb, property.Name);
                    if (i < allKeyProperties.Count - 1)
                        sb.Append(" AND ");
                }
            }

            var deleted = await connection.ExecuteAsync(sb.ToString(), entityToDelete, transaction, commandTimeout).ConfigureAwait(false);
            return deleted > 0;
        }


        /// <summary>
        /// 按照Id删除
        /// </summary>
        /// <typeparam name="T">Type of entity</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="id">id</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <returns>true if deleted, false if not found</returns>
        public static bool Delete<T>(this IDbConnection connection, long id, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            var type = typeof(T);

            if (type.IsArray)
            {
                type = type.GetElementType();
            }
            else if (type.IsGenericType)
            {
                var typeInfo = type.GetTypeInfo();
                bool implementsGenericIEnumerableOrIsGenericIEnumerable =
                    typeInfo.ImplementedInterfaces.Any(ti => ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
                    typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>);

                if (implementsGenericIEnumerableOrIsGenericIEnumerable)
                {
                    type = type.GetGenericArguments()[0];
                }
            }

            var keyProperties = KeyPropertiesCache(type).ToList();  //added ToList() due to issue #418, must work on a list copy
            var explicitKeyProperties = ExplicitKeyPropertiesCache(type);
            if (keyProperties.Count == 0 && explicitKeyProperties.Count == 0)
                throw new ArgumentException("Entity must have at least one [Key] or [ExplicitKey] property");

            var name = GetTableName(type);

            var sb = new StringBuilder();
            var key = GetSingleKey<T>(nameof(Delete));
            sb.AppendFormat("delete from {0} where {1}= {2} ", name, key.Name, id);

            var deleted = connection.Execute(sb.ToString(), null, transaction, commandTimeout);
            return deleted > 0;
        }

        /// <summary>
        /// 异步按照id删除
        /// </summary>
        /// <typeparam name="T">Type of entity</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="id">id</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <returns>true if deleted, false if not found</returns>
        public static async Task<bool> DeleteAsync<T>(this IDbConnection connection, long id, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {

            var type = typeof(T);

            if (type.IsArray)
            {
                type = type.GetElementType();
            }
            else if (type.IsGenericType)
            {
                var typeInfo = type.GetTypeInfo();
                bool implementsGenericIEnumerableOrIsGenericIEnumerable =
                    typeInfo.ImplementedInterfaces.Any(ti => ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
                    typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>);

                if (implementsGenericIEnumerableOrIsGenericIEnumerable)
                {
                    type = type.GetGenericArguments()[0];
                }
            }

            var keyProperties = KeyPropertiesCache(type);
            var explicitKeyProperties = ExplicitKeyPropertiesCache(type);
            if (keyProperties.Count == 0 && explicitKeyProperties.Count == 0)
                throw new ArgumentException("Entity must have at least one [Key] or [ExplicitKey] property");

            var name = GetTableName(type);

            var sb = new StringBuilder();
            var key = GetSingleKey<T>(nameof(Delete));
            sb.AppendFormat("delete from {0} where {1}= {2} ", name, key.Name, id);

            var deleted = await connection.ExecuteAsync(sb.ToString(), null, transaction, commandTimeout).ConfigureAwait(false);
            return deleted > 0;
        }

        #endregion

        #region 查询

        /// <summary>
        /// 根据条件获取一条记录
        /// Returns a single entity by a single id from table "Ts".  
        /// Id must be marked with [Key] attribute.
        /// Entities created from interfaces are tracked/intercepted for changes and used by the Update() extension
        /// for optimal performance. 
        /// </summary>
        /// <typeparam name="T">Interface or type to create and populate</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="whereSql">sql条件语句</param>
        /// <param name="dicParms">字段参数化，例如dicParms.Add("@id", 12);</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <returns>Entity of T</returns>
        public static T Get<T>(this IDbConnection connection, string whereSql, Dictionary<string, object> dicParms, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            var type = typeof(T);
            //去掉缓存的Sql语句
            var name = GetTableName(type);

            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
            }
            //sql = $"select * from {name} where {key.Name} = @id";
            var sql = $"select * from {name} " + whereSql;
            //自定义参数
            var dynParms = new DynamicParameters();
            dynParms.AddDynamicParams(dicParms);
            /*dynParms.Add("@id", id);*/

            T obj;

            if (type.IsInterface)
            {
                var res = connection.Query(sql, dynParms).FirstOrDefault() as IDictionary<string, object>;

                if (res == null)
                    return null;

                obj = ProxyGenerator.GetInterfaceProxy<T>();

                foreach (var property in TypePropertiesCache(type))
                {
                    var val = res[property.Name];
                    if (val == null) continue;
                    if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        var genericType = Nullable.GetUnderlyingType(property.PropertyType);
                        if (genericType != null) property.SetValue(obj, Convert.ChangeType(val, genericType), null);
                    }
                    else
                    {
                        property.SetValue(obj, Convert.ChangeType(val, property.PropertyType), null);
                    }
                }

                ((IProxy)obj).IsDirty = false;   //reset change tracking and return
            }
            else
            {
                obj = connection.Query<T>(sql, dynParms, transaction, commandTimeout: commandTimeout).FirstOrDefault();
            }
            return obj;
        }

        /// <summary>
        /// Returns a single entity by a single id from table "Ts" asynchronously using .NET 4.5 Task. T must be of interface type. 
        /// Id must be marked with [Key] attribute.
        /// Created entity is tracked/intercepted for changes and used by the Update() extension. 
        /// </summary>
        /// <typeparam name="T">Interface type to create and populate</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="id">Id of the entity to get, must be marked with [Key] attribute</param>
        /// <param name="dicParms">参数化字段</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <param name="whereSql">条件Sql语句</param>
        /// <returns>Entity of T</returns>
        public static async Task<T> GetAsync<T>(this IDbConnection connection, string whereSql, Dictionary<string, object> dicParms, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            var type = typeof(T);
            //去掉缓存的sql语句
            var name = GetTableName(type);
            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
            }
            //sql = $"SELECT * FROM {name} WHERE {key.Name} = @id";
            var sql = $"SELECT * FROM {name} " + whereSql;


            var dynParms = new DynamicParameters();
            dynParms.AddDynamicParams(dicParms);
            //dynParms.Add("@id", id);

            if (!type.IsInterface)
                return (await connection.QueryAsync<T>(sql, dynParms, transaction, commandTimeout).ConfigureAwait(false)).FirstOrDefault();

            var res = (await connection.QueryAsync<dynamic>(sql, dynParms).ConfigureAwait(false)).FirstOrDefault() as IDictionary<string, object>;

            if (res == null)
                return null;

            var obj = ProxyGenerator.GetInterfaceProxy<T>();

            foreach (var property in TypePropertiesCache(type))
            {
                var val = res[property.Name];
                if (val == null) continue;
                if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var genericType = Nullable.GetUnderlyingType(property.PropertyType);
                    if (genericType != null) property.SetValue(obj, Convert.ChangeType(val, genericType), null);
                }
                else
                {
                    property.SetValue(obj, Convert.ChangeType(val, property.PropertyType), null);
                }
            }

            ((IProxy)obj).IsDirty = false;   //reset change tracking and return

            return obj;
        }

        /// <summary>
        /// Returns a list of entites from table "Ts".  
        /// Id of T must be marked with [Key] attribute.
        /// Entities created from interfaces are tracked/intercepted for changes and used by the Update() extension
        /// for optimal performance. 
        /// </summary>
        /// <typeparam name="T">Interface or type to create and populate</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="dicParms">参数化字段</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <param name="whereSql">条件Sql语句</param>
        /// <param name="sortBy">排序Sql语句</param>
        /// <returns>Entity of T</returns>
        public static IEnumerable<T> GetAll<T>(this IDbConnection connection, string whereSql, Dictionary<string, object> dicParms, string sortBy = null, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            var type = typeof(T);

            //去掉缓存的sql语句
            var name = GetTableName(type);
            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
            }
            //排序
            if (!string.IsNullOrEmpty(sortBy) && !sortBy.ToLower().Contains("order ") && !sortBy.ToLower().Contains(" by"))
            {
                sortBy = " order by " + sortBy;
            }
            //sql = "select * from " + name;
            var sql = $"select * from {name} {whereSql} {(string.IsNullOrEmpty(sortBy) ? "" : sortBy) } ";

            //自定义参数
            var dynParms = new DynamicParameters();
            dynParms.AddDynamicParams(dicParms);

            //if (!type.IsInterface) return connection.Query<T>(sql, null, transaction, commandTimeout: commandTimeout);
            if (!type.IsInterface) return connection.Query<T>(sql, dynParms, transaction, commandTimeout: commandTimeout);

            //var result = connection.Query(sql);
            var result = connection.Query(sql, dynParms);
            var list = DynamicToInterfaceList<T>(result);
            return list;
        }

        /// <summary>
        /// Returns a list of entites from table "Ts".  
        /// Id of T must be marked with [Key] attribute.
        /// Entities created from interfaces are tracked/intercepted for changes and used by the Update() extension
        /// for optimal performance. 
        /// </summary>
        /// <typeparam name="T">Interface or type to create and populate</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="dicParms">参数化字段</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <param name="whereSql">条件Sql语句</param>
        /// <param name="sortBy">条件Sql语句</param>
        /// <returns>Entity of T</returns>
        public static Task<IEnumerable<T>> GetAllAsync<T>(this IDbConnection connection, string whereSql, Dictionary<string, object> dicParms, string sortBy = null, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            var type = typeof(T);

            //TODO:去掉缓存的sql语句
            GetSingleKey<T>(nameof(GetAll));
            var name = GetTableName(type);

            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
            }
            //排序
            if (!string.IsNullOrEmpty(sortBy) && !sortBy.ToLower().Contains("order ") && !sortBy.ToLower().Contains(" by"))
            {
                sortBy = " order by " + sortBy;
            }
            //sql = "SELECT * FROM " + name;
            var sql = $"SELECT * FROM {name} {whereSql} {(string.IsNullOrEmpty(sortBy) ? "" : sortBy) } ";

            //自定义参数
            var dynParms = new DynamicParameters();
            dynParms.AddDynamicParams(dicParms);
            if (!type.IsInterface)
            {
                //return connection.QueryAsync<T>(sql, null, transaction, commandTimeout);
                return connection.QueryAsync<T>(sql, dynParms, transaction, commandTimeout);
            }
            return GetAllAsyncImpl<T>(connection, transaction, commandTimeout, sql, type);
        }

        /// <summary>
        /// Returns a list of entites from table "Ts".  
        /// Id of T must be marked with [Key] attribute.
        /// Entities created from interfaces are tracked/intercepted for changes and used by the Update() extension
        /// for optimal performance. 
        /// </summary>
        /// <typeparam name="T">Interface or type to create and populate</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <param name="sortBy">排序Sql语句</param>
        /// <returns>Entity of T</returns>
        public static IEnumerable<T> GetAll<T>(this IDbConnection connection, string sortBy = null, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            var type = typeof(T);

            //去掉缓存的sql语句
            var name = GetTableName(type);
            //排序
            if (!string.IsNullOrEmpty(sortBy) && !sortBy.ToLower().Contains("order ") && !sortBy.ToLower().Contains(" by"))
            {
                sortBy = " order by " + sortBy;
            }
            //sql = "select * from " + name;
            var sql = $"select * from {name}  {(string.IsNullOrEmpty(sortBy) ? "" : sortBy) } ";

            //if (!type.IsInterface) return connection.Query<T>(sql, null, transaction, commandTimeout: commandTimeout);
            if (!type.IsInterface) return connection.Query<T>(sql, null, transaction, commandTimeout: commandTimeout);

            //var result = connection.Query(sql);
            var result = connection.Query(sql);
            var list = DynamicToInterfaceList<T>(result);
            return list;
        }

        /// <summary>
        /// Returns a list of entites from table "Ts".  
        /// Id of T must be marked with [Key] attribute.
        /// Entities created from interfaces are tracked/intercepted for changes and used by the Update() extension
        /// for optimal performance. 
        /// </summary>
        /// <typeparam name="T">Interface or type to create and populate</typeparam>
        /// <param name="connection">Open SqlConnection</param>
        /// <param name="transaction">The transaction to run under, null (the default) if none</param>
        /// <param name="commandTimeout">Number of seconds before command execution timeout</param>
        /// <param name="sortBy">条件Sql语句</param>
        /// <returns>Entity of T</returns>
        public static Task<IEnumerable<T>> GetAllAsync<T>(this IDbConnection connection, string sortBy = null, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            var type = typeof(T);

            //TODO:去掉缓存的sql语句
            GetSingleKey<T>(nameof(GetAll));
            var name = GetTableName(type);
            //排序
            if (!string.IsNullOrEmpty(sortBy) && !sortBy.ToLower().Contains("order ") && !sortBy.ToLower().Contains(" by"))
            {
                sortBy = " order by " + sortBy;
            }
            //sql = "SELECT * FROM " + name;
            var sql = $"SELECT * FROM {name}  {(string.IsNullOrEmpty(sortBy) ? "" : sortBy) } ";

            if (!type.IsInterface)
            {
                //return connection.QueryAsync<T>(sql, null, transaction, commandTimeout);
                return connection.QueryAsync<T>(sql, null, transaction, commandTimeout);
            }
            return GetAllAsyncImpl<T>(connection, transaction, commandTimeout, sql, type);
        }

        #endregion

        #region 分页
        
        /// <summary>
        /// 分页
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="pageSize">每页大小</param>
        /// <param name="pageIndex">页数，第几页</param>
        /// <param name="whereSql">条件sql语句</param>
        /// <param name="sortBy"></param>
        /// <param name="dicParms">sql参数化</param>
        /// <param name="recordCount">返回的总记录数</param>
        /// <param name="transaction">事务</param>
        /// <param name="commandTimeout">超时时间</param>
        /// <returns></returns>
        public static (IEnumerable<T> list, int recordCount) Pager<T>(this IDbConnection connection,
            int pageSize,
            int pageIndex,
            string whereSql,
            string sortBy,
            Dictionary<string, object> dicParms,
            IDbTransaction transaction = null,
            int? commandTimeout = null) where T : class
        {
            var type = typeof(T);

            #region 总记录数
            var recordSql = GetRecordSql<T>(connection, pageSize, pageIndex, whereSql, sortBy);

            //自定义参数
            var recordDynParms = new DynamicParameters();
            recordDynParms.AddDynamicParams(dicParms);
            var recordCount = connection.QuerySingle<int>(recordSql, recordDynParms, transaction, commandTimeout: commandTimeout);
            #endregion

            List<T> list;
            if (recordCount == 0)
            {
                list = new List<T>();
                return new ValueTuple<IEnumerable<T>, int>(list, recordCount);
            }

            int totalPage = (recordCount + pageSize - 1) / pageSize;
            // 超出总页数的，页数设置为最后一页
            if (pageIndex > totalPage)
            {
                pageIndex = totalPage;
            }
            #region 分页

            var pagerSqlResult = GetPagerSql<T>(connection, pageSize, pageIndex, whereSql, sortBy, dicParms,recordCount);
            //自定义参数
            var dynParms = pagerSqlResult.dynParms;
            string sql = pagerSqlResult.pagerSql;
            //if (!type.IsInterface) return connection.Query<T>(sql, null, transaction, commandTimeout: commandTimeout);
            if (!type.IsInterface)
            {
                list = connection.Query<T>(sql, dynParms, transaction, commandTimeout: commandTimeout).ToList();
            }
            else
            {
                #region IsInterface
                var result = connection.Query(sql, dynParms);
                list = DynamicToInterfaceList<T>(result);
                #endregion
            }

            #endregion
            (IEnumerable<T> list, int recordCount) tupleResult = new ValueTuple<IEnumerable<T>, int>(list, recordCount);
            return tupleResult;
        }


        /// <summary>
        /// 异步分页
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="pageSize">每页大小</param>
        /// <param name="pageIndex">页数，第几页</param>
        /// <param name="whereSql">条件sql语句</param>
        /// <param name="sortBy">如按创建时间倒序 CreateTime  Desc</param>
        /// <param name="dicParms">sql参数化</param>
        /// <param name="recordCount">返回的总记录数</param>
        /// <param name="transaction">事务</param>
        /// <param name="commandTimeout">超时时间</param>
        /// <returns></returns>
        public static async Task<(IEnumerable<T> list, int recordCount)> PagerAsync<T>(this IDbConnection connection,
            int pageSize,
            int pageIndex,
            string whereSql,
            string sortBy,
            Dictionary<string, object> dicParms,
            IDbTransaction transaction = null,
            int? commandTimeout = null) where T : class
        {
            var type = typeof(T);

            #region 总记录数
            var recordSql = GetRecordSql<T>(connection, pageSize, pageIndex, whereSql, sortBy);

            //自定义参数
            var recordDynParms = new DynamicParameters();
            recordDynParms.AddDynamicParams(dicParms);
            var recordCount = await connection.QuerySingleAsync<int>(recordSql, recordDynParms, transaction, commandTimeout: commandTimeout);
            #endregion

            List<T> list;
            if (recordCount == 0)
            {
                list = new List<T>();
                return new ValueTuple<IEnumerable<T>, int>(list, recordCount);
            }

            int totalPage = (recordCount + pageSize - 1) / pageSize;
            // 超出总页数的，页数设置为最后一页
            if (pageIndex > totalPage)
            {
                pageIndex = totalPage;
            }

            #region 分页
            var pagerSqlResult = GetPagerSql<T>(connection, pageSize, pageIndex, whereSql, sortBy, dicParms,recordCount);

            //自定义参数
            var dynParms = pagerSqlResult.dynParms;
            string sql = pagerSqlResult.pagerSql;
            //if (!type.IsInterface) return connection.Query<T>(sql, null, transaction, commandTimeout: commandTimeout);
            if (!type.IsInterface)
            {
                list = (await connection.QueryAsync<T>(sql, dynParms, transaction, commandTimeout: commandTimeout)).ToList();
            }
            else
            {
                #region IsInterface
                var result = await connection.QueryAsync(sql, dynParms);
                list = DynamicToInterfaceList<T>(result);
                #endregion
            }

            #endregion
            (IEnumerable<T> list, int recordCount) tupleResult = new ValueTuple<IEnumerable<T>, int>(list, recordCount);
            return tupleResult;
        }
        #endregion

        #region private method

        private static (string recordSql, string pagerSql, DynamicParameters dynParms) GetPagerSql<T>(IDbConnection connection, 
            int pageSize,
            int pageIndex,
            string whereSql,
            string sortBy,
            Dictionary<string, object> dicParms
            ) where T : class
        {
            (string recordSql, string pagerSql,DynamicParameters dynParms) result = new ValueTuple<string, string,DynamicParameters>();
            var type = typeof(T);

            //去掉缓存的sql语句
            var name = GetTableName(type);
            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
            }

            //自定义参数
            var dynParms = new DynamicParameters();
            dynParms.AddDynamicParams(dicParms);

            #region 总记录数
            var recordSql = $"select count(1) recordCount  from {name} {whereSql}";
            #endregion

            #region 分页
            int startLimit = 1;
            if (pageIndex < 0)
            {
                pageIndex = 0;
            }
            if (pageSize > 0)
            {
                startLimit = (pageIndex - 1) * pageSize + 1;
            }
            var selectQuery = $"select * from {name} {whereSql}";
            //默认id来排序
            if (string.IsNullOrEmpty(sortBy))
            {
                var key = GetSingleKey<T>(nameof(Pager));
                sortBy = key.Name;
            }
            //排序
            if (!string.IsNullOrEmpty(sortBy) && !sortBy.ToLower().Contains("order ") && !sortBy.ToLower().Contains(" by"))
            {
                sortBy = " order by " + sortBy;
            }
            //数据库类型名称
            var databaseTypeName = GetDatabaseType?.Invoke(connection).ToLower()
                                   ?? connection.GetType().Name.ToLower();
            var sql = "";
            if (databaseTypeName == "sqlconnection")
            {
                //不再支持Sql Server 2008及以下版本的ROW_NUMBER分页
                /*
                sql =
                $@"SELECT * FROM(
                                    SELECT ROW_NUMBER() OVER ({sortBy}) AS tempid, * FROM {"(" + selectQuery + ") t "} WHERE 1=1
                                ) AS tempTableName WHERE tempid BETWEEN {startLimit} AND {pageIndex * pageSize}";
                */

                //Sql Server 2012及以上使用offset fetch分页
                //前端页面pageIndex是从1开始
                var offsetCount = (pageIndex - 1) * pageSize;
                sql = $"{selectQuery}  {sortBy}  offset {offsetCount} rows fetch next {pageSize} rows only ";
            }
            else if (databaseTypeName == "mysqlconnection")
            {
                sql = $"select * from {name}  limit {pageIndex * pageSize} , {pageSize} {sortBy} {whereSql} ";
            }
            else if (databaseTypeName == "sqliteconnection")
            {
                sql = $"select  *  from  {name}  {whereSql}  {sortBy} limit {pageSize} OFFSET {pageIndex * pageSize} ";
            }
            else if (databaseTypeName == "sqlceconnection")
            {
                throw new Exception("暂不支持SqlCE数据库分页");
            }
            else if (databaseTypeName == "npgsqlconnection")
            {
                throw new Exception("暂不支持PostgreSQL数据库分页");
            }
            else if (databaseTypeName == "fbconnection")
            {
                throw new Exception("暂不支持Firebird数据库分页");
            }
            else
            {
                throw new Exception($"暂不支持{databaseTypeName}连接的数据库分页");
            }
            #endregion

            result.recordSql = recordSql;
            result.pagerSql = sql;
            result.dynParms = dynParms;
            return result;
        }

              
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageIndex">第几页，从1开始</param>
        /// <param name="whereSql"></param>
        /// <param name="sortBy"></param>
        /// <param name="dicParms"></param>
        /// <param name="recordCount"></param>
        /// <returns></returns>
        private static (string pagerSql, DynamicParameters dynParms) GetPagerSql<T>(IDbConnection connection, 
            int pageSize,
            int pageIndex,
            string whereSql,
            string sortBy,
            Dictionary<string, object> dicParms,
            int recordCount
            ) where T : class
        {
            (string pagerSql,DynamicParameters dynParms) result = new ValueTuple<string,DynamicParameters>();
            var type = typeof(T);

            //去掉缓存的sql语句
            var name = GetTableName(type);
            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
            }

            //自定义参数
            var dynParms = new DynamicParameters();
            dynParms.AddDynamicParams(dicParms);

            #region 分页
            int startLimit = 1;
            if (pageIndex < 0)
            {
                pageIndex = 0;
            }
            if (pageSize > 0)
            {
                startLimit = (pageIndex - 1) * pageSize + 1;
            }

            var selectQuery = $"select * from {name} {whereSql}";
            //默认id来排序
            if (string.IsNullOrEmpty(sortBy))
            {
                var key = GetSingleKey<T>(nameof(Pager));
                sortBy = key.Name;
            }
            //排序
            if (!string.IsNullOrEmpty(sortBy) && !sortBy.ToLower().Contains("order ") && !sortBy.ToLower().Contains(" by"))
            {
                sortBy = " order by " + sortBy;
            }
            //数据库类型名称
            var databaseTypeName = GetDatabaseType?.Invoke(connection).ToLower()
                                   ?? connection.GetType().Name.ToLower();
            var sql = "";
            if (databaseTypeName == "sqlconnection")
            {
                //不再支持Sql Server 2008及以下版本的ROW_NUMBER分页
                /*
                sql =
                $@"SELECT * FROM(
                                    SELECT ROW_NUMBER() OVER ({sortBy}) AS tempid, * FROM {"(" + selectQuery + ") t "} WHERE 1=1
                                ) AS tempTableName WHERE tempid BETWEEN {startLimit} AND {pageIndex * pageSize}";
                */

                //Sql Server 2012及以上使用offset fetch分页
                //前端页面pageIndex是从1开始
                var offsetCount = (pageIndex - 1) * pageSize;

                // 跳过的记录数
                var skipRecordCount = (pageIndex - 1) * pageSize;

                // 剩余记录数
                var remainingRecordCount = recordCount - skipRecordCount;

                // 取出的数量
                var tackRecordCount = pageSize;

                // 剩余记录数小于每页的数量
                if (remainingRecordCount < pageSize)
                {
                    tackRecordCount = remainingRecordCount;
                }

                sql = $"{selectQuery}  {sortBy}  offset {offsetCount} rows fetch next {tackRecordCount} rows only ";
            }
            else if (databaseTypeName == "mysqlconnection")
            {
                sql = $"select * from {name}  limit {pageIndex * pageSize} , {pageSize} {sortBy} {whereSql} ";
            }
            else if (databaseTypeName == "sqliteconnection")
            {
                sql = $"select  *  from  {name}  {whereSql}  {sortBy} limit {pageSize} OFFSET {pageIndex * pageSize} ";
            }
            else if (databaseTypeName == "sqlceconnection")
            {
                throw new Exception("暂不支持SqlCE数据库分页");
            }
            else if (databaseTypeName == "npgsqlconnection")
            {
                throw new Exception("暂不支持PostgreSQL数据库分页");
            }
            else if (databaseTypeName == "fbconnection")
            {
                throw new Exception("暂不支持Firebird数据库分页");
            }
            else
            {
                throw new Exception($"暂不支持{databaseTypeName}连接的数据库分页");
            }
            #endregion

            result.pagerSql = sql;
            result.dynParms = dynParms;
            return result;
        }

        private static string GetRecordSql<T>(IDbConnection connection, 
            int pageSize,
            int pageIndex,
            string whereSql,
            string sortBy
            ) where T : class
        {
            var type = typeof(T);

            //去掉缓存的sql语句
            var name = GetTableName(type);
            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
            }

            #region 总记录数
            var recordSql = $"select count(1) recordCount  from {name} {whereSql}";
            #endregion

            return recordSql;
        }

        
        private static  List<T> DynamicToInterfaceList<T>(IEnumerable<dynamic> dynamicList) where T : class
        {
            var type = typeof(T);
            var list = new List<T>();
            foreach (IDictionary<string, object> res in dynamicList)
            {
                var obj = ProxyGenerator.GetInterfaceProxy<T>();
                foreach (var property in TypePropertiesCache(type))
                {
                    var val = res[property.Name];
                    if (val == null) continue;
                    if (property.PropertyType.IsGenericType &&
                        property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        var genericType = Nullable.GetUnderlyingType(property.PropertyType);
                        if (genericType != null) property.SetValue(obj, Convert.ChangeType(val, genericType), null);
                    }
                    else
                    {
                        property.SetValue(obj, Convert.ChangeType(val, property.PropertyType), null);
                    }
                }

                ((IProxy)obj).IsDirty = false; //reset change tracking and return
                list.Add(obj);
            }
            return list;
        }
        #endregion

        #region 批量操作

        
        public static bool BulkInsert2<T>(this DbConnection connection, List<T> list) where T : class
        {
            var dt = ConvertEntityListToDataTable(list,connection);
            using var bcp = BulkCopy.TryCreate(connection);
            try
            {
                bcp.DestinationTableName = dt.TableName;
                bcp.WriteToServer(dt);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return true;
        }
        

        public static bool BulkInsert<T>(this DbConnection connection, List<T> list, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            //数据库类型名称
            var databaseTypeName = GetDatabaseType?.Invoke(connection).ToLower()
                                   ?? connection.GetType().Name.ToLower();
            if (databaseTypeName == "sqlconnection")
            {
                return BulkInsertSqlServer(connection, list, transaction, commandTimeout);
            }
            else if (databaseTypeName == "mysqlconnection")
            {
                throw new Exception("暂不支持MySql数据库");
            }
            else if (databaseTypeName == "sqliteconnection")
            {
            }
            else if (databaseTypeName == "sqlceconnection")
            {
                throw new Exception("暂不支持SqlCE数据库");
            }
            else if (databaseTypeName == "npgsqlconnection")
            {
                throw new Exception("暂不支持PostgreSQL数据库");
            }
            else if (databaseTypeName == "fbconnection")
            {
                throw new Exception("暂不支持Firebird数据库");
            }
            else
            {
                throw new Exception($"暂不支持{databaseTypeName}连接的数据库");
            }

            return false;
        }
        public static async Task<bool> BulkInsertAsync<T>(this DbConnection connection, List<T> list, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            //数据库类型名称
            var databaseTypeName = GetDatabaseType?.Invoke(connection).ToLower()
                                   ?? connection.GetType().Name.ToLower();
            if (databaseTypeName == "sqlconnection")
            {
                return await BulkInsertSqlServerAsync(connection, list, transaction, commandTimeout);
            }
            else if (databaseTypeName == "mysqlconnection")
            {
                throw new Exception("暂不支持MySql数据库");
            }
            else if (databaseTypeName == "sqliteconnection")
            {
            }
            else if (databaseTypeName == "sqlceconnection")
            {
                throw new Exception("暂不支持SqlCE数据库");
            }
            else if (databaseTypeName == "npgsqlconnection")
            {
                throw new Exception("暂不支持PostgreSQL数据库");
            }
            else if (databaseTypeName == "fbconnection")
            {
                throw new Exception("暂不支持Firebird数据库");
            }
            else
            {
                throw new Exception($"暂不支持{databaseTypeName}连接的数据库");
            }

            return false;
        }


        public static async Task<bool> BulkUpdateAsync<T>(
            this DbConnection connection, 
            List<T> list, string[] updateProps, 
            IDbTransaction transaction = null, 
            int? commandTimeout = null) where T : class
        {
            #region 数据库判断
            //数据库类型名称
            var databaseTypeName = GetDatabaseType?.Invoke(connection).ToLower()
                                   ?? connection.GetType().Name.ToLower();

            if (databaseTypeName == "sqlconnection")
            {

            }
            else if (databaseTypeName == "mysqlconnection")
            {
                throw new Exception("暂不支持MySql数据库");
            }
            else if (databaseTypeName == "sqliteconnection")
            {
            }
            else if (databaseTypeName == "sqlceconnection")
            {
                throw new Exception("暂不支持SqlCE数据库");
            }
            else if (databaseTypeName == "npgsqlconnection")
            {
                throw new Exception("暂不支持PostgreSQL数据库");
            }
            else if (databaseTypeName == "fbconnection")
            {
                throw new Exception("暂不支持Firebird数据库");
            }
            else
            {
                throw new Exception($"暂不支持{databaseTypeName}连接的数据库");
            } 
            #endregion

            var type = typeof(T);

            // 自动主键
            var keyProperties = KeyPropertiesCache(type).ToList();
            // 非自增主键
            var explicitKeyProperties = ExplicitKeyPropertiesCache(type);
            if (keyProperties.Count == 0 && explicitKeyProperties.Count == 0)
                throw new ArgumentException("Entity must have at least one [Key] or [ExplicitKey] property");

            keyProperties.AddRange(explicitKeyProperties);

            var tableName = GetTableName(type);

            // 创建临时表 (注意：临时表需要在同一个连接内才能访问)

            var tempTableName = @"#TmpTable" + tableName;
            var dt = ConvertEntityListToDataTable(list, connection, transaction,commandTimeout);
            dt.TableName = tempTableName;

            var createTmpTableSql = $"select  top 0  * into {tempTableName} from {tableName} where 1!=1 ; ";
            await connection.ExecuteAsync(createTmpTableSql,null, transaction,commandTimeout);

            #region // 批量插入数据到临时表

            SqlBulkCopyOptions options = SqlBulkCopyOptions.Default;
            SqlBulkCopy sqlBulkCopy;
            if (transaction == null)
            {
                sqlBulkCopy = new SqlBulkCopy((SqlConnection)connection);
            }
            else
            {
                sqlBulkCopy = new SqlBulkCopy((SqlConnection)connection, options, (SqlTransaction)transaction);
            }

            //设置数据库表名
            sqlBulkCopy.DestinationTableName = dt.TableName;
            if (commandTimeout.HasValue && commandTimeout.Value > 0)
                sqlBulkCopy.BulkCopyTimeout = commandTimeout.Value;

            await sqlBulkCopy.WriteToServerAsync(dt);

            if (transaction == null)
            {
                sqlBulkCopy.Close();
            }
            #endregion

            // 通过Id关联临时表更新数据
            var updatePropsSql = "";
            foreach(var item in updateProps)
            {
                updatePropsSql += $" {item}= tmp.{item} ,";
            }
            updatePropsSql = updatePropsSql.TrimEnd(',');

            var relationPropSql = "";

            for (var i = 0; i < keyProperties.Count; i++)
            {
                var property = keyProperties[i];
                if (i > 0) relationPropSql += " and ";
                relationPropSql += $" tb.{property.Name}=tmp.{property.Name} ";
            }

            var updateSql = $" update {tableName} set {updatePropsSql} from {tableName} tb inner join {tempTableName} tmp on {relationPropSql} ; ";

            await connection.ExecuteAsync(updateSql,null, transaction,commandTimeout);

            // 删除临时表
            var deleteTemTableSql = $" drop table {tempTableName} ; ";
            await connection.ExecuteAsync(deleteTemTableSql,null, transaction,commandTimeout);
            return true;
        }

        public static async Task<IEnumerable<T>> BulkGetAsync<T>(
            this IDbConnection connection,
            string whereSql, 
            Dictionary<string, object> dicParms,
            KeyValuePair<string, List<object>> queryPropList,
            string sortBy = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null) 
            where T : class
        {
            #region 数据库判断
            //数据库类型名称
            var databaseTypeName = GetDatabaseType?.Invoke(connection).ToLower()
                                   ?? connection.GetType().Name.ToLower();

            if (databaseTypeName == "sqlconnection")
            {

            }
            else if (databaseTypeName == "mysqlconnection")
            {
                throw new Exception("暂不支持MySql数据库");
            }
            else if (databaseTypeName == "sqliteconnection")
            {
            }
            else if (databaseTypeName == "sqlceconnection")
            {
                throw new Exception("暂不支持SqlCE数据库");
            }
            else if (databaseTypeName == "npgsqlconnection")
            {
                throw new Exception("暂不支持PostgreSQL数据库");
            }
            else if (databaseTypeName == "fbconnection")
            {
                throw new Exception("暂不支持Firebird数据库");
            }
            else
            {
                throw new Exception($"暂不支持{databaseTypeName}连接的数据库");
            } 
            #endregion

            var queryColumnName = queryPropList.Key;
            var queryColumnTempName = queryColumnName + "_TempColumn";
            var type = typeof(T);

            var tableName = GetTableName(type);

            // 创建临时表 (注意：临时表需要在同一个连接内才能访问)

            var tempTableName = @"#TmpTable" + tableName+"_"+queryPropList.Key;
            var dt = ConvertPropListToDataTable(queryPropList);
            dt.TableName = tempTableName;

            var createTmpTableSql = $"select  top 0  {queryColumnName} {queryColumnTempName} into {tempTableName} from {tableName} where 1!=1 ; ";
            await connection.ExecuteAsync(createTmpTableSql,null, transaction,commandTimeout);

            #region // 批量插入数据到临时表

            SqlBulkCopyOptions options = SqlBulkCopyOptions.Default;
            SqlBulkCopy sqlBulkCopy;
            if (transaction == null)
            {
                sqlBulkCopy = new SqlBulkCopy((SqlConnection)connection);
            }
            else
            {
                sqlBulkCopy = new SqlBulkCopy((SqlConnection)connection, options, (SqlTransaction)transaction);
            }

            //设置数据库表名
            sqlBulkCopy.DestinationTableName = dt.TableName;
            if (commandTimeout.HasValue && commandTimeout.Value > 0)
                sqlBulkCopy.BulkCopyTimeout = commandTimeout.Value;

            await sqlBulkCopy.WriteToServerAsync(dt);

            if (transaction == null)
            {
                sqlBulkCopy.Close();
            }
            #endregion
            
            //TODO:去掉缓存的sql语句
            GetSingleKey<T>(nameof(GetAll));
            //var name = GetTableName(type);

            //自定义条件
            if (!string.IsNullOrEmpty(whereSql))
            {
                if (!whereSql.ToLower().Contains("where"))
                {
                    whereSql = " where " + whereSql;
                }
            }
            //排序
            if (!string.IsNullOrEmpty(sortBy) && !sortBy.ToLower().Contains("order ") && !sortBy.ToLower().Contains(" by"))
            {
                sortBy = " order by " + sortBy;
            }
            //sql = "SELECT * FROM " + name;
            // var sql = $"SELECT * FROM {tableName} {whereSql} {(string.IsNullOrEmpty(sortBy) ? "" : sortBy) } ";
            var sql = $"SELECT a.* FROM {tableName} a inner join {tempTableName} temp on a.{queryColumnName} =temp.{queryColumnTempName} {whereSql} {(string.IsNullOrEmpty(sortBy) ? "" : sortBy) } ";

            //自定义参数
            var dynParms = new DynamicParameters();
            dynParms.AddDynamicParams(dicParms);
            IEnumerable<T> list;
            if (!type.IsInterface)
            {
                //return connection.QueryAsync<T>(sql, null, transaction, commandTimeout);
                list = await connection.QueryAsync<T>(sql, dynParms, transaction, commandTimeout);
            }
            else
            {
                list = await GetAllAsyncImpl<T>(connection, transaction, commandTimeout, sql, type);
            }

            // 删除临时表
            var deleteTemTableSql = $" drop table {tempTableName} ; ";
            await connection.ExecuteAsync(deleteTemTableSql, null, transaction,commandTimeout);
            return list;
        }
        
        #region private method
        private static async Task<bool> BulkInsertSqlServerAsync<T>(
            DbConnection connection, 
            List<T> list, IDbTransaction transaction = null,
            int? commandTimeout = null) 
            where T : class
        {
            var dt = ConvertEntityListToDataTable(list,connection,transaction, commandTimeout);

            SqlBulkCopyOptions options = SqlBulkCopyOptions.Default;
            SqlBulkCopy sqlBulkCopy;
            if (transaction == null)
            {
                sqlBulkCopy = new SqlBulkCopy((SqlConnection)connection);
            }
            else
            {
                sqlBulkCopy = new SqlBulkCopy((SqlConnection)connection, options, (SqlTransaction)transaction);
            }

            //设置数据库表名
            sqlBulkCopy.DestinationTableName = dt.TableName;
            //copy.BatchSize = 0;
            if (commandTimeout.HasValue && commandTimeout.Value > 0)
                sqlBulkCopy.BulkCopyTimeout = commandTimeout.Value;

            await sqlBulkCopy.WriteToServerAsync(dt);

            if (transaction == null)
            {
                sqlBulkCopy.Close();
            }
            return true;
        }


        private static bool BulkInsertSqlServer<T>(
            DbConnection connection, 
            List<T> list, IDbTransaction transaction = null,
            int? commandTimeout = null) 
            where T : class
        {
            var dt = ConvertEntityListToDataTable(list,connection,transaction,commandTimeout);

            SqlBulkCopyOptions options = SqlBulkCopyOptions.Default;
            SqlBulkCopy sqlBulkCopy;
            if (transaction == null)
            {
                sqlBulkCopy = new SqlBulkCopy((SqlConnection)connection);
            }
            else
            {
                sqlBulkCopy = new SqlBulkCopy((SqlConnection)connection, options, (SqlTransaction)transaction);
            }

            //设置数据库表名
            sqlBulkCopy.DestinationTableName = dt.TableName;
            //copy.BatchSize = 0;
            if (commandTimeout.HasValue && commandTimeout.Value > 0)
                sqlBulkCopy.BulkCopyTimeout = commandTimeout.Value;

            sqlBulkCopy.WriteToServer(dt);

            if (transaction == null)
            {
                sqlBulkCopy.Close();
            }
            return true;
        }

        private static DataTable ConvertEntityListToDataTable<T>(List<T> list,DbConnection connection, IDbTransaction transaction = null, int? commandTimeout = null) where T : class
        {
            var dt = new DataTable();

            #region 构建DataTable

            var type = typeof(T);

            var name = GetTableName(type);
            var allProperties = TypePropertiesCache(type);
            var keyProperties = KeyPropertiesCache(type);
            //var explicitKeyProperties = ExplicitKeyPropertiesCache(type);
            var computedProperties = ComputedPropertiesCache(type);
            var allPropertiesExceptKeyAndComputed = allProperties.Except(keyProperties.Union(computedProperties)).ToList();
            
            /*
            for (var i = 0; i < allPropertiesExceptKeyAndComputed.Count; i++)
            {
                var property = allPropertiesExceptKeyAndComputed[i];
                dt.Columns.Add(property.Name);
            }

            //自增字段
            for (var i = 0; i < keyProperties.Count; i++)
            {
                var property = keyProperties[i];
                dt.Columns.Add(property.Name);
            }
            */

            #region 获取表字段顺序
            var dataReader = connection.ExecuteReader($"select top 0 * from {name}",null,transaction,commandTimeout);
            dt.Load(dataReader);
            #endregion

            foreach (var entityToInsert in list)
            {
                var row = dt.NewRow();
                //自增字段设置为0
                for (var i = 0; i < keyProperties.Count; i++)
                {
                    var property = keyProperties[i];
                    if (property.PropertyType==typeof(int))
                        row[property.Name] = 0;
                    else if (property.PropertyType==typeof(long))
                        row[property.Name] = 0;
                    else
                        row[property.Name] = property.GetValue(entityToInsert, null) ?? DBNull.Value;
                }
                
                for (var i = 0; i < allPropertiesExceptKeyAndComputed.Count; i++)
                {
                    var property = allPropertiesExceptKeyAndComputed[i];
                    row[property.Name] = property.GetValue(entityToInsert, null) ?? DBNull.Value;
                }
                dt.Rows.Add(row);
            }
            #endregion

            dt.TableName = name;
            return dt;
        } 
        
        private static DataTable ConvertPropListToDataTable(KeyValuePair<string,List<object>> kvPair)
        {
            var dt = new DataTable();
            #region 构建DataTable
            //var propType = kvPair.Value[0].GetType();
            dt.Columns.Add(kvPair.Key);
            var dataList = kvPair.Value.Distinct();
            foreach (var item in dataList)
            {
                var row = dt.NewRow();
                row[0] = item;
                dt.Rows.Add(row);
            }
            #endregion

            return dt;
        } 
        #endregion

        #endregion
    }
}