using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

// Based on this https://github.com/alexjamesbrown/MongDBIntIdGenerator

namespace LyphTEC.MongoSimpleMembership.Services
{
    internal class IntIdGenerator<T> : IIdGenerator where T : class
    {
        #region IIdGenerator Members

        public object GenerateId(object container, object document)
        {
            var idSequenceCollection = ((IMongoCollection<T>)container).Database.GetCollection<BsonDocument>("IDSequence");

            var filter = Builders<BsonDocument>.Filter.Eq("_id", typeof(T).Name);
            var update = Builders<BsonDocument>.Update.Inc("seq", 1);
            var options = new FindOneAndUpdateOptions<BsonDocument>
            {
                ReturnDocument = ReturnDocument.After,
                IsUpsert = true
            };

            var result = idSequenceCollection.FindOneAndUpdateAsync(filter, update, options);

            return result.Result["seq"].AsInt32;
        }

        public bool IsEmpty(object id)
        {
            if (ReferenceEquals(null, id))
                return true;

            return (int) id == default(int);
        }

        #endregion

        public static IntIdGenerator<T> Instance => new IntIdGenerator<T>();
    }
}