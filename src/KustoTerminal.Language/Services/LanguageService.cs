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
            var databases = new List<DatabaseSymbol>();

            // Convert each database from the schema
            if (clusterSchema.Databases != null)
            {
                foreach (var dbSchemaKvp in clusterSchema.Databases)
                {
                    var databaseName = dbSchemaKvp.Key;
                    var dbSchema = dbSchemaKvp.Value;
                    var tables = new List<TableSymbol>();

                    // Convert tables if they exist
                    if (dbSchema.Tables != null)
                    {
                        foreach (var tableSchemaKvp in dbSchema.Tables)
                        {
                            var tableName = tableSchemaKvp.Key;
                            var tableSchema = tableSchemaKvp.Value;
                            var columns = new List<ColumnSymbol>();

                            // Convert columns if they exist
                            if (tableSchema.Columns != null)
                            {
                                foreach (var columnSchemaKvp in tableSchema.Columns)
                                {
                                    var columnName = columnSchemaKvp.Key;
                                    var columnSchema = columnSchemaKvp.Value;
                                    var columnSymbol = new ColumnSymbol(
                                        columnName,
                                        ScalarTypes.GetSymbol(columnSchema.CslType ?? "string")
                                    );
                                    columns.Add(columnSymbol);
                                }
                            }

                            var tableSymbol = new TableSymbol(
                                tableName,
                                columns
                            );
                            tables.Add(tableSymbol);
                        }
                    }

                    // Convert functions if they exist
                    var functions = new List<FunctionSymbol>();
                    if (dbSchema.Functions != null)
                    {
                        foreach (var functionSchemaKvp in dbSchema.Functions)
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
                    }

                    // Convert materialized views if they exist
                    var materializedViews = new List<TableSymbol>();
                    if (dbSchema.MaterializedViews != null)
                    {
                        foreach (var materializedViewKvp in dbSchema.MaterializedViews)
                        {
                            var materializedViewName = materializedViewKvp.Key;
                            var materializedViewSchema = materializedViewKvp.Value;
                            var columns = new List<ColumnSymbol>();

                            // Convert columns if they exist
                            if (materializedViewSchema.Columns != null)
                            {
                                foreach (var columnSchemaKvp in materializedViewSchema.Columns)
                                {
                                    var columnName = columnSchemaKvp.Key;
                                    var columnSchema = columnSchemaKvp.Value;
                                    var columnSymbol = new ColumnSymbol(
                                        columnName,
                                        ScalarTypes.GetSymbol(columnSchema.CslType ?? "string")
                                    );
                                    columns.Add(columnSymbol);
                                }
                            }

                            // Create a table symbol for the materialized view
                            // Materialized views are treated as tables in the language service
                            var materializedViewSymbol = new TableSymbol(
                                materializedViewName,
                                columns
                            );
                            materializedViews.Add(materializedViewSymbol);
                        }
                    }

                    // Convert entity groups if they exist  
                    var entityGroups = new List<Symbol>();
                    if (dbSchema.EntityGroups != null && dbSchema.EntityGroups.Count > 0)
                    {
                        foreach (var entityGroupKvp in dbSchema.EntityGroups)
                        {
                            var entityGroupName = entityGroupKvp.Key;
                            var entityGroupItems = entityGroupKvp.Value;
                            
                            // EntityGroups contains a collection of items (likely table names)
                            // Create a database symbol to represent the entity group with its items
                            var entityGroupMembers = new List<Symbol>();
                            
                            // For each item in the entity group, create a simple table symbol
                            // Since we don't have detailed schema, we'll create empty tables
                            foreach (var itemName in entityGroupItems)
                            {
                                var itemSymbol = new TableSymbol(itemName, new List<ColumnSymbol>());
                                entityGroupMembers.Add(itemSymbol);
                            }

                            var entityGroupSymbol = new EntityGroupSymbol(
                                entityGroupName,
                                entityGroupMembers
                            );
                            entityGroups.Add(entityGroupSymbol);
                        }
                    }

                    // Combine tables, functions, materialized views, and entity groups as database members
                    var databaseMembers = new List<Symbol>();
                    databaseMembers.AddRange(tables);
                    databaseMembers.AddRange(functions);
                    databaseMembers.AddRange(materializedViews);
                    databaseMembers.AddRange(entityGroups);

                    var databaseSymbol = new DatabaseSymbol(
                        databaseName,
                        databaseMembers
                    );
                    databases.Add(databaseSymbol);
                }
            }

            return new ClusterSymbol(name, databases);
        }
    }
}
