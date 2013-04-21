using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

// Based on this https://github.com/alexjamesbrown/MongDBIntIdGenerator

namespace LyphTEC.MongoSimpleMembership.Services
{
    internal class IntIdGenerator : IIdGenerator
    {
        #region IIdGenerator Members

        public object GenerateId(object container, object document)
        {
            var idSequenceCollection = ((MongoCollection)container).Database.GetCollection("IDSequence");

            var query = Query.EQ("_id", ((MongoCollection)container).Name);
            
            return idSequenceCollection
                .FindAndModify(query, null, Update.Inc("seq", 1), true, true)
                .ModifiedDocument["seq"]
                .AsInt32;
        }

        public bool IsEmpty(object id)
        {
            if (ReferenceEquals(null, id))
                return true;

            return (int) id == default(int);
        }

        #endregion

        public static IntIdGenerator Instance
        {
            get { return new IntIdGenerator(); }
        }
    }
}