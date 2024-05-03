namespace simplesqlclient
{
    internal class AuthenticationOptions
    {
        public AuthDetails AuthDetails { get; set; }

        public AuthenticationType AuthenticationType { get; set; }

        public static AuthenticationOptions Create(string userName, string password)
        {
            return new AuthenticationOptions
            {
                AuthDetails = new AuthDetails
                {
                    UserName = userName,
                    Password = password
                },
                AuthenticationType = AuthenticationType.SQLAUTH
            };
        }
    }

    internal class AuthDetails
    {
        public string UserName { get; set; }

        public string Password
        {
            set
            {
                EncryptedPassword = Utilities.ObfuscatePassword(value);
            }
        }

        public byte[] EncryptedPassword { get; internal set; }
    }
}
