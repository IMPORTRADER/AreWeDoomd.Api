using System.Text.RegularExpressions;

namespace AreWeDoomd.Domain.Users
{
    public sealed class User
    {
        private static readonly Regex UsernamePattern =
            new("^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

        public Guid Id { get; private set; }
        public string Username { get; private set; } = null!;
        public string Email { get; private set; } = null!;
        public string PasswordHash { get; private set; } = null!;
        public UserType UserType { get; private set; }
        public UserProfile Profile { get; private set; } = null!;
        public AiPersonality? AiPersonality { get; private set; }
        public bool IsAdmin { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // EF Core icin parameterless ctor (private/protected olabilir)
        private User() { }

        // Domain ctor: invariants burada korunur
        public User(Guid id, string username, string email, string passwordHash, UserType userType, DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Id cannot be empty.", nameof(id));
            }
            Id = id;
            SetUsername(username);
            SetEmail(email);
            SetPasswordHash(passwordHash);
            UserType = userType;

            Profile = UserProfile.CreateEmpty(createdAt);
            CreatedAt = createdAt;
        }

        // Tercihen factory: Id/CreatedAt uretimi tek yerde
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

        // PasswordHash domain'de saklanir (plain password saklama yok)
        public void SetPassword(string newPasswordHash, DateTimeOffset now)
        {
            SetPasswordHash(newPasswordHash);
            Touch(now);
        }

        public void SetAiPersonality(
            IReadOnlyList<string> traits, string typingStyle, string summary, DateTimeOffset now)
        {
            if (UserType != UserType.Ai)
            {
                throw new InvalidOperationException("Only AI users can have an AI personality.");
            }

            int nextVersion = (AiPersonality?.Version ?? 0) + 1;
            AiPersonality = AiPersonality.Create(traits, typingStyle, summary, nextVersion, now);
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

            if (username.Length is < 3 or > 24)
            {
                throw new ArgumentOutOfRangeException(nameof(username), "Username must be 3..24 characters.");
            }

            if (!UsernamePattern.IsMatch(username))
            {
                throw new ArgumentException("Username may only contain letters, digits and underscore.", nameof(username));
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

            // Basit kontrol; daha iyi dogrulama Value Object ile yapilabilir
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

            // Hash formati (orn. Argon2/BCrypt)
            // En azindan bos/cok kisa olmasin.
            if (passwordHash.Length < 20)
            {
                throw new ArgumentException("PasswordHash looks invalid.", nameof(passwordHash));
            }

            PasswordHash = passwordHash;
        }
    }
}