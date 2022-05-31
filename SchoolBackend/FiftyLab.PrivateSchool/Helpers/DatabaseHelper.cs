using System.Diagnostics;
using Akavache;
using Akavache.Sqlite3;
using FiftyLab.PrivateSchool.Models;
using MongoDB.Driver;
using MongoDB.Entities;
using Registrations = Akavache.Registrations;

namespace FiftyLab.PrivateSchool.Helpers;

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
        await CreateIndices();
        await CreateDefaultAdminAccount();
    }

    public static void InitCache()
    {
        if (!Directory.Exists(AppContext.BaseDirectory + @"\Cache"))
            Directory.CreateDirectory(AppContext.BaseDirectory + @"\Cache");

        BlobCache.LocalMachine = new SqlRawPersistentBlobCache(AppContext.BaseDirectory + @"\Cache\cache.db");

        Registrations.Start("CrecheDb");
    }

    private static async Task CreateDefaultAdminAccount()
    {
        try
        {
            var adminExist = await DB.Find<Account>().Match(x => x.UserName == "admin").ExecuteAnyAsync();
            if (!adminExist)
            {
                var account = new Account("Default Administrator", "admin", "adminadmin", "Default Administrator");
                account.Role = AccountType.Admin;
                await account.InsertAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private static async Task CreateIndices()
    {
        await DB.Index<Account>()
            .Key(b => b.UserName, KeyType.Ascending)
            .Option(x => x.Unique = true)
            .CreateAsync();

        await DB.Index<Invoice>()
            .Key(x => x.InvoiceID, KeyType.Ascending)
            .Option(x => x.Unique = true)
            .CreateAsync();

        await DB.Index<Invoice>().Key(b => b.Enabled, KeyType.Ascending).CreateAsync();
        await DB.Index<Invoice>().Key(b => b.Type, KeyType.Ascending).CreateAsync();

        await DB.Index<Invoice>().Key(b => b.Type, KeyType.Ascending)
            .Key(b => b.Student.ID, KeyType.Ascending)
            .Key(b => b.CreatedBy.ID, KeyType.Ascending)
            .Key(b => b.CreatedOn, KeyType.Descending)
            .Key(b => b.Formation.ID, KeyType.Ascending)
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
            .CreateAsync();

        await DB.Index<Formation>()
            .Key(b => b.Name, KeyType.Ascending)
            .Key(b => b.Price, KeyType.Ascending)
            .Key(b => b.Enabled, KeyType.Ascending)
            .CreateAsync();

        try
        {
            await DB.CreateCollectionAsync<SchoolInfo>(options =>
            {
                options.MaxDocuments = 1;
                options.MaxSize = 1;
                options.Capped = true;
            });
            await new SchoolInfo()
            {
                Name = "50LAB SCHOOL APP",
                Address = "Laghouat",
                Website = "https://50lab.tech",
                PhoneNumber = "0674878700",
            }.InsertAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }

        //await Task.WhenAll(invoiceIndex, accountIndex, transactionIndex, formationIndex);
    }
}