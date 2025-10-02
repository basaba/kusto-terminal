using System;
using System.Collections.Generic;
using System.Linq;
using Kusto.Data.Common;
using Kusto.Language;
using Kusto.Language.Editor;
using Kusto.Language.Symbols;
using Kusto.Language.Utils;
using KustoTerminal.Language.Models;

namespace KustoTerminal.Language.Services
{
    public class LanguageService
    {
        private GlobalState _globalState;
        public LanguageService()
        {
            _globalState = GlobalState.Default;
        }

        public ClassificationResult GetClassifications(ITextModel textModel, string clusterName, string databaseName)
        {
            _globalState = _globalState.WithCluster(clusterName).WithDatabase(databaseName);
            
            var classifications = CodeScript.From(textModel.GetText(), _globalState)
            .Blocks
            .SelectMany(block => block.Service.GetClassifications(block.Start, block.Length).Classifications)
            .Select(classification => new Classification
            {
                Kind = classification.Kind,
                Start = classification.Start,
                Length = classification.Length
            })
            .ToList();

            return new ClassificationResult { Classifications = classifications.ToArray() };
        }

        public void AddOrUpdateCluster(string clusterName, ClusterSchema schema)
        {
            var clusterSymbol = ConvertToClusterSymbol(schema, clusterName);
            _globalState = _globalState.WithCluster(clusterSymbol);
        }

        /// <summary>
        /// Converts a Kusto.Data.Common.ClusterSchema to a Kusto.Language.Symbols.ClusterSymbol
        /// </summary>
        /// <param name="clusterSchema">The ClusterSchema to convert</param>
        /// <param name="clusterName">Optional name for the cluster symbol. If not provided, uses "cluster" as default</param>
        /// <returns>A ClusterSymbol representing the schema</returns>
        private static ClusterSymbol ConvertToClusterSymbol(ClusterSchema clusterSchema, string? clusterName = null)
        {
            if (clusterSchema == null)
                throw new ArgumentNullException(nameof(clusterSchema));

            var name = clusterName ?? "cluster";
            var databases = ConvertDatabases(clusterSchema.Databases);

            return new ClusterSymbol(name, databases);
        }

        /// <summary>
        /// Converts database schemas to database symbols
        /// </summary>
        /// <param name="databaseSchemas">Dictionary of database schemas</param>
        /// <returns>List of database symbols</returns>
        private static List<DatabaseSymbol> ConvertDatabases(IDictionary<string, DatabaseSchema>? databaseSchemas)
        {
            var databases = new List<DatabaseSymbol>();

            if (databaseSchemas == null)
                return databases;

            foreach (var dbSchemaKvp in databaseSchemas)
            {
                var databaseName = dbSchemaKvp.Key;
                var dbSchema = dbSchemaKvp.Value;
                
                var databaseMembers = CreateDatabaseMembers(dbSchema);
                var databaseSymbol = new DatabaseSymbol(databaseName, databaseMembers);
                
                databases.Add(databaseSymbol);
            }

            return databases;
        }

        /// <summary>
        /// Creates all database members (tables, functions, materialized views, entity groups)
        /// </summary>
        /// <param name="dbSchema">Database schema</param>
        /// <returns>List of database member symbols</returns>
        private static List<Symbol> CreateDatabaseMembers(DatabaseSchema dbSchema)
        {
            var databaseMembers = new List<Symbol>();
            
            databaseMembers.AddRange(ConvertTables(dbSchema.Tables));
            databaseMembers.AddRange(ConvertFunctions(dbSchema.Functions));
            databaseMembers.AddRange(ConvertMaterializedViews(dbSchema.MaterializedViews));
            databaseMembers.AddRange(ConvertEntityGroups(dbSchema.EntityGroups));

            return databaseMembers;
        }

        /// <summary>
        /// Converts table schemas to table symbols
        /// </summary>
        /// <param name="tableSchemas">Dictionary of table schemas</param>
        /// <returns>List of table symbols</returns>
        private static List<TableSymbol> ConvertTables(IDictionary<string, TableSchema>? tableSchemas)
        {
            var tables = new List<TableSymbol>();

            if (tableSchemas == null)
                return tables;

            foreach (var tableSchemaKvp in tableSchemas)
            {
                var tableName = tableSchemaKvp.Key;
                var tableSchema = tableSchemaKvp.Value;
                var columns = ConvertColumns(tableSchema.Columns);

                var tableSymbol = new TableSymbol(tableName, columns);
                tables.Add(tableSymbol);
            }

            return tables;
        }

        /// <summary>
        /// Converts column schemas to column symbols
        /// </summary>
        /// <param name="columnSchemas">Dictionary of column schemas</param>
        /// <returns>List of column symbols</returns>
        private static List<ColumnSymbol> ConvertColumns(IDictionary<string, ColumnSchema>? columnSchemas)
        {
            var columns = new List<ColumnSymbol>();

            if (columnSchemas == null)
                return columns;

            foreach (var columnSchemaKvp in columnSchemas)
            {
                var columnName = columnSchemaKvp.Key;
                var columnSchema = columnSchemaKvp.Value;
                var columnSymbol = new ColumnSymbol(
                    columnName,
                    ScalarTypes.GetSymbol(columnSchema.CslType ?? "string")
                );
                columns.Add(columnSymbol);
            }

            return columns;
        }

        /// <summary>
        /// Converts function schemas to function symbols
        /// </summary>
        /// <param name="functionSchemas">Dictionary of function schemas</param>
        /// <returns>List of function symbols</returns>
        private static List<FunctionSymbol> ConvertFunctions(IDictionary<string, FunctionSchema>? functionSchemas)
        {
            var functions = new List<FunctionSymbol>();

            if (functionSchemas == null)
                return functions;

            foreach (var functionSchemaKvp in functionSchemas)
            {
                var functionName = functionSchemaKvp.Key;
                var functionSchema = functionSchemaKvp.Value;
                // Create a basic function symbol - function conversion might need more sophisticated logic
                // depending on the function signature complexity
                var functionSymbol = new FunctionSymbol(
                    functionName,
                    functionSchema.Body ?? string.Empty
                );
                functions.Add(functionSymbol);
            }

            return functions;
        }

        /// <summary>
        /// Converts materialized view schemas to table symbols
        /// </summary>
        /// <param name="materializedViewSchemas">Dictionary of materialized view schemas</param>
        /// <returns>List of table symbols representing materialized views</returns>
        private static List<TableSymbol> ConvertMaterializedViews(IDictionary<string, TableSchema>? materializedViewSchemas)
        {
            var materializedViews = new List<TableSymbol>();

            if (materializedViewSchemas == null)
                return materializedViews;

            foreach (var materializedViewKvp in materializedViewSchemas)
            {
                var materializedViewName = materializedViewKvp.Key;
                var materializedViewSchema = materializedViewKvp.Value;
                var columns = ConvertColumns(materializedViewSchema.Columns);

                // Create a table symbol for the materialized view
                // Materialized views are treated as tables in the language service
                var materializedViewSymbol = new TableSymbol(materializedViewName, columns);
                materializedViews.Add(materializedViewSymbol);
            }

            return materializedViews;
        }

        /// <summary>
        /// Converts entity group schemas to entity group symbols
        /// </summary>
        /// <param name="entityGroupSchemas">Dictionary of entity group schemas</param>
        /// <returns>List of entity group symbols</returns>
        private static List<Symbol> ConvertEntityGroups(IDictionary<string, System.Collections.Immutable.ImmutableArray<string>>? entityGroupSchemas)
        {
            var entityGroups = new List<Symbol>();

            if (entityGroupSchemas == null || entityGroupSchemas.Count == 0)
                return entityGroups;

            foreach (var entityGroupKvp in entityGroupSchemas)
            {
                var entityGroupName = entityGroupKvp.Key;
                var entityGroupItems = entityGroupKvp.Value;
                
                var entityGroupMembers = CreateEntityGroupMembers(entityGroupItems);
                var entityGroupSymbol = new EntityGroupSymbol(entityGroupName, entityGroupMembers);
                
                entityGroups.Add(entityGroupSymbol);
            }

            return entityGroups;
        }

        /// <summary>
        /// Creates entity group members from item names
        /// </summary>
        /// <param name="entityGroupItems">List of item names in the entity group</param>
        /// <returns>List of symbols representing entity group members</returns>
        private static List<Symbol> CreateEntityGroupMembers(System.Collections.Immutable.ImmutableArray<string> entityGroupItems)
        {
            var entityGroupMembers = new List<Symbol>();
            
            // For each item in the entity group, create a simple table symbol
            // Since we don't have detailed schema, we'll create empty tables
            foreach (var itemName in entityGroupItems)
            {
                var itemSymbol = new TableSymbol(itemName, new List<ColumnSymbol>());
                entityGroupMembers.Add(itemSymbol);
            }

            return entityGroupMembers;
        }
    }
}
