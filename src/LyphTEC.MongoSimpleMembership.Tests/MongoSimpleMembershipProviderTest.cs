using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration.Provider;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Helpers;
using System.Web.Security;
using ServiceStack.Text;
using Xunit;

namespace LyphTEC.MongoSimpleMembership.Tests
{
    // ReSharper disable InconsistentNaming

    public class MongoSimpleMembershipProviderTest : IUseFixture<MembershipProviderTestFixture>
    {
        private MembershipProviderTestFixture _fixture;
        private MongoSimpleMembershipProvider _provider;

        #region IUseFixture<MembershipProviderTestFixture> Members

        public void SetFixture(MembershipProviderTestFixture data)
        {
            _fixture = data;
            _provider = data.Provider;
        }

        #endregion

        [Fact]
        public void ThrowsException_When_ConnectionStringNotConfigured()
        {
            var provider = new MongoSimpleMembershipProvider();

            var config = new NameValueCollection();

            Assert.Throws<ProviderException>(() => provider.Initialize("Test", config)).PrintDump();
        }

        [Fact]
        public void GetUserId()
        {
            var result = _provider.GetUserId("User1");
            Assert.Equal(1, result);

            var result2 = _provider.GetUserId("Foobar");
            Assert.Equal(-1, result2);
        }

        [Fact]
        public void ConfirmAccount()
        {
            var user1 = _fixture.GetUserById(1);

            var result = _provider.ConfirmAccount(user1.ConfirmationToken);
            Assert.True(result);

            var result2 = _provider.ConfirmAccount("foobartoken");
            Assert.False(result2);
        }

        [Fact]
        public void ConfirmAccountUsername()
        {
            var user = _fixture.GetUserById(1);

            var noMatchResult = _provider.ConfirmAccount("foobar", "foobartoken");
            Assert.False(noMatchResult);

            var noMatchTokenResult = _provider.ConfirmAccount("User1", "foobartoken");
            Assert.False(noMatchTokenResult);

            var result = _provider.ConfirmAccount("User1", user.ConfirmationToken);
            Assert.True(result);

            var alreadyConfirmedResult = _provider.ConfirmAccount("User1", user.ConfirmationToken);
            Assert.True(alreadyConfirmedResult);
        }

        [Fact]
        public void CreateAccount()
        {
            // invalid username
            Assert.Throws<MembershipCreateUserException>(() => _provider.CreateAccount(string.Empty, "password")).PrintDump();

            // invalid password
            Assert.Throws<MembershipCreateUserException>(() => _provider.CreateAccount("user2", string.Empty)).PrintDump();

            // duplicate username
            Assert.Throws<MembershipCreateUserException>(() => _provider.CreateAccount("user1", "password")).PrintDump();

            // requires confirmation - returns confirmation token
            var user2Token = _provider.CreateAccount("User2", "password", true);
            Assert.False(string.IsNullOrWhiteSpace(user2Token));

            var user2 = _fixture.GetUserById(2);
            Assert.False(user2.IsConfirmed);
            
            // does not require confirmation
            var user3Token = _provider.CreateAccount("User3", "password", false);
            Assert.Null(user3Token);

            var user3 = _fixture.GetUserById(3);
            Assert.True(user3.IsConfirmed);


            _fixture.Users.PrintDump();
        }

        [Fact]
        public void DeleteAccount()
        {
            var result = _provider.DeleteAccount("User1");

            Assert.True(result);

            Assert.Equal(0, _fixture.Users.Count());
        }

        [Fact]
        public void GeneratePasswordResetToken()
        {
            // user is not yet confirmed
            Assert.Throws<InvalidOperationException>(() => _provider.GeneratePasswordResetToken("User1")).PrintDump();

            // confirm & then test
            ConfirmUser1();
            
            var result = _provider.GeneratePasswordResetToken("User1");

            Assert.False(string.IsNullOrWhiteSpace(result));

            // need to re-read
            var updatedUser = _fixture.GetUserById(1);
            
            Assert.Equal(result, updatedUser.PasswordVerificationToken);
            Assert.True(updatedUser.PasswordVerificationTokenExpirationDate > DateTime.UtcNow);

            updatedUser.PrintDump();
        }

        [Fact]
        public void GetAccountsForUser()
        {
            var result = _provider.GetAccountsForUser("User1");

            Assert.Equal(1, result.Count);
            
            result.PrintDump();
        }

        [Fact]
        public void GetCreateDate()
        {
            var expected = _fixture.GetUserById(1).CreateDate;
            var actual = _provider.GetCreateDate("User1");

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetLastPasswordFailureDate()
        {
            var result = _provider.GetLastPasswordFailureDate("User1");

            Assert.Equal(DateTime.MinValue, result);
            
            // Confirm user, do a login failure & test
            ConfirmUser1();

            _provider.ValidateUser("User1", "foobar");

            var updatedUser = _fixture.GetUserById(1);

            Assert.Equal(updatedUser.LastPasswordFailureDate, _provider.GetLastPasswordFailureDate("User1"));
            Assert.Equal(1, updatedUser.PasswordFailuresSinceLastSuccess);
        }

        [Fact]
        public void GetPasswordChangedDate()
        {
            var result = _provider.GetPasswordChangedDate("User1");

            Assert.True(result > DateTime.MinValue);
        }

        [Fact]
        public void GetPasswordFailuresSinceLastSuccess()
        {
            // non-existant user
            Assert.Equal(-1, _provider.GetPasswordFailuresSinceLastSuccess("foobar"));

            Assert.Equal(0, _provider.GetPasswordFailuresSinceLastSuccess("User1"));
        }

        [Fact]
        public void GetUserIdFromPasswordResetToken()
        {
            // non-existant
            Assert.Equal(-1, _provider.GetUserIdFromPasswordResetToken("foobartoken"));

            // need to confirm & generate password verification token
            ConfirmUser1();

            var token = _provider.GeneratePasswordResetToken("User1");

            Assert.Equal(1, _provider.GetUserIdFromPasswordResetToken(token));
        }

        [Fact]
        public void IsConfirmed()
        {
            Assert.False(_provider.IsConfirmed("User1"));
            
            ConfirmUser1();

            Assert.True(_provider.IsConfirmed("User1"));
        }

        [Fact]
        public void ValidateUser()
        {
            // not confirmed
            Assert.False(_provider.ValidateUser("User1", "p@ssword"));
            
            ConfirmUser1();

            // invalid password
            Assert.False(_provider.ValidateUser("User1", "foobar"));

            var user = _fixture.GetUserById(1);
            Assert.Equal(1, user.PasswordFailuresSinceLastSuccess);
            
            // valid password
            Assert.True(_provider.ValidateUser("User1", "p@ssword"));
        }

        [Fact]
        public void ResetPasswordWithToken()
        {
            // no token
            Assert.False(_provider.ResetPasswordWithToken("foobartoken", "dummy"));
            
            ConfirmUser1();
            var token = _provider.GeneratePasswordResetToken("User1");

            // invalid token
            Assert.False(_provider.ResetPasswordWithToken("foobartoken", "dummy"));

            // valid token
            const string newPassword = "NewP@ssword";

            Assert.True(_provider.ResetPasswordWithToken(token, newPassword));
            
            // verify password has updated
            
            // TODO: why no work?
            // var user = _fixture.GetUserById(1);
            // Assert.Equal(Crypto.HashPassword(newPassword + user.PasswordSalt), user.Password);

            Assert.True(_provider.ValidateUser("User1", newPassword));
        }

        [Fact]
        public void HasLocalAccount()
        {
            Assert.True(_provider.HasLocalAccount(1));
            Assert.False(_provider.HasLocalAccount(3));
        }

        [Fact]
        public void GetUserNameFromId()
        {
            Assert.Equal("User1", _provider.GetUserNameFromId(1));
            Assert.Equal(string.Empty, _provider.GetUserNameFromId(3));
        }

        [Fact]
        public void CreateOrUpdateOAuthAccount()
        {
            // invalid user
            Assert.Throws<MembershipCreateUserException>(() => _provider.CreateOrUpdateOAuthAccount("Test", "foobar", "foobar"));

            // create
            _provider.CreateOrUpdateOAuthAccount("Test", "ProviderUserId2", "User1");
            Assert.Equal(2, _fixture.OAuthMemberships.Count());
            _fixture.OAuthMemberships.PrintDump();

            // TODO: update
        }

        [Fact]
        public void DeleteOAuthAccount()
        {
           _provider.DeleteOAuthAccount("Test", "User1@Test");

            Assert.Equal(0, _fixture.OAuthMemberships.Count());
        }

        [Fact]
        public void GetUserIdFromOAuth()
        {
            // invalid
            Assert.Equal(-1, _provider.GetUserIdFromOAuth("Test", "foobar"));

            // valid
            Assert.Equal(1, _provider.GetUserIdFromOAuth("Test", "User1@Test"));
        }

        [Fact]
        public void GetOAuthTokenSecret()
        {
            // invalid
            Assert.Equal(string.Empty, _provider.GetOAuthTokenSecret("foobar"));
            
            // valid
            Assert.Equal("tok3nSecret", _provider.GetOAuthTokenSecret("Tok3n"));
        }

        [Fact]
        public void StoreOAuthRequestToken()
        {
            // record already exists
            _provider.StoreOAuthRequestToken("Tok3n", "tok3nSecret");

            Assert.Equal(1, _fixture.OAuthTokens.Count());

            // new record
            _provider.StoreOAuthRequestToken("tk2", "tk2Secret");

            Assert.Equal(2, _fixture.OAuthTokens.Count());
            
            _fixture.OAuthTokens.PrintDump();
        }

        [Fact]
        public void ReplaceOAuthRequestTokenWithAccessToken()
        {
            // non-existant request token
            _provider.ReplaceOAuthRequestTokenWithAccessToken("foobar", "tk", "secreT");

            Assert.Equal(1, _fixture.OAuthTokens.Count());
            
            // replace
            _provider.ReplaceOAuthRequestTokenWithAccessToken("Tok3n", "tk", "secreT");

            var record = _fixture.OAuthTokens.FirstOrDefault();

            Assert.Equal("secreT", record.Secret);

            _fixture.OAuthTokens.PrintDump();
        }

        [Fact]
        public void DeleteOAuthToken()
        {
            _provider.DeleteOAuthToken("Tok3n");

            Assert.Equal(0, _fixture.OAuthTokens.Count());
        }

        [Fact]
        public void ChangePassword()
        {
            // invalid user
            Assert.False(_provider.ChangePassword("foobar", "old", "new"));
            
            // invalid old p/w
            Assert.False(_provider.ChangePassword("User1", "foobar", "new"));
            
            // valid
            Assert.True(_provider.ChangePassword("User1", "p@ssword", "newPassword1"));
            ConfirmUser1();
            Assert.True(_provider.ValidateUser("User1", "newPassword1"));
        }

        [Fact]
        public void DeleteUser()
        {
            Assert.True(_provider.DeleteUser("User1", true));

            Assert.Equal(0, _fixture.Users.Count());
            Assert.Equal(0, _fixture.OAuthMemberships.Count());
        }

        [Fact]
        public void GetAllUsers()
        {
            int totalRecords;
            var result = _provider.GetAllUsers(0, 10, out totalRecords);

            Assert.Equal(1, result.Count);
            
            result.PrintDump();
            totalRecords.PrintDump();
        }

        [Fact]
        public void GetUser()
        {
            // non-existant
            Assert.Null(_provider.GetUser("foobar", false));

            // valid
            var result = _provider.GetUser("User1", false);
            Assert.NotNull(result);
            result.PrintDump();
        }

        [Fact]
        public void GetUserByKey()
        {
            // non-existant
            Assert.Null(_provider.GetUser(3, false));

            // valid
            var result = _provider.GetUser(1, false);
            Assert.NotNull(result);
            result.PrintDump();
        }


        private void ConfirmUser1()
        {
            var user = _fixture.GetUserById(1);
            user.IsConfirmed = true;
            _fixture.SaveUser(user);
        }
    }
}
