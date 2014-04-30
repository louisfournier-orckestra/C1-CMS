﻿namespace Composite.Plugins.Data.DataProviders.MSSqlServerDataProvider.Sql
{
	internal interface ISqlTableInformationStore
	{
        ISqlTableInformation GetTableInformation(string connectionString, string tableName);
        void OnFlush();
	    void ClearCache(string tableName);
	}
}
