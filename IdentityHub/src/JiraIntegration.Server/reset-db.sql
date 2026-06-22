-- Reset IdentityHub to a clean demo state for submission.
-- Login: demo / Demo123!  or  testuser / Test123!
-- No Jira connection, API keys, or ticket history.
--
-- Demo users are created by reset-demo.ps1 via ASP.NET Core Identity (UserManager).

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
