CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'mpaz_matricula') THEN
        CREATE SCHEMA mpaz_matricula;
    END IF;
END $EF$;

CREATE TABLE mpaz_matricula."OpenIddictApplications" (
    "Id" text NOT NULL,
    "ApplicationType" character varying(50),
    "ClientId" character varying(100),
    "ClientSecret" text,
    "ClientType" character varying(50),
    "ConcurrencyToken" character varying(50),
    "ConsentType" character varying(50),
    "DisplayName" text,
    "DisplayNames" text,
    "JsonWebKeySet" text,
    "Permissions" text,
    "PostLogoutRedirectUris" text,
    "Properties" text,
    "RedirectUris" text,
    "Requirements" text,
    "Settings" text,
    CONSTRAINT "PK_OpenIddictApplications" PRIMARY KEY ("Id")
);

CREATE TABLE mpaz_matricula."OpenIddictScopes" (
    "Id" text NOT NULL,
    "ConcurrencyToken" character varying(50),
    "Description" text,
    "Descriptions" text,
    "DisplayName" text,
    "DisplayNames" text,
    "Name" character varying(200),
    "Properties" text,
    "Resources" text,
    CONSTRAINT "PK_OpenIddictScopes" PRIMARY KEY ("Id")
);

CREATE TABLE mpaz_matricula."OpenIddictAuthorizations" (
    "Id" text NOT NULL,
    "ApplicationId" text,
    "ConcurrencyToken" character varying(50),
    "CreationDate" timestamp with time zone,
    "Properties" text,
    "Scopes" text,
    "Status" character varying(50),
    "Subject" character varying(400),
    "Type" character varying(50),
    CONSTRAINT "PK_OpenIddictAuthorizations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~" FOREIGN KEY ("ApplicationId") REFERENCES mpaz_matricula."OpenIddictApplications" ("Id")
);

CREATE TABLE mpaz_matricula."OpenIddictTokens" (
    "Id" text NOT NULL,
    "ApplicationId" text,
    "AuthorizationId" text,
    "ConcurrencyToken" character varying(50),
    "CreationDate" timestamp with time zone,
    "ExpirationDate" timestamp with time zone,
    "Payload" text,
    "Properties" text,
    "RedemptionDate" timestamp with time zone,
    "ReferenceId" character varying(100),
    "Status" character varying(50),
    "Subject" character varying(400),
    "Type" character varying(150),
    CONSTRAINT "PK_OpenIddictTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId" FOREIGN KEY ("ApplicationId") REFERENCES mpaz_matricula."OpenIddictApplications" ("Id"),
    CONSTRAINT "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId" FOREIGN KEY ("AuthorizationId") REFERENCES mpaz_matricula."OpenIddictAuthorizations" ("Id")
);

CREATE UNIQUE INDEX "IX_OpenIddictApplications_ClientId" ON mpaz_matricula."OpenIddictApplications" ("ClientId");

CREATE INDEX "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type" ON mpaz_matricula."OpenIddictAuthorizations" ("ApplicationId", "Status", "Subject", "Type");

CREATE UNIQUE INDEX "IX_OpenIddictScopes_Name" ON mpaz_matricula."OpenIddictScopes" ("Name");

CREATE INDEX "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type" ON mpaz_matricula."OpenIddictTokens" ("ApplicationId", "Status", "Subject", "Type");

CREATE INDEX "IX_OpenIddictTokens_AuthorizationId" ON mpaz_matricula."OpenIddictTokens" ("AuthorizationId");

CREATE UNIQUE INDEX "IX_OpenIddictTokens_ReferenceId" ON mpaz_matricula."OpenIddictTokens" ("ReferenceId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261112002838_AgregarSso', '10.0.14');

COMMIT;

