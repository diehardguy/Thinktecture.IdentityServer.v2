//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Kin.Identity.Repository.Domain
{
    using System;
    using System.Collections.Generic;
    
    public partial class User
    {
        public User()
        {
            this.UserRoles = new HashSet<UserRole>();
        }
    
        public int UserId { get; set; }
        public string Password { get; set; }
        public string PasswordSalt { get; set; }
        public string Email { get; set; }
        public bool IsApproved { get; set; }
        public bool IsLockedOut { get; set; }
        public System.DateTime DateCreated { get; set; }
        public Nullable<System.DateTime> DateLastActivity { get; set; }
        public byte[] ts { get; set; }
    
        public virtual UserProfile UserProfile { get; set; }
        public virtual ICollection<UserRole> UserRoles { get; set; }
    }
}
