using System;
using System.Collections.Generic;
using System.Configuration;
using LyphTEC.MongoSimpleMembership.Helpers;

namespace LyphTEC.MongoSimpleMembership.Models
{
    /// <summary>
    /// Represents a user account 
    /// </summary>
    public class MembershipAccount
    {
        public MembershipAccount(string userName)
        {
            Check.IsNotNull(userName);

            var now = DateTime.UtcNow;

            UserName = userName;
            UserNameLower = userName.ToLowerInvariant();
            
            ConfirmationToken = Util.GenerateToken();
            PasswordFailuresSinceLastSuccess = 0;
            PasswordChangedDate = now;
            CreateDate = now;

            Roles = new List<Role>();
        }

        public int UserId { get; set; }
        public string UserName { get; private set; }
        public string UserNameLower { get; private set; }

        public DateTime CreateDate { get; set; }
        public string ConfirmationToken { get; private set; }   // should be random & unique
        public bool IsConfirmed { get; set; }
        public DateTime? LastPasswordFailureDate { get; set; }
        public int PasswordFailuresSinceLastSuccess { get; set; }
        public string Password { get; set; }
        public DateTime? PasswordChangedDate { get; set; }
        public string PasswordSalt { get; set; } 
        public string PasswordVerificationToken { get; set; }
        public DateTime? PasswordVerificationTokenExpirationDate { get; set; }
        public DateTime? LastLoginDate { get; set; }

        public List<Role> Roles { get; set; }

        /// <summary>
        /// Gets the name of the collection when stored in Mongo. By default it's &quot;webpages_Membership&quot;, but can be overridden in config by app setting &quot;MongoDBSimpleMembership:MembershipAccountName&quot;
        /// </summary>
        /// <returns></returns>
        public static string GetCollectionName()
        {
            var name = "webpages_Membership";
            
            var setting = ConfigurationManager.AppSettings["MongoDBSimpleMembership:MembershipAccountName"];
            if (setting != null && !string.IsNullOrWhiteSpace(setting))
                name = setting;

            return name;
        }
    }
}
