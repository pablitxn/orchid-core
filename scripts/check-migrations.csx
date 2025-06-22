#!/usr/bin/env dotnet-script
#r "nuget: Npgsql, 8.0.5"

using Npgsql;
using System;

var connectionString = "Host=localhost;Port=5433;Database=orchid_core;Username=admin;Password=admin";

try
{
    using var connection = new NpgsqlConnection(connectionString);
    connection.Open();
    Console.WriteLine("✓ Connected to database successfully");
    
    // Check if __EFMigrationsHistory table exists
    using var checkTableCmd = new NpgsqlCommand(@"
        SELECT EXISTS (
            SELECT FROM information_schema.tables 
            WHERE table_schema = 'public' 
            AND table_name = '__EFMigrationsHistory'
        );", connection);
    
    var tableExists = (bool)checkTableCmd.ExecuteScalar();
    Console.WriteLine($"✓ __EFMigrationsHistory table exists: {tableExists}");
    
    if (tableExists)
    {
        // List all applied migrations
        using var listMigrationsCmd = new NpgsqlCommand(@"
            SELECT ""MigrationId"", ""ProductVersion"" 
            FROM ""__EFMigrationsHistory"" 
            ORDER BY ""MigrationId"";", connection);
        
        using var reader = listMigrationsCmd.ExecuteReader();
        Console.WriteLine("\nApplied Migrations:");
        while (reader.Read())
        {
            Console.WriteLine($"  - {reader.GetString(0)} (EF Core {reader.GetString(1)})");
        }
    }
    
    // List all tables
    using var listTablesCmd = new NpgsqlCommand(@"
        SELECT table_name 
        FROM information_schema.tables 
        WHERE table_schema = 'public' 
        ORDER BY table_name;", connection);
    
    using var tableReader = listTablesCmd.ExecuteReader();
    Console.WriteLine("\nDatabase Tables:");
    while (tableReader.Read())
    {
        Console.WriteLine($"  - {tableReader.GetString(0)}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
}