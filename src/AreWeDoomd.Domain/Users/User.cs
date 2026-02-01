namespace AreWeDoomd.Domain.Users
{
    public sealed class User
    {
        public Guid Id { get; private set; }
        public string Username { get; private set; } = null!;
        public string Email { get; private set; } = null!;
        public string PasswordHash { get; private set; } = null!;
        public UserType UserType { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // EF Core için parameterless ctor (private/protected olabilir)
        private User() { }

        // Domain ctor: invariants burada korunur
        public User(Guid id, string username, string email, string passwordHash, UserType userType, DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Id cannot be empty.", nameof(id));
            }
            SetUsername(username);
            SetEmail(email);
            SetPasswordHash(passwordHash);
            UserType = userType;
            Id = id;
            CreatedAt = createdAt;
        }

        // Tercihen factory: Id/CreatedAt üretimi tek yerde
        public static User Create(string username, string email, string passwordHash, UserType userType, DateTimeOffset now)
            => new(Guid.NewGuid(), username, email, passwordHash, userType, now);

        public void ChangeUsername(string username, DateTimeOffset now)
        {
            SetUsername(username);
            Touch(now);
        }

        public void ChangeEmail(string email, DateTimeOffset now)
        {
            SetEmail(email);
            Touch(now);
        }

        public void ChangeUserType(UserType userType, DateTimeOffset now)
        {
            UserType = userType;
            Touch(now);
        }

        // PasswordHash domain’de “hash” olduğu varsayımıyla saklanır (plain password saklama yok)
        public void SetPassword(string newPasswordHash, DateTimeOffset now)
        {
            SetPasswordHash(newPasswordHash);
            Touch(now);
        }

        private void Touch(DateTimeOffset now) => UpdatedAt = now;

        private void SetUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username is required.", nameof(username));
            }

            username = username.Trim();

            if (username.Length is < 3 or > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(username), "Username must be 3..32 characters.");
            }

            Username = username;
        }

        private void SetEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            email = email.Trim();

            // Basit kontrol; daha iyi doğrulama Value Object ile yapılabilir
            if (!email.Contains('@') || email.Length > 254)
            {
                throw new ArgumentException("Email is invalid.", nameof(email));
            }

            Email = email;
        }

        private void SetPasswordHash(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException("PasswordHash is required.", nameof(passwordHash));
            }

            // Hash formatı (örn. Argon2/BCrypt)
            // En azından boş/çok kısa olmasın.
            if (passwordHash.Length < 20)
            {
                throw new ArgumentException("PasswordHash looks invalid.", nameof(passwordHash));
            }

            PasswordHash = passwordHash;
        }
    }
}
