-- Reset IdentityHub to a clean demo state for submission.
-- Login: demo / Demo123!  or  testuser / Test123!
-- No Jira connection, API keys, or ticket history.
--
-- Passwords use LegacySalt + LegacyPasswordHash (pre-Identity PBKDF2 format).
-- The app migrates them to Identity hashes on first successful login.

PRAGMA foreign_keys = ON;

DELETE FROM OpenIddictTokens;
DELETE FROM OpenIddictAuthorizations;
DELETE FROM OpenIddictApplications;
DELETE FROM ApiKeys;
DELETE FROM JiraConnections;
DELETE FROM NhiTicketLedgers;
DELETE FROM AspNetUserClaims;
DELETE FROM AspNetUserLogins;
DELETE FROM AspNetUserRoles;
DELETE FROM AspNetUserTokens;
DELETE FROM Users;

INSERT INTO Users (
    Id,
    UserName,
    NormalizedUserName,
    PasswordHash,
    LegacyPasswordHash,
    LegacySalt,
    AccessFailedCount,
    EmailConfirmed,
    LockoutEnabled,
    PhoneNumberConfirmed,
    TwoFactorEnabled,
    SecurityStamp,
    ConcurrencyStamp)
VALUES (
    'a0000000-0000-4000-8000-000000000001',
    'demo',
    'DEMO',
    NULL,
    'vdbZSQyVtayHR9zrV5d/4X84DuMN9u9d0qx+WVUwDAw=',
    'T2FzaXNEZW1vU2FsdCEhIQ==',
    0,
    0,
    0,
    0,
    0,
    'demo-security-stamp-01',
    'demo-concurrency-stamp-01');

INSERT INTO Users (
    Id,
    UserName,
    NormalizedUserName,
    PasswordHash,
    LegacyPasswordHash,
    LegacySalt,
    AccessFailedCount,
    EmailConfirmed,
    LockoutEnabled,
    PhoneNumberConfirmed,
    TwoFactorEnabled,
    SecurityStamp,
    ConcurrencyStamp)
VALUES (
    'a0000000-0000-4000-8000-000000000002',
    'testuser',
    'TESTUSER',
    NULL,
    'g8TW52uL3/JQew6xMqlFpCwq7KecXRqWHNA5y4nRk/0=',
    'T2FzaXNUZXN0U2FsdCEh',
    0,
    0,
    0,
    0,
    0,
    'testuser-security-stamp-01',
    'testuser-concurrency-stamp-01');
