using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Linq;
using System.Data.SqlTypes;
using System.Reflection;
using Composite.Core.Extensions;
using Composite.Core.Logging;
using Composite.Core.Sql;
using Composite.Core.Threading;
using Composite.Data;
using Composite.Data.Plugins.DataProvider;
using Composite.Plugins.Data.DataProviders.MSSqlServerDataProvider.CodeGeneration;
using Composite.Plugins.Data.DataProviders.MSSqlServerDataProvider.Foundation;


namespace Composite.Plugins.Data.DataProviders.MSSqlServerDataProvider
{
    internal sealed class SqlDataTypeStoresContainer
    {
        private static readonly ConcurrentDictionary<Type, List<PropertyInfo>> _dateTimeProperties = new ConcurrentDictionary<Type, List<PropertyInfo>>();
        private static readonly object[] EmptyObjectsArray = new object[0];


        private readonly Dictionary<Type, SqlDataTypeStore> _sqlDataTypeStores = new Dictionary<Type, SqlDataTypeStore>();
        private readonly List<Type> _supportedInterface = new List<Type>();
        private readonly List<Type> _knownInterfaces = new List<Type>();
        private readonly List<Type> _generatedInterface = new List<Type>();


        private readonly string _providerName;
        private readonly SqlLoggingContext _sqlLoggingContext;
        
        
        internal SqlDataTypeStoresContainer(string providerName, string connectionString, SqlLoggingContext sqlLoggingContext = null)
        {
            _providerName = providerName;
            ConnectionString = connectionString;
            _sqlLoggingContext = sqlLoggingContext;
        }



        public string ConnectionString { get; internal set; }



        /// <summary>
        /// All working data types 
        /// </summary>
        public IEnumerable<Type> SupportedInterface { get { return _supportedInterface; } }



        /// <summary>
        /// All data types, including non working due to config error or something else
        /// </summary>
        public IEnumerable<Type> KnownInterfaces { get { return _knownInterfaces; } }



        /// <summary>
        /// All working generated data types
        /// </summary>
        public IEnumerable<Type> GeneratedInterfaces { get { return _generatedInterface; } }



        internal Type DataContextClass { get; set; }



        public SqlDataTypeStore GetDataTypeStore(Type interfaceType)
        {
            if (!_sqlDataTypeStores.ContainsKey(interfaceType)) throw new InvalidOperationException();
            
            return _sqlDataTypeStores[interfaceType];
        }



        /// <summary>
        /// This method adds the support of the given data interface type to the xml data provider.
        /// </summary>
        /// <param name="interfaceType"></param>
        /// <param name="sqlDataTypeStore"></param>
        internal void AddSupportedDataTypeStore(Type interfaceType, SqlDataTypeStore sqlDataTypeStore)
        {
            _sqlDataTypeStores.Add(interfaceType, sqlDataTypeStore);

            _supportedInterface.Add(interfaceType);
            AddKnownInterface(interfaceType);

            if (sqlDataTypeStore.IsGeneretedDataType)
            {
                _generatedInterface.Add(interfaceType);
            }
        }



        internal void AddKnownInterface(Type interfaceType)
        {
            _knownInterfaces.Add(interfaceType);
        }


        internal void RemoveKnownInterface(Type interfaceType)
        {
            _knownInterfaces.Remove(interfaceType);
        }


        #region CRUD methos


        public List<T> AddNew<T>(IEnumerable<T> dataset, DataProviderContext dataProviderContext)
            where T : class, IData
        {
            SqlDataTypeStore sqlDataTypeStore = TryGetsqlDataTypeStore(typeof(T));
            if (sqlDataTypeStore == null) throw new InvalidOperationException(string.Format("The interface '{0}' has not been configures", typeof(T).FullName));

            var resultDataset = new List<T>();

            using (var dataContext = CreateDataContext())
            {
                foreach (IData data in dataset)
                {
                    Verify.ArgumentCondition(data != null, "dataset", "Data set may not contain nulls");

                    IData newData = sqlDataTypeStore.AddNew(data, dataProviderContext, dataContext);

                    (newData as IEntity).Commit();

                    CheckConstraints(newData);

                    resultDataset.Add((T)newData);
                }

                SubmitChanges(dataContext);
            }

            return resultDataset;
        }



        public void Update(IEnumerable<IData> dataset)
        {
            using (DataContext dataContext = CreateDataContext())
            {
                foreach (IData data in dataset)
                {
                    Verify.ArgumentCondition(data != null, "dataset", "The data set shouldn't contain any null values.");

                    // TODO: Check if it's necessury to make an optimization here
                    ITable table = GetTable(dataContext, data);
                    table.Attach(data);

                    IEntity entity = (IEntity)data;
                    entity.Commit();
                }

                SubmitChanges(dataContext);
            }
        }



        public void Delete(IEnumerable<DataSourceId> dataSourceIds, DataProviderContext dataProivderContext)
        {
            DataContext dataContext = null;
            try
            {
                foreach (DataSourceId dataSourceId in dataSourceIds)
                {
                    if (dataSourceId == null) throw new ArgumentException("dataSourceIds contains nulls");

                    using (new DataScope(dataSourceId.DataScopeIdentifier, dataSourceId.LocaleScope))
                    {
                        SqlDataTypeStore sqlDataTypeStore = TryGetsqlDataTypeStore(dataSourceId.InterfaceType);
                        if (sqlDataTypeStore == null) throw new InvalidOperationException(string.Format("The interface '{0}' has not been configures", dataSourceId.InterfaceType.FullName));

                        IData data = sqlDataTypeStore.GetDataByDataId(dataSourceId.DataId, dataProivderContext);

                        Verify.That(data != null, "Row has already been deleted");

                        if (dataContext == null) dataContext = CreateDataContext();

                        sqlDataTypeStore.RemoveData(data, dataContext);
                    }
                }

                if (dataContext != null)
                {
                    SubmitChanges(dataContext);
                }
            }
            finally
            {
                if (dataContext != null)
                {
                    dataContext.Dispose();
                }
            }
        }


        #endregion



        private SqlDataTypeStore TryGetsqlDataTypeStore(Type interfaceType)
        {
            Verify.ArgumentNotNull(interfaceType, "interfaceType");

            SqlDataTypeStore result;
            return _sqlDataTypeStores.TryGetValue(interfaceType, out result) ? result : null;
        }



        private static ITable GetTable(DataContext dataContext, Object entity)
        {
            Verify.ArgumentNotNull(dataContext, "dataContext");
            Verify.ArgumentNotNull(entity, "entity");

            Type entityType = entity.GetType();

            ITable table = dataContext.GetTable(entityType);
            Verify.IsNotNull(table, "Failed to find a table, related to '{0}' type".FormatWith(entityType.FullName));
            return table;
        }



        private static void CheckConstraints(IData data)
        {
            // DateTime.MinValue is not supported by SQL, since it has a different minimal value for a date
            if (data is IChangeHistory)
            {
                var changeHistory = (IChangeHistory)data;
                if (changeHistory.ChangeDate == DateTime.MinValue)
                {
                    changeHistory.ChangeDate = DateTime.Now;
                }
            }

            foreach(PropertyInfo dateTimeProperty in GetDateTimeProperties(data.DataSourceId.InterfaceType))
            {
                object value = dateTimeProperty.GetValue(data, EmptyObjectsArray);
                if(value == null) continue;

                DateTime dateTime = (DateTime) value;
                if(dateTime == DateTime.MinValue)
                {
                    dateTimeProperty.SetValue(data, SqlDateTime.MinValue.Value, EmptyObjectsArray);
                }
            }
        }



        private static IEnumerable<PropertyInfo> GetDateTimeProperties(Type interfaceType)
        {
            return 
                _dateTimeProperties.GetOrAdd(interfaceType, type =>
                {
                    List<PropertyInfo> result = new List<PropertyInfo>();

                    foreach (PropertyInfo property in interfaceType.GetProperties())
                    {
                        if ((property.PropertyType != typeof(DateTime) && property.PropertyType != typeof(DateTime?))
                            || property.GetSetMethod() == null)
                        {
                            continue;
                        }

                        result.Add(property);
                    }

                    foreach(Type baseInterface in interfaceType.GetInterfaces())
                    {
                        if (baseInterface == typeof(IChangeHistory)) continue;
                        

                        result.AddRange(GetDateTimeProperties(baseInterface));
                    }

                    return result;
                });
        }



        /// <summary>
        /// Gets an instance of a DataContext.
        /// </summary>
        /// <returns></returns>
        internal DataContext GetDataContext()
        {
            string threadDataKey = "SqlDataContext" + _providerName;

            var threadData = ThreadDataManager.GetCurrentNotNull();
            if (threadData.HasValue(threadDataKey))
            {
                DataContext result = Verify.ResultNotNull(threadData[threadDataKey] as DataContext);

                // In a result of a flush, data context type can be changed
               if(result.GetType().GUID == DataContextClass.GUID)
               {
                   return result;
               }
            }

            DataContext dataContext = CreateDataContext();
            dataContext.ObjectTrackingEnabled = false;

            threadData.OnDispose += dataContext.Dispose;
        
            threadData.SetValue(threadDataKey, dataContext);

            if (_sqlLoggingContext.Enabled)
            {
                dataContext.Log = new SqlLoggerTextWriter(_sqlLoggingContext);
            }
            
            return dataContext;
        }



        private DataContext CreateDataContext()
        {
            IDbConnection connection = SqlConnectionManager.GetConnection(ConnectionString);

            DataContext dataContext = (DataContext)Activator.CreateInstance(DataContextClass, connection);

            if (_sqlLoggingContext.Enabled)
            {
                dataContext.Log = new SqlLoggerTextWriter(_sqlLoggingContext);
            }

            return dataContext;
        }



        internal static void SubmitChanges(DataContext dataContext)
        {
            try
            {
                dataContext.SubmitChanges();
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning("SqlDataProviderCodeGeneratorResult", ex);

                throw;
            }
        }        
    }
}
