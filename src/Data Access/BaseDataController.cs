using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_Bot_Csharp.src.Data_Access
{
    public class BaseDataController<T>
    {
        protected string _connectionString { get; set; }
        protected string _databaseName { get; set; }

        public BaseDataController(string connectionString)
        {
            _connectionString = connectionString;
            _databaseName = "VecnaBot";
        }

        protected MongoRepository<T> GetRepository()
        {
            return new MongoRepository<T>(_connectionString, _databaseName);
        }

        public IMongoCollection<T> GetCollection()
        {
            return GetRepository().GetCollection(typeof(T));
        }

        public IMongoQueryable<T> GetQuery()
        {
            return GetCollection().AsQueryable<T>();
        }
    }
}

