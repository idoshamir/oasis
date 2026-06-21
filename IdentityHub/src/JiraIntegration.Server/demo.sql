-- Reset IdentityHub to a clean demo state for submission.
-- Login: demo / Demo123!  or  testuser / Test123!
-- No Jira connection, API keys, or ticket history.

PRAGMA foreign_keys = ON;

DELETE FROM ApiKeys;
DELETE FROM JiraConnections;
DELETE FROM NhiTicketLedgers;
DELETE FROM Users;

INSERT INTO Users (Id, Username, PasswordHash, Salt)
VALUES (
    'a0000000-0000-4000-8000-000000000001',
    'demo',
    'vdbZSQyVtayHR9zrV5d/4X84DuMN9u9d0qx+WVUwDAw=',
    'T2FzaXNEZW1vU2FsdCEhIQ=='
);

INSERT INTO Users (Id, Username, PasswordHash, Salt)
VALUES (
    'a0000000-0000-4000-8000-000000000002',
    'testuser',
    'g8TW52uL3/JQew6xMqlFpCwq7KecXRqWHNA5y4nRk/0=',
    'T2FzaXNUZXN0U2FsdCEh'
);
