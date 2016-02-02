using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web.Helpers;
using LyphTEC.MongoSimpleMembership.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LyphTEC.MongoSimpleMembership.Tests
{
    public class MembershipProviderTestFixture
    {
        public MongoSimpleMembershipProvider Provider { get; private set; }
        public IQueryable<MembershipAccount> Users { get; protected set; }
        public IQueryable<OAuthToken> OAuthTokens { get; protected set; }
        public IQueryable<OAuthMembership> OAuthMemberships { get; protected set; }

        private IMongoCollection<MembershipAccount> _usersCol;

        public MembershipProviderTestFixture()
        {
            Provider = new MongoSimpleMembershipProvider();

            var config = new NameValueCollection();
            config["connectionStringName"] = "DefaultConnection";

            Provider.Initialize("MongoSimpleMembershipProvider", config);

            SeedData(Provider.Database);
        }

        private async void SeedData(IMongoDatabase db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            
            // Reset db
            await db.DropCollectionAsync("IDSequence");
            await db.DropCollectionAsync(MembershipAccount.GetCollectionName());

            _usersCol = Provider.Database.GetCollection<MembershipAccount>(MembershipAccount.GetCollectionName());

            var salt = Crypto.GenerateSalt();

            var users = new List<MembershipAccount>
            {
                new MembershipAccount("User1")
                {
                    PasswordSalt = salt,
                    Password = Crypto.HashPassword("p@ssword" + salt),
                    IsConfirmed = false
                },

                new MembershipAccount("NonLocalUser")
                {
                    IsLocalAccount = false,
                    IsConfirmed = true
                }
            };

            await _usersCol.InsertManyAsync(users);

            await db.DropCollectionAsync(OAuthToken.GetCollectionName());
            var oAuthTokenCol = db.GetCollection<OAuthToken>(OAuthToken.GetCollectionName());
            await oAuthTokenCol.InsertOneAsync(new OAuthToken("Tok3n", "tok3nSecret"));

            await db.DropCollectionAsync(OAuthMembership.GetCollectionName());
            var oAuthMembershipCol = db.GetCollection<OAuthMembership>(OAuthMembership.GetCollectionName());
            await oAuthMembershipCol.InsertOneAsync(new OAuthMembership("Test", "User1@Test", 1) );

            Users = _usersCol.AsQueryable();
            OAuthTokens = oAuthTokenCol.AsQueryable();
            OAuthMemberships = oAuthMembershipCol.AsQueryable();
        }

        public MembershipAccount GetUserById(int id)
        {
            var filter = Builders<MembershipAccount>.Filter.Eq("_id", BsonValue.Create(id));

            var result = _usersCol.Find(filter).FirstOrDefaultAsync();

            return result.Result;
        }

        public void SaveUser(MembershipAccount user)
        {
            var filter = Builders<MembershipAccount>.Filter.Empty;
            var update = Builders<MembershipAccount>.Update.Set(x => x, user);
            var options = new UpdateOptions { IsUpsert = true };

            _usersCol.UpdateOneAsync(filter, update, options);
        }
    }
}
