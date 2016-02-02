using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Configuration.Provider;
using System.Diagnostics;
using System.Linq;
using System.Web.Helpers;
using System.Web.Security;
using LyphTEC.MongoSimpleMembership.Extensions;
using LyphTEC.MongoSimpleMembership.Helpers;
using LyphTEC.MongoSimpleMembership.Models;
using LyphTEC.MongoSimpleMembership.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using WebMatrix.WebData;

namespace LyphTEC.MongoSimpleMembership
{
    public class MongoSimpleMembershipProvider : ExtendedMembershipProvider
    {
        private string _appName;
        private MongoDataContext _context;
        private bool _isInitialized = false;
        private string _providerName;
        
        private int _minRequiredPasswordLength;
        private int _minRequiredNonalphanumericCharacters;
        private string _passwordStrengthRegularExpression = string.Empty;

        public IMongoDatabase Database
        {
            get
            {
                VerifyInitialized();

                return _context.Database;
            }
        }

        public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config)
        {
            Check.IsNotNull(config);

            if (string.IsNullOrWhiteSpace(name))
                name = this.GetType().Name;

            _providerName = name;

            if (string.IsNullOrWhiteSpace(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "MongoDB SimpleMembership provider");
            }

            base.Initialize(name, config);

            _appName = Util.GetValueOrDefault(config, "applicationName", o => o.ToString(), Util.GetDefaultAppName());
            Util.CheckAppNameLength(_appName);

            var connStringName = Util.GetValueOrDefault(config, "connectionStringName", o => o.ToString(), string.Empty);

            if (string.IsNullOrWhiteSpace(connStringName))
                throw new ProviderException("connectionStringName not specified");

            var connSettings = ConfigurationManager.ConnectionStrings[connStringName];
            Util.CheckConnectionStringSettings(connStringName, connSettings);

            InitializeContext(connSettings.ConnectionString);

            _minRequiredPasswordLength = Util.GetValueOrDefault(config, "minRequiredPasswordLength", Convert.ToInt32, 6);
            _minRequiredNonalphanumericCharacters = Util.GetValueOrDefault(config, "minRequiredNonalphanumericCharacters", Convert.ToInt32, 0);
            _passwordStrengthRegularExpression = Util.GetValueOrDefault(config, "passwordStrengthRegularExpression", Convert.ToString, string.Empty);

            config.Remove("name");
            config.Remove("description");
            config.Remove("applicationName");
            config.Remove("connectionStringName");
            config.Remove("enablePasswordRetrieval");
            config.Remove("enablePasswordReset");
            config.Remove("requiresQuestionAndAnswer");
            config.Remove("requiresUniqueEmail");
            config.Remove("maxInvalidPasswordAttempts");
            config.Remove("passwordAttemptWindow");
            config.Remove("passwordFormat");
            config.Remove("minRequiredPasswordLength");
            config.Remove("minRequiredNonalphanumericCharacters");
            config.Remove("passwordStrengthRegularExpression");
            config.Remove("hashAlgorithmType");

            if (config.Count <= 0)
            {
                _isInitialized = true;
                return;
            }

            var unrecognized = config.GetKey(0);
            if (!string.IsNullOrWhiteSpace(unrecognized))
                throw new ProviderException(string.Format("Specified config setting '{0}' is not supported by this provider.", unrecognized));
        }

        private void InitializeContext(string connectionString)
        {
            Check.IsNotNull(connectionString);

            try
            {
                _context = new MongoDataContext(connectionString);
            }
            catch (Exception ex)
            {
                throw new ProviderException("Error initializing the provider. See inner exception for details.", ex);
            }
        }

        private void VerifyInitialized()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Provider hasn't been initialized yet. Please call Initialize() first.");
        }

        public int GetUserId(string userName)
        {
            VerifyInitialized();

            var user = _context.GetUser(userName);

            return user == null ? -1 : user.UserId;
        }

        #region ExtendedMembershipProvider overrides

        /// <summary>
        /// Sets the confirmed flag for the token
        /// </summary>
        /// <param name="accountConfirmationToken"></param>
        /// <returns></returns>
        public override bool ConfirmAccount(string accountConfirmationToken)
        {
            Check.IsNotNull(accountConfirmationToken);

            VerifyInitialized();

            try
            {
                // need to do case insensitive compare
                var user = _context.Users.SingleOrDefault(x => x.ConfirmationToken.ToLowerInvariant() == accountConfirmationToken.ToLowerInvariant());

                if (user == null)
                {
                    Trace.TraceInformation("MongoSimpleMembershipProvider.ConfirmAccount() : No user with token = {0} found", accountConfirmationToken);
                    return false;
                }

                // Already confirmed, so no need to update DB
                if (user.IsConfirmed)
                {
                    Trace.TraceInformation("MongoSimpleMembershipProvider.ConfirmAccount() : User matching token = {0} is already confirmed", accountConfirmationToken);
                    return true;
                }

                user.IsConfirmed = true;

                return _context.Save(user);
            }
            catch (Exception ex)
            {
                throw new ProviderException(string.Format("Error confirming account with token = {0}. See inner exception for details", accountConfirmationToken), ex);
            }
        }

        /// <summary>
        /// Sets the confirmed flag for the username
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="accountConfirmationToken"></param>
        /// <returns></returns>
        public override bool ConfirmAccount(string userName, string accountConfirmationToken)
        {
            Check.IsNotNull(userName);
            Check.IsNotNull(accountConfirmationToken);

            VerifyInitialized();

            try
            {
                var user = _context.GetUser(userName);

                if (user == null)
                {
                    Trace.TraceInformation("MongoSimpleMembershipProvider.ConfirmAccount() : No matching user found with username = {0}", userName);
                    return false;
                }

                if (user.IsConfirmed)
                {
                    Trace.TraceInformation("MongoSimpleMembershipProvider.ConfirmAccount() : User matching username = {0} is already confirmed", userName);
                    return true;
                }

                if (!string.Equals(user.ConfirmationToken, accountConfirmationToken, StringComparison.Ordinal))
                {
                    Trace.TraceInformation("MongoSimpleMembershipProvider.ConfirmAccount() : Cannot confirm - tokens don't match");
                    return false;
                }

                user.IsConfirmed = true;

                return _context.Save(user);
            }
            catch (Exception ex)
            {
                throw new ProviderException(string.Format("Error confirming account with username = {0} & token = {1}. See inner exception for details", userName, accountConfirmationToken), ex);
            }
        }

        public override string CreateAccount(string userName, string password, bool requireConfirmationToken)
        {
            return CreateUserAndAccount(userName, password, requireConfirmationToken, null);
        }

        public override string CreateUserAndAccount(string userName, string password, bool requireConfirmation, IDictionary<string, object> values)
        {
            VerifyInitialized();

            if (string.IsNullOrWhiteSpace(userName))
                throw new MembershipCreateUserException(MembershipCreateStatus.InvalidUserName);

            if (string.IsNullOrWhiteSpace(password))
                throw new MembershipCreateUserException(MembershipCreateStatus.InvalidPassword);

            var user = _context.GetUser(userName);

            // existing local accounts are duplicates
            if (user != null && user.IsLocalAccount)
                throw new MembershipCreateUserException(MembershipCreateStatus.DuplicateUserName);

            var salt = Crypto.GenerateSalt();
            var hashedPassword = Crypto.HashPassword(password + salt);

            if (hashedPassword.Length > 128)
                throw new MembershipCreateUserException(MembershipCreateStatus.InvalidPassword);
                
            // create a new local account
            if (user == null)
            {
                user = new MembershipAccount(userName)
                           {
                               PasswordSalt = salt,
                               Password = hashedPassword,
                               IsConfirmed = !requireConfirmation
                           };
            }
            else
            {
                // convert a non-local account
                user.IsLocalAccount = true;
                user.PasswordSalt = salt;
                user.Password = hashedPassword;
                user.IsConfirmed = !requireConfirmation;    // should already be confirmed
                user.PasswordChangedDate = DateTime.UtcNow;
                user.LastPasswordFailureDate = null;
                user.PasswordFailuresSinceLastSuccess = 0;
            }

            if (values != null)
                user.ExtraData = values.ToJson();

            try
            {
                _context.Save(user);
            }
            catch (Exception ex)
            {
                Trace.TraceError("MongoSimpleMembershipProvider.CreateUserAndAccount() ERROR: {0}", ex.ToString());
                throw new MembershipCreateUserException(MembershipCreateStatus.ProviderError);
            }

            return requireConfirmation ? user.ConfirmationToken : null;
        }

        public override bool DeleteAccount(string userName)
        {
            Check.IsNotNull(userName);

            VerifyInitialized();

            var user = _context.GetUser(userName);

            return user != null && _context.RemoveById<MembershipAccount>(user.UserId);
        }

        public override string GeneratePasswordResetToken(string userName, int tokenExpirationInMinutesFromNow)
        {
            Check.IsNotNull(userName);

            VerifyInitialized();

            var userId = VerifyConfirmedAccount(userName);

            var user = _context.FindOneById<MembershipAccount>(userId);

            var token = (user.PasswordVerificationTokenExpirationDate.HasValue
                         && user.PasswordVerificationTokenExpirationDate.Value > DateTime.UtcNow)
                            ? user.PasswordVerificationToken
                            : null;

            if (string.IsNullOrWhiteSpace(token))
            {
                token = Util.GenerateToken();

                user.PasswordVerificationToken = token;
                user.PasswordVerificationTokenExpirationDate = DateTime.UtcNow.AddMinutes(tokenExpirationInMinutesFromNow);

                if (!_context.Save(user))
                    throw new ProviderException("MongoSimpleMembershipProvider.GeneratePasswordResetToken() error. Unable to update password verification token for user.");
            }

            return token;
        }

        public override ICollection<OAuthAccountData> GetAccountsForUser(string userName)
        {
            Check.IsNotNull(userName);

            VerifyInitialized();

            var user = _context.GetUser(userName);

            if (user == null || !_context.OAuthMemberships.Any())
                return new Collection<OAuthAccountData>();

            return _context.OAuthMemberships.Where(x => x.UserId == user.UserId).Select(x => new OAuthAccountData(x.Provider, x.ProviderUserId)).ToList();
        }

        public override DateTime GetCreateDate(string userName)
        {
            Check.IsNotNull(userName);

            VerifyInitialized();

            var user = _context.GetUser(userName);

            return user == null
                       ? DateTime.MinValue
                       : user.CreateDate;
        }

        public override DateTime GetLastPasswordFailureDate(string userName)
        {
            Check.IsNotNull(userName);

            VerifyInitialized();

            var user = _context.GetUser(userName);

            return user == null || !user.LastPasswordFailureDate.HasValue
                       ? DateTime.MinValue
                       : user.LastPasswordFailureDate.Value;
        }

        public override DateTime GetPasswordChangedDate(string userName)
        {
            Check.IsNotNull(userName);

            VerifyInitialized();

            var user = _context.GetUser(userName);

            return user == null || !user.PasswordChangedDate.HasValue
                       ? DateTime.MinValue
                       : user.PasswordChangedDate.Value;
        }

        public override int GetPasswordFailuresSinceLastSuccess(string userName)
        {
            Check.IsNotNull(userName);

            VerifyInitialized();

            var user = _context.GetUser(userName);

            return user == null
                       ? -1
                       : user.PasswordFailuresSinceLastSuccess;
        }

        public override int GetUserIdFromPasswordResetToken(string token)
        {
            VerifyInitialized();

            try
            {
                // Will throw exception if duplicate token found
                var user = _context.Users.SingleOrDefault(x => x.PasswordVerificationToken.ToLowerInvariant() == token.ToLowerInvariant());

                return user == null ? -1 : user.UserId;
            }
            catch (Exception ex)
            {
                Trace.TraceError("MongoSimpleMembershipProvider.GetUserIdFromPasswordResetToken() ERROR: {0}", ex.ToString());
            }

            return -1;
        }

        public override bool IsConfirmed(string userName)
        {
            var userId = VerifyConfirmedAccount(userName, false);

            return (userId != -1);
        }

        private int VerifyConfirmedAccount(string userName, bool throwException = true)
        {
            Check.IsNotNull(userName);

            VerifyInitialized();

            var user = _context.GetUser(userName);

            if (user == null)
            {
                if (throwException)
                    throw new InvalidOperationException(string.Format("No account found matching username : {0}", userName));

                return -1;
            }

            if (!user.IsConfirmed)
            {
                if (throwException)
                    throw new InvalidOperationException(string.Format("No account found matching username : {0}", userName));

                return -1;
            }

            return user.UserId;
        }

        public override bool ResetPasswordWithToken(string token, string newPassword)
        {
            Check.IsNotNull(token);
            Check.IsNotNull(newPassword);

            VerifyInitialized();

            try
            {
                // this will throw Mongo exception if PasswordVerificationToken field is null
                var user = _context.Users.SingleOrDefault(x => x.PasswordVerificationToken.ToLowerInvariant() == token.ToLowerInvariant());

                if (user == null)
                    return false;

                if (user.PasswordVerificationTokenExpirationDate.HasValue && user.PasswordVerificationTokenExpirationDate.Value > DateTime.UtcNow)
                {
                    var success = SetPassword(user, newPassword, true);

                    return success;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("MongoSimpleMembershipProvider.ResetPasswordWithToken() ERROR: {0}", ex.ToString());
            }

            return false;
        }

        public override bool HasLocalAccount(int userId)
        {
            VerifyInitialized();

            var user = _context.FindOneById<MembershipAccount>(userId);

            return (user != null && user.IsLocalAccount);
        }

        public override string GetUserNameFromId(int userId)
        {
            var user = _context.FindOneById<MembershipAccount>(userId);

            return user == null
                       ? string.Empty
                       : user.UserName;
        }

        public override void CreateOrUpdateOAuthAccount(string provider, string providerUserId, string userName)
        {
            VerifyInitialized();

            if (string.IsNullOrWhiteSpace(userName))
                throw new MembershipCreateUserException(MembershipCreateStatus.InvalidUserName);

            if (string.IsNullOrWhiteSpace(providerUserId))
                throw new MembershipCreateUserException(MembershipCreateStatus.InvalidProviderUserKey);     // not really the right status ??

            var user = _context.GetUser(userName);

            if (user == null)
            {
                // create a non-local account
                user = new MembershipAccount(userName)
                           {
                               IsConfirmed = true,
                               IsLocalAccount = false
                           };

                try
                {
                    _context.Save(user);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("MongoSimpleMembershipProvider.CreateOrUpdateOAuthAccount() ERROR : {0}", ex.ToString());
                    throw new MembershipCreateUserException(MembershipCreateStatus.ProviderError);
                }
            }

            var oam = _context.GetOAuthMembership(provider, providerUserId);

            if (oam == null)
            {
                // account doesn't exist, create a new one.
                oam = new OAuthMembership(provider, providerUserId, user.UserId);
            }
            else
            {
                // account already exist, update it
                oam.UserId = user.UserId;
            }

            try
            {
                _context.Save(oam);
            }
            catch (Exception ex)
            {
                Trace.TraceError("MongoSimpleMembershipProvider.CreateOrUpdateOAuthAccount() ERROR : {0}", ex.ToString());
                throw new MembershipCreateUserException(MembershipCreateStatus.ProviderError);
            }
        }

        public override void DeleteOAuthAccount(string provider, string providerUserId)
        {
            VerifyInitialized();

            var oam = _context.GetOAuthMembership(provider, providerUserId);

            if (oam == null)
                return;

            try
            {
                _context.RemoveById<OAuthMembership>(oam.Id);
            }
            catch (Exception ex)
            {
                Trace.TraceError("MongoSimpleMembershipProvider.DeleteOAuthAccount() ERROR : {0}", ex.ToString());
                throw new MembershipCreateUserException(MembershipCreateStatus.ProviderError);
            }
        }

        public override int GetUserIdFromOAuth(string provider, string providerUserId)
        {
            VerifyInitialized();

            var oam = _context.GetOAuthMembership(provider, providerUserId);

            return oam == null
                       ? -1
                       : oam.UserId;
        }

        public override string GetOAuthTokenSecret(string token)
        {
            VerifyInitialized();

            // Token is case sensitive
            var tk = _context.GetToken(token);

            return tk == null
                       ? string.Empty
                       : tk.Secret;
        }

        public override void StoreOAuthRequestToken(string requestToken, string requestTokenSecret)
        {
            VerifyInitialized();

            try
            {
                var tk = _context.GetToken(requestToken);

                if (tk == null)
                {
                    tk = new OAuthToken(requestToken, requestTokenSecret);
                }
                else
                {
                    // record already exists
                    if (tk.Secret.Equals(requestTokenSecret))
                        return;

                    tk.Secret = requestTokenSecret;
                }

                _context.Save(tk);
            }
            catch (Exception ex)
            {
                Trace.TraceError("MongoSimpleMembershipProvider.StoreOAuthRequestToken() ERROR : {0}", ex.ToString());
                throw new ProviderException("Failed to store OAuthToken");
            }
        }

        /// <summary>
        /// Replaces the request token with access token and secret
        /// </summary>
        /// <param name="requestToken">The request token</param>
        /// <param name="accessToken">The access token</param>
        /// <param name="accessTokenSecret">The access token secret</param>
        public override void ReplaceOAuthRequestTokenWithAccessToken(string requestToken, string accessToken, string accessTokenSecret)
        {
            VerifyInitialized();

            var tk = _context.GetToken(requestToken);

            if (tk == null) return;

            _context.RemoveById<OAuthToken>(tk.Id);

            // Although there are two different types of tokens, request token and access token,
            // we treat them the same in database records.
            StoreOAuthRequestToken(accessToken, accessTokenSecret);
        }

        public override void DeleteOAuthToken(string token)
        {
            VerifyInitialized();

            var tk = _context.GetToken(token);

            if (tk != null)
                _context.RemoveById<OAuthToken>(tk.Id);
        }

        #endregion


        #region MembershipProvider overrides

        public override string ApplicationName
        {
            get { return _appName; }
            set
            {
                Util.CheckAppNameLength(value);

                _appName = value;
            }
        }

        public override bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            Check.IsNotNull(username);
            Check.IsNotNull(oldPassword);
            Check.IsNotNull(newPassword);

            VerifyInitialized();

            var user = _context.GetUser(username);

            if (user == null)
                return false;

            return CheckPassword(user, oldPassword) && SetPassword(user, newPassword);
        }

        public override bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            throw new NotSupportedException();
        }

        public override MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out System.Web.Security.MembershipCreateStatus status)
        {
            throw new NotSupportedException();
        }

        public override bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            Check.IsNotNull(username);

            VerifyInitialized();

            var user = _context.GetUser(username);

            if (user == null)
                return false;

            try
            {
                var success = _context.RemoveById<MembershipAccount>(user.UserId);

                if (deleteAllRelatedData)
                    _context.RemoveOAuthMemberships(user.UserId);

                return success;
            }
            catch (Exception ex)
            {
                Trace.TraceError("MongoSimpleMembershipProvider.DeleteUser() ERROR : {0}", ex.ToString());
            }

            return false;
        }

        public override bool EnablePasswordReset
        {
            get { return false; }
        }

        public override bool EnablePasswordRetrieval
        {
            get { return false; }
        }

        public override MembershipUserCollection FindUsersByEmail(string emailToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            throw new NotSupportedException();
        }

        public override MembershipUserCollection FindUsersByName(string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            throw new NotSupportedException();
        }

        public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
        {
            totalRecords = _context.Users.Count();

            var results = new MembershipUserCollection();

            var users = _context.Users.Skip(pageIndex * pageSize).Take(pageSize);

            users.ForEach(x => results.Add(GetUser(x)));

            return results;
        }

        public override int GetNumberOfUsersOnline()
        {
            throw new NotSupportedException();
        }

        public override string GetPassword(string username, string answer)
        {
            throw new NotSupportedException();
        }

        public override MembershipUser GetUser(string username, bool userIsOnline)
        {
            Check.IsNotNull(username);

            VerifyInitialized();

            var user = _context.GetUser(username);

            return GetUser(user);
        }

        public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            Check.IsNotNull(providerUserKey);

            VerifyInitialized();

            var user = _context.FindOneById<MembershipAccount>(providerUserKey);

            return GetUser(user);
        }

        private MembershipUser GetUser(MembershipAccount user)
        {
            if (user == null)
                return null;

            var lastLogin = user.LastLoginDate.HasValue ? user.LastLoginDate.Value : DateTime.MinValue;
            var lastPasswordChange = user.PasswordChangedDate.HasValue ? user.PasswordChangedDate.Value : DateTime.MinValue;

            // NOTE: This requires a valid system.web/membership section in config with matching provider name
            return new MembershipUser(_providerName, user.UserName, user.UserId, null, null, null, user.IsConfirmed /* isApproved */, false, user.CreateDate, lastLogin, DateTime.MinValue, lastPasswordChange, DateTime.MinValue);
        }

        public override string GetUserNameByEmail(string email)
        {
            throw new NotSupportedException();
        }

        public override int MaxInvalidPasswordAttempts
        {
            get { return Int32.MaxValue; }
        }

        public override int MinRequiredNonAlphanumericCharacters
        {
            get { return _minRequiredNonalphanumericCharacters; }
        }

        public override int MinRequiredPasswordLength
        {
            get { return _minRequiredPasswordLength; }
        }

        public override int PasswordAttemptWindow
        {
            get { return Int32.MaxValue; }
        }

        public override MembershipPasswordFormat PasswordFormat
        {
            get { return MembershipPasswordFormat.Hashed; }
        }

        public override string PasswordStrengthRegularExpression
        {
            get { return _passwordStrengthRegularExpression; }
        }

        public override bool RequiresQuestionAndAnswer
        {
            get { return false; }
        }

        public override bool RequiresUniqueEmail
        {
            get { return false; }
        }

        public override string ResetPassword(string username, string answer)
        {
            throw new NotSupportedException();
        }

        public override bool UnlockUser(string userName)
        {
            throw new NotSupportedException();
        }

        public override void UpdateUser(MembershipUser user)
        {
            throw new NotSupportedException();
        }

        public override bool ValidateUser(string username, string password)
        {
            Check.IsNotNull(username);
            Check.IsNotNull(password);

            VerifyInitialized();

            var user = _context.GetUser(username);

            return user != null
                   && user.IsConfirmed
                   && user.IsLocalAccount
                   && CheckPassword(user, password);
        }

        #endregion

        private bool CheckPassword(MembershipAccount user, string password)
        {
            if (user == null || string.IsNullOrWhiteSpace(password))
                return false;

            var verificationSucceeded = Crypto.VerifyHashedPassword(user.Password, password + user.PasswordSalt);

            if (verificationSucceeded)
            {
                // Reset password failure count if applicable
                if (user.PasswordFailuresSinceLastSuccess > 0)
                {
                    user.PasswordFailuresSinceLastSuccess = 0;
                    _context.Save(user);
                }
            }
            else
            {
                user.PasswordFailuresSinceLastSuccess = user.PasswordFailuresSinceLastSuccess + 1;
                user.LastPasswordFailureDate = DateTime.UtcNow;
                _context.Save(user);
            }

            return verificationSucceeded;
        }

        private bool SetPassword(MembershipAccount user, string newPassword, bool clearVerificationToken = false)
        {
            if (user == null || string.IsNullOrWhiteSpace(newPassword))
                return false;

            var salt = Crypto.GenerateSalt();

            user.PasswordSalt = salt;
            user.Password = Crypto.HashPassword(newPassword + salt);
            user.PasswordChangedDate = DateTime.UtcNow;

            if (clearVerificationToken)
            {
                user.PasswordVerificationToken = null;
                user.PasswordVerificationTokenExpirationDate = null;
            }

            return _context.Save(user);
        }
    }
}
