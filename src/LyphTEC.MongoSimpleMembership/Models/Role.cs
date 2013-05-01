using System;
using System.Configuration;
using System.Diagnostics.Contracts;

namespace LyphTEC.MongoSimpleMembership.Models
{
    /// <summary>
    /// Represents a security role
    /// </summary>
    public class Role : IEquatable<Role>
    {
        public Role(string roleName)
        {
            Contract.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(roleName));

            RoleName = roleName;
        }

        public int RoleId { get; set; }
        public string RoleName { get; private set; }

        #region IEquatable<Role> Members

        public bool Equals(Role other)
        {
            if (ReferenceEquals(other, null)) return false;

            return ReferenceEquals(this, other) || 
                (RoleId.Equals(other.RoleId) && RoleName.Equals(other.RoleName));
        }

        #endregion

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null)) return false;
            if (ReferenceEquals(this, obj)) return true;

            return obj.GetType() == typeof (Role) && Equals((Role) obj);
        }

        public override int GetHashCode()
        {
            var hashRoleName = (RoleName == null) ? 0 : RoleName.GetHashCode();
            var hashRoleId = RoleId.GetHashCode();

            return hashRoleName ^ hashRoleId;
        }

        /// <summary>
        /// Gets the name of the collection when stored in Mongo. By default it's &quot;webpages_Role&quot; (similar to the standard SimpleMembershipProvider in WebMatrix.WebData), but can be overridden in config by app setting &quot;MongoDBSimpleMembership:RoleName&quot;
        /// </summary>
        /// <returns></returns>
        public static string GetCollectionName()
        {
            var name = "webpages_Role";
            
            var setting = ConfigurationManager.AppSettings["MongoSimpleMembership:RoleName"];
            if (setting != null && !string.IsNullOrWhiteSpace(setting))
                name = setting;

            return name;
        }
    }
}
