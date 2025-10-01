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

                    // Combine tables and functions as database members
                    var databaseMembers = new List<Symbol>();
                    databaseMembers.AddRange(tables);
                    databaseMembers.AddRange(functions);

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
