using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_Bot_Csharp.src.Data_Access
{
    public class MongoRepository<T>
    {
        private IMongoDatabase _database;

        private MongoClient _mongoClient;

        public MongoRepository(string connectionString, string databaseName)
        {
            this._mongoClient = new MongoClient(connectionString);
            this._database = _mongoClient.GetDatabase(databaseName);
        }

        public async Task DropDatabase(string databaseName)
        {
            await _mongoClient.DropDatabaseAsync(databaseName);
        }

        public IMongoCollection<T> GetCollection(Type type)
        {
            return _database.GetCollection<T>(type.Name);
        }
    }
}