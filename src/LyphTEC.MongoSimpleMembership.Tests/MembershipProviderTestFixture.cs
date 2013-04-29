using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Helpers;
using LyphTEC.MongoSimpleMembership.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LyphTEC.MongoSimpleMembership.Tests
{
    public class MembershipProviderTestFixture
    {
        public MongoSimpleMembershipProvider Provider { get; private set; }
        public IQueryable<MembershipAccount> Users { get; protected set; }
        public IQueryable<OAuthToken> OAuthTokens { get; protected set; }
        public IQueryable<OAuthMembership> OAuthMemberships { get; protected set; }

        private MongoCollection<MembershipAccount> _usersCol;

        public MembershipProviderTestFixture()
        {
            Provider = new MongoSimpleMembershipProvider();

            var config = new NameValueCollection();
            config["connectionStringName"] = "DefaultConnection";

            Provider.Initialize("MongoSimpleMembershipProvider", config);

            SeedData(Provider.Database);
        }

        private void SeedData(MongoDatabase db)
        {
            if (db == null) throw new ArgumentNullException("db");
            
            // Reset db
            db.GetCollection("IDSequence").Drop();

            _usersCol = db.GetCollection<MembershipAccount>(MembershipAccount.GetCollectionName());
            _usersCol.Drop();

            var salt = Crypto.GenerateSalt();

            var user1 = new MembershipAccount("User1")
                            {
                                PasswordSalt =  salt,
                                Password = Crypto.HashPassword("p@ssword" + salt),
                                IsConfirmed = false
                            };

            _usersCol.Insert(user1);

            var oAuthTokenCol = db.GetCollection<OAuthToken>(OAuthToken.GetCollectionName());
            oAuthTokenCol.Drop();
            oAuthTokenCol.Insert(new OAuthToken("Tok3n", "tok3nSecret"));

            var oAuthMembershipCol = db.GetCollection<OAuthMembership>(OAuthMembership.GetCollectionName());
            oAuthMembershipCol.Drop();
            oAuthMembershipCol.Insert( new OAuthMembership("Test", "User1@Test", 1) );

            Users = _usersCol.AsQueryable();
            OAuthTokens = oAuthTokenCol.AsQueryable();
            OAuthMemberships = oAuthMembershipCol.AsQueryable();
        }

        public MembershipAccount GetUserById(int id)
        {
            return _usersCol.FindOneById(BsonValue.Create(id));
        }

        public void SaveUser(MembershipAccount user)
        {
            _usersCol.Save(user);
        }
    }
}
