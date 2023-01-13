using MongoDB.Driver;
using MongoDB.Entities;
using QuranSchool.Models;
using System.Diagnostics;
using DevExpress.CodeParser.VB;
using Microsoft.AspNetCore.Identity;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Transaction = QuranSchool.Models.Transaction;

namespace QuranSchool.Helpers;

public class DatabaseHelper
{
    public static async Task InitDb(string dbName, string connectionString)
    {
        var clientSettings = MongoClientSettings.FromConnectionString(connectionString);
        /*clientSettings.Compressors = new List<CompressorConfiguration>() 
        {
            new CompressorConfiguration(CompressorType.ZStandard)
        };*/
        clientSettings.ApplicationName = "50LAB Private School";

        await DB.InitAsync(dbName, clientSettings);
        try
        {
            BsonSerializer.RegisterSerializer(new MongoDBDateTimeSerializer());
        }
        catch
        {
        }

        await CreateIndices();
    }

    /*public static void InitCache()
    {
        if (!Directory.Exists(AppContext.BaseDirectory + @"\Cache"))
            Directory.CreateDirectory(AppContext.BaseDirectory + @"\Cache");

        BlobCache.LocalMachine = new SqlRawPersistentBlobCache(AppContext.BaseDirectory + @"\Cache\cache.db");

        Registrations.Start(_config["MongoDB.DatabaseName"]);
    }*/


    private static async Task CreateIndices()
    {
        await DB.Index<Account>()
            .Key(b => b.TenantId, KeyType.Ascending)
            .CreateAsync();

        //await DB.Index<Account>()
        //    .Key(x => x.PersonalId, KeyType.Ascending)
        //    .Option(x => x.Unique = true)
        //    .CreateAsync();

        /*await DB.Index<Account>()
            .Key(x => x.Email, KeyType.Ascending)
            .Option(x => x.Unique = true)
            .CreateAsync();*/

        await DB.Index<Account>()
            .Key(b => b.UserName, KeyType.Ascending)
            .Option(x => x.Unique = true)
            .CreateAsync();

        await DB.Index<Invoice>()
            .Key(x => x.InvoiceID, KeyType.Ascending)
            .Option(x => x.Unique = true)
            .CreateAsync();

        await DB.Index<Invoice>()
            .Key(b => b.Enabled, KeyType.Ascending)
            .Key(b => b.Type, KeyType.Ascending)
            .CreateAsync();

        await DB.Index<Invoice>().Key(b => b.Type, KeyType.Ascending)
            .Key(b => b.Student.ID, KeyType.Ascending)
            .Key(b => b.CreatedBy.ID, KeyType.Ascending)
            .Key(b => b.CreatedOn, KeyType.Descending)
            .Key(b => b.Formation.ID, KeyType.Ascending)
            .Key(b => b.TenantId, KeyType.Ascending)
            .CreateAsync();

        await DB.Index<Invoice>()
            .Key(b => b.ID, KeyType.Text)
            .Key(b => b.InvoiceID, KeyType.Text)
            .Key(b => b.Formation.Name, KeyType.Text)
            .CreateAsync();

        await DB.Index<Account>()
            .Key(b => b.Name, KeyType.Text)
            .Key(b => b.UserName, KeyType.Text)
            .Key(b => b.ID, KeyType.Text)
            .Key(b => b.Description, KeyType.Text)
            .CreateAsync();

        await DB.Index<Transaction>()
            .Key(b => b.Enabled, KeyType.Ascending)
            .Key(b => b.CreatedOn, KeyType.Descending)
            .Key(b => b.Type, KeyType.Ascending)
            .Key(b => b.TenantId, KeyType.Ascending)
            .CreateAsync();

        await DB.Index<Formation>()
            .Key(b => b.Name, KeyType.Ascending)
            .Key(b => b.Price, KeyType.Ascending)
            .Key(b => b.Enabled, KeyType.Ascending)
            .Key(b => b.TenantId, KeyType.Ascending)
            .CreateAsync();

        await DB.Index<Group>()
            .Key(b => b.Name, KeyType.Ascending)
            .Key(b => b.Teacher.ID, KeyType.Ascending)
            .Key(b => b.Formation.ID, KeyType.Ascending)
            .Key(b => b.TenantId, KeyType.Ascending)
            .CreateAsync();

        await DB.Index<Group>()
            .Key(b => b.Students[0].ID, KeyType.Ascending)
            .Key(b => b.Students[0].TenantId, KeyType.Ascending)
            .CreateAsync();

        await DB.Index<Group>()
            .Key(b => b.Sessions[0].Start, KeyType.Ascending)
            .Key(b => b.Sessions[0].End, KeyType.Ascending)
            .CreateAsync();

        await DB.Index<Group>()
            .Key(b => b.Name, KeyType.Text)
            .Key(b => b.Formation.Name, KeyType.Text)
            .Key(b => b.Teacher, KeyType.Text)
            .CreateAsync();

        await DB.Index<Group>()
            .Key(b => b.Students[0].Name, KeyType.Text)
            .CreateAsync();

        await DB.Index<Student>()
            .Key(b => b.Name, KeyType.Text)
            .Key(b => b.Parents[0].Name, KeyType.Text)
            .Key(b => b.PhoneNumber, KeyType.Text)
            .Key(b => b.Parents[0].PhoneNumber, KeyType.Text)
            .CreateAsync();


        await DB.Index<Student>()
            .Key(b => b.TenantId, KeyType.Ascending)
            .Key(b => b.Name, KeyType.Ascending)
            .Key(b => b.PhoneNumber, KeyType.Ascending)
            .CreateAsync();

        await DB.Index<Student>()
            .Key(b => b.Parents[0].Name, KeyType.Ascending)
            .Key(b => b.Parents[0].PhoneNumber, KeyType.Ascending)
            .CreateAsync();

        await DB.Index<Teacher>()
            .Key(b => b.Name, KeyType.Ascending)
            .Key(b => b.TenantId, KeyType.Ascending)
            .CreateAsync();

        await DB.Index<Teacher>()
            .Key(b => b.Name, KeyType.Text)
            .CreateAsync();

        try
        {
            var exist = await DB.Find<SchoolInfo>().ExecuteAnyAsync();
            if (!exist)
                await new SchoolInfo
                {
                    Name = "50LAB MADRASA",
                    Address = "Laghouat",
                    Website = "https://50lab.tech",
                    PhoneNumber = "0674878700"
                }.InsertAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }

        //await Task.WhenAll(invoiceIndex, accountIndex, transactionIndex, formationIndex);
    }
}